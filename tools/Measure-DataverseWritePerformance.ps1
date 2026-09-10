[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [ValidateRange(1, 5000)][int]$Count = 100,
    [ValidateNotNullOrEmpty()][int[]]$MaxDop = @(1, 8, 20, 32),
    [ValidateNotNullOrEmpty()][int[]]$BatchMaxDop = @(1, 4, 8),
    [ValidateNotNullOrEmpty()][int[]]$BulkMaxDop = @(1, 4),
    [ValidateRange(1, 1000)][int]$BatchSize = 20,
    [ValidateRange(1, 1000)][int]$BulkSize = 50,
    [ValidateRange(1, 20)][int]$RepeatCount = 3,
    [ValidateRange(0, 100)][int]$WarmUpCount = 5,
    [int]$RandomSeed = 8848,
    [switch]$SummaryOnly,
    [string]$ConnectionName
)

$ErrorActionPreference = 'Stop'
$connectionParameters = @{}
if ($ConnectionName) { $connectionParameters.ConnectionName = $ConnectionName }

foreach ($dop in @($MaxDop) + @($BatchMaxDop) + @($BulkMaxDop)) {
    if ($dop -lt 1 -or $dop -gt 1024) { throw "MaxDop value '$dop' must be between 1 and 1024." }
}

$bulkEnvelopeCount = [math]::Ceiling($Count / [double]$BulkSize)
$effectiveBulkMaxDop = @($BulkMaxDop | Sort-Object -Unique)
if ($bulkEnvelopeCount -eq 1 -and @($effectiveBulkMaxDop | Where-Object { $_ -gt 1 }).Count) {
    Write-Warning "Count $Count and BulkSize $BulkSize produce one bulk envelope; BulkMaxDop above 1 cannot add concurrency and will be skipped."
    $effectiveBulkMaxDop = @(1)
}

$batchEnvelopeCount = [math]::Ceiling($Count / [double]$BatchSize)
$effectiveBatchMaxDop = @($BatchMaxDop | Sort-Object -Unique)
if ($batchEnvelopeCount -eq 1 -and @($effectiveBatchMaxDop | Where-Object { $_ -gt 1 }).Count) {
    Write-Warning "Count $Count and BatchSize $BatchSize produce one batch envelope; BatchMaxDop above 1 cannot add concurrency and will be skipped."
    $effectiveBatchMaxDop = @(1)
}

$suffix = (Get-Date -Format 'MMddHHmmss') + (Get-Random -Minimum 100 -Maximum 999)
$schemaName = "new_PsdvBenchmark$suffix"
$logicalName = $schemaName.ToLowerInvariant()
$tableCreated = $false
$results = [Collections.Generic.List[object]]::new()
$random = [Random]::new($RandomSeed)

function Add-TimedResult {
    param([int]$Iteration, [string]$Operation, [pscustomobject]$Scenario,
        [int]$EnvelopeCount, [timespan]$Elapsed)
    $script:results.Add([pscustomobject]@{
        Iteration = $Iteration
        Operation = $Operation
        Transport = $Scenario.Transport
        RequestedMaxDop = $Scenario.MaxDop
        EnvelopeCount = $EnvelopeCount
        OperationCount = $Scenario.Rows.Count
        Elapsed = $Elapsed
        OperationsPerSecond = [math]::Round($Scenario.Rows.Count / $Elapsed.TotalSeconds, 2)
    })
}

function Get-TableCount {
    $response = Invoke-DataverseRequest -Uri "${script:tableSetName}?`$count=true&`$top=1" @connectionParameters
    [int](($response.Content | ConvertFrom-Json).'@odata.count')
}

function Assert-TableCount {
    param([int]$Expected)
    $actual = Get-TableCount
    if ($actual -ne $Expected) { throw "Expected $Expected rows in $script:tableSetName, but found $actual." }
}

function Get-BenchmarkRowSet {
    param([string]$Cohort, [int]$RowCount)
    @(1..$RowCount | ForEach-Object {
        $id = [guid]::NewGuid()
        [pscustomobject]@{ Id = $id; Cohort = $Cohort; Name = "$Cohort create $_" }
    })
}

function Get-BulkRequest {
    param([object[]]$Rows, [int]$Size, [ValidateSet('POST', 'PATCH')][string]$Operation)
    for ($offset = 0; $offset -lt $Rows.Count; $offset += $Size) {
        $last = [math]::Min($offset + $Size - 1, $Rows.Count - 1)
        $chunk = @($Rows[$offset..$last])
        $targets = if ($Operation -eq 'POST') {
            @($chunk | ForEach-Object {
                @{ '@odata.type' = "Microsoft.Dynamics.CRM.$script:logicalName"
                    $script:primaryId = $_.Id; $script:primaryName = $_.Name }
            })
        } else {
            @($chunk | ForEach-Object {
                @{ '@odata.type' = "Microsoft.Dynamics.CRM.$script:logicalName"
                    $script:primaryId = $_.Id; $script:primaryName = "$($_.Cohort) updated" }
            })
        }
        $actionName = if ($Operation -eq 'POST') { 'CreateMultiple' } else { 'UpdateMultiple' }
        @{ ContentId = "bulk-$offset"; Method = 'POST'
            Uri = "$script:tableSetName/Microsoft.Dynamics.CRM.$actionName"
            Value = @{ Targets = $targets } }
    }
}

function Invoke-Scenario {
    param([pscustomobject]$Scenario,
        [ValidateSet('POST', 'PATCH', 'DELETE')][string]$Operation,
        [int]$Iteration, [switch]$Measure)

    $timer = [Diagnostics.Stopwatch]::StartNew()
    switch ($Scenario.Kind) {
        'Individual' {
            $null = $Scenario.Rows | ForEach-Object {
                $request = @{ ContentId = $_.Id.ToString(); Method = $Operation }
                if ($Operation -eq 'POST') {
                    $request.Uri = $script:tableSetName
                    $request.Value = @{ $script:primaryId = $_.Id; $script:primaryName = $_.Name }
                } else {
                    $request.Uri = "$script:tableSetName($($_.Id))"
                    $request.Headers = @{ 'If-Match' = '*' }
                    if ($Operation -eq 'PATCH') {
                        $request.Value = @{ $script:primaryName = "$($_.Cohort) updated" }
                    }
                }
                $request
            } | Invoke-DataverseRequest -MaxDop $Scenario.MaxDop @connectionParameters
            $envelopeCount = $Scenario.Rows.Count
        }
        'Batch' {
            $null = $Scenario.Rows | ForEach-Object {
                $request = @{ ContentId = $_.Id.ToString(); Method = $Operation }
                if ($Operation -eq 'POST') {
                    $request.Uri = $script:tableSetName
                    $request.Value = @{ $script:primaryId = $_.Id; $script:primaryName = $_.Name }
                } else {
                    $request.Uri = "$script:tableSetName($($_.Id))"
                    $request.Headers = @{ 'If-Match' = '*' }
                    if ($Operation -eq 'PATCH') {
                        $request.Value = @{ $script:primaryName = "$($_.Cohort) updated" }
                    }
                }
                $request
            } | Invoke-DataverseRequest -BatchSize $Scenario.Size -MaxDop $Scenario.MaxDop @connectionParameters
            $envelopeCount = [math]::Ceiling($Scenario.Rows.Count / [double]$Scenario.Size)
        }
        'Bulk' {
            if ($Operation -eq 'DELETE') {
                $null = $Scenario.Rows | ForEach-Object {
                    @{ ContentId = $_.Id.ToString(); Method = 'DELETE'; Uri = "$script:tableSetName($($_.Id))"
                        Headers = @{ 'If-Match' = '*' } }
                } | Invoke-DataverseRequest -MaxDop 20 @connectionParameters
                $envelopeCount = $Scenario.Rows.Count
                break
            }
            $null = Get-BulkRequest $Scenario.Rows $Scenario.Size $Operation |
                Invoke-DataverseRequest -MaxDop $Scenario.MaxDop @connectionParameters
            $envelopeCount = [math]::Ceiling($Scenario.Rows.Count / [double]$Scenario.Size)
        }
    }
    $timer.Stop()
    if ($Measure) { Add-TimedResult $Iteration $Operation $Scenario $envelopeCount $timer.Elapsed }
}

function Get-ShuffledScenario {
    param([object[]]$Scenario)
    @($Scenario | Sort-Object { $script:random.Next() })
}

function Get-Median {
    param([double[]]$Value)
    $ordered = @($Value | Sort-Object)
    $middle = [math]::Floor($ordered.Count / 2)
    if ($ordered.Count % 2) { return $ordered[$middle] }
    ($ordered[$middle - 1] + $ordered[$middle]) / 2
}

function Get-ScenarioSet {
    param([int]$Iteration, [int]$RowCount)
    $scenarios = [Collections.Generic.List[object]]::new()
    foreach ($dop in $MaxDop | Sort-Object -Unique) {
        $scenarios.Add([pscustomobject]@{
            Kind = 'Individual'; Transport = 'Individual'; MaxDop = $dop; Size = 0
            Rows = Get-BenchmarkRowSet "run-$Iteration-individual-$dop" $RowCount
        })
    }
    foreach ($dop in $effectiveBatchMaxDop) {
        $scenarios.Add([pscustomobject]@{
            Kind = 'Batch'; Transport = "Batch($BatchSize)"; MaxDop = $dop; Size = $BatchSize
            Rows = Get-BenchmarkRowSet "run-$Iteration-batch-$dop" $RowCount
        })
    }
    foreach ($dop in $effectiveBulkMaxDop) {
        $scenarios.Add([pscustomobject]@{
            Kind = 'Bulk'; Transport = "Bulk($BulkSize)"; MaxDop = $dop; Size = $BulkSize
            Rows = Get-BenchmarkRowSet "run-$Iteration-bulk-$dop" $RowCount
        })
    }
    @($scenarios)
}

if (!$PSCmdlet.ShouldProcess(
    $schemaName,
    "Create a disposable Dataverse table, benchmark POST/PATCH/DELETE, then delete the table")) { return }

try {
    Write-Verbose "Creating disposable table $schemaName."
    $null = New-DataverseTable -SchemaName $schemaName -DisplayName 'PSDataverse benchmark' `
        -DisplayCollectionName 'PSDataverse benchmarks' -PrimaryNameSchemaName "${schemaName}Name" `
        -OwnershipType OrganizationOwned @connectionParameters -Confirm:$false
    $tableCreated = $true

    $metadataUri = "EntityDefinitions(LogicalName='$logicalName')?`$select=LogicalName,EntitySetName,PrimaryIdAttribute,PrimaryNameAttribute"
    $metadata = $null
    for ($attempt = 1; $attempt -le 60 -and !$metadata; $attempt++) {
        try {
            $response = Invoke-DataverseRequest -Uri $metadataUri @connectionParameters
            $metadata = $response.Content | ConvertFrom-Json
        } catch {
            if ($attempt -eq 60) { throw }
            Start-Sleep -Seconds 2
        }
    }
    $script:tableSetName = $metadata.EntitySetName
    $script:primaryId = $metadata.PrimaryIdAttribute
    $script:primaryName = $metadata.PrimaryNameAttribute
    $script:logicalName = $metadata.LogicalName
    Write-Verbose "Benchmarking $logicalName through $script:tableSetName."

    if ($WarmUpCount -gt 0) {
        Write-Verbose "Warming individual, batch, and bulk write paths with $WarmUpCount rows each."
        $warmScenarios = @(
            [pscustomobject]@{ Kind = 'Individual'; Transport = 'Individual'; MaxDop = 20; Size = 0
                Rows = Get-BenchmarkRowSet 'warm-individual' $WarmUpCount }
            [pscustomobject]@{ Kind = 'Batch'; Transport = "Batch($BatchSize)"; MaxDop = 1; Size = $BatchSize
                Rows = Get-BenchmarkRowSet 'warm-batch' $WarmUpCount }
            [pscustomobject]@{ Kind = 'Bulk'; Transport = "Bulk($BulkSize)"; MaxDop = 1; Size = $BulkSize
                Rows = Get-BenchmarkRowSet 'warm-bulk' $WarmUpCount }
        )
        foreach ($operation in 'POST', 'PATCH', 'DELETE') {
            foreach ($scenario in $warmScenarios) { Invoke-Scenario $scenario $operation 0 }
        }
        Assert-TableCount 0
    }

    for ($iteration = 1; $iteration -le $RepeatCount; $iteration++) {
        $scenarios = Get-ScenarioSet $iteration $Count
        Write-Verbose "Starting measured iteration $iteration of $RepeatCount with $($scenarios.Count) scenarios."
        foreach ($operation in 'POST', 'PATCH') {
            foreach ($scenario in Get-ShuffledScenario $scenarios) {
                Invoke-Scenario $scenario $operation $iteration -Measure
            }
            Assert-TableCount ($Count * $scenarios.Count)
        }
        foreach ($scenario in Get-ShuffledScenario @($scenarios | Where-Object Kind -NE 'Bulk')) {
            Invoke-Scenario $scenario DELETE $iteration -Measure
        }
        foreach ($scenario in $scenarios | Where-Object Kind -EQ 'Bulk') {
            Invoke-Scenario $scenario DELETE $iteration
        }
        Assert-TableCount 0
    }
    if ($SummaryOnly) {
        $results | Group-Object Operation, Transport, RequestedMaxDop | ForEach-Object {
            $sample = @($_.Group)
            $rates = @($sample.OperationsPerSecond)
            [pscustomobject]@{
                Operation = $sample[0].Operation
                Transport = $sample[0].Transport
                RequestedMaxDop = $sample[0].RequestedMaxDop
                EnvelopeCount = $sample[0].EnvelopeCount
                OperationCount = $sample[0].OperationCount
                Samples = $sample.Count
                MedianOperationsPerSecond = [math]::Round((Get-Median $rates), 2)
                MinimumOperationsPerSecond = [math]::Round(($rates | Measure-Object -Minimum).Minimum, 2)
                MaximumOperationsPerSecond = [math]::Round(($rates | Measure-Object -Maximum).Maximum, 2)
            }
        } | Sort-Object Operation, Transport, RequestedMaxDop
    } else {
        $results
    }
}
finally {
    if ($tableCreated) {
        if ($logicalName -notmatch '^new_psdvbenchmark[0-9]{13}$') {
            throw "Refusing to clean up unexpected table '$logicalName'."
        }
        Write-Verbose "Removing disposable table $logicalName."
        Remove-DataverseTable $logicalName @connectionParameters -Confirm:$false -ErrorAction Stop | Out-Null
    }
}
