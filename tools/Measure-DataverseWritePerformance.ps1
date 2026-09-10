[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [ValidateRange(1, 5000)][int]$Count = 100,
    [ValidateNotNullOrEmpty()][int[]]$MaxDop = @(1, 8, 32),
    [ValidateRange(1, 1000)][int]$BatchSize = 100,
    [ValidateRange(1, 1000)][int]$BulkSize = 100,
    [string]$ConnectionName
)

$ErrorActionPreference = 'Stop'
$connectionParameters = @{}
if ($ConnectionName) { $connectionParameters.ConnectionName = $ConnectionName }

$suffix = (Get-Date -Format 'MMddHHmmss') + (Get-Random -Minimum 100 -Maximum 999)
$schemaName = "new_PsdvBenchmark$suffix"
$logicalName = $schemaName.ToLowerInvariant()
$primaryName = "${logicalName}name"
$primaryId = "${logicalName}id"
$tableCreated = $false
$results = [Collections.Generic.List[object]]::new()

function Invoke-TimedScenario {
    param(
        [string]$Operation,
        [string]$Transport,
        [int]$LogicalOperationCount,
        [int]$RequestedMaxDop,
        [scriptblock]$Action
    )
    $timer = [Diagnostics.Stopwatch]::StartNew()
    & $Action
    $timer.Stop()
    $script:results.Add([pscustomobject]@{
        Operation = $Operation
        Transport = $Transport
        RequestedMaxDop = $RequestedMaxDop
        OperationCount = $LogicalOperationCount
        Elapsed = $timer.Elapsed
        OperationsPerSecond = [math]::Round($LogicalOperationCount / $timer.Elapsed.TotalSeconds, 2)
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
    param([string]$Cohort)
    @(1..$Count | ForEach-Object {
        $id = [guid]::NewGuid()
        [pscustomobject]@{ Id = $id; Cohort = $Cohort; Name = "$Cohort create $_" }
    })
}

function Invoke-ChunkedAction {
    param([object[]]$Rows, [int]$Size, [scriptblock]$Action)
    for ($offset = 0; $offset -lt $Rows.Count; $offset += $Size) {
        $last = [math]::Min($offset + $Size - 1, $Rows.Count - 1)
        $chunk = @($Rows[$offset..$last])
        & $Action -chunk $chunk
    }
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
    $primaryId = $metadata.PrimaryIdAttribute
    $primaryName = $metadata.PrimaryNameAttribute
    Write-Verbose "Benchmarking $logicalName through $script:tableSetName."

    $cohorts = [ordered]@{}
    foreach ($dop in $MaxDop) {
        if ($dop -lt 1 -or $dop -gt 1024) { throw "MaxDop value '$dop' must be between 1 and 1024." }
        $rows = Get-BenchmarkRowSet "individual-$dop"
        $cohorts["Individual:$dop"] = $rows
        Invoke-TimedScenario POST Individual $Count $dop {
            $null = $rows | ForEach-Object {
                @{ ContentId = $_.Id.ToString(); Method = 'POST'; Uri = $script:tableSetName
                    Value = @{ $primaryId = $_.Id; $primaryName = $_.Name } }
            } | Invoke-DataverseRequest -MaxDop $dop @connectionParameters
        }
    }

    $batchRows = Get-BenchmarkRowSet 'batch'
    $cohorts.Batch = $batchRows
    Invoke-TimedScenario POST "Batch($BatchSize)" $Count 1 {
        $null = $batchRows | ForEach-Object {
            @{ ContentId = $_.Id.ToString(); Method = 'POST'; Uri = $script:tableSetName
                Value = @{ $primaryId = $_.Id; $primaryName = $_.Name } }
        } | Invoke-DataverseRequest -BatchSize $BatchSize -MaxDop 1 @connectionParameters
    }

    $bulkRows = Get-BenchmarkRowSet 'create-multiple'
    $cohorts.CreateMultiple = $bulkRows
    Invoke-TimedScenario POST "CreateMultiple($BulkSize)" $Count 1 {
        Invoke-ChunkedAction $bulkRows $BulkSize {
            param($chunk)
            $targets = @($chunk | ForEach-Object { @{ $primaryId = $_.Id; $primaryName = $_.Name } })
            $null = Invoke-DataverseCreateMultiple $script:tableSetName $logicalName $targets `
                @connectionParameters -Confirm:$false
        }
    }
    Assert-TableCount ($Count * $cohorts.Count)

    foreach ($dop in $MaxDop) {
        $rows = $cohorts["Individual:$dop"]
        Invoke-TimedScenario PATCH Individual $Count $dop {
            $null = $rows | ForEach-Object {
                @{ ContentId = $_.Id.ToString(); Method = 'PATCH'; Uri = "$script:tableSetName($($_.Id))"
                    Headers = @{ 'If-Match' = '*' }; Value = @{ $primaryName = "$($_.Cohort) updated" } }
            } | Invoke-DataverseRequest -MaxDop $dop @connectionParameters
        }
    }

    Invoke-TimedScenario PATCH "Batch($BatchSize)" $Count 1 {
        $null = $batchRows | ForEach-Object {
            @{ ContentId = $_.Id.ToString(); Method = 'PATCH'; Uri = "$script:tableSetName($($_.Id))"
                Headers = @{ 'If-Match' = '*' }; Value = @{ $primaryName = 'batch updated' } }
        } | Invoke-DataverseRequest -BatchSize $BatchSize -MaxDop 1 @connectionParameters
    }

    Invoke-TimedScenario PATCH "UpdateMultiple($BulkSize)" $Count 1 {
        Invoke-ChunkedAction $bulkRows $BulkSize {
            param($chunk)
            $targets = @($chunk | ForEach-Object { @{ $primaryId = $_.Id; $primaryName = 'update-multiple updated' } })
            $null = Invoke-DataverseUpdateMultiple $script:tableSetName $logicalName $targets `
                @connectionParameters -Confirm:$false
        }
    }
    Assert-TableCount ($Count * $cohorts.Count)

    foreach ($dop in $MaxDop) {
        $rows = $cohorts["Individual:$dop"]
        Invoke-TimedScenario DELETE Individual $Count $dop {
            $null = $rows | ForEach-Object {
                @{ ContentId = $_.Id.ToString(); Method = 'DELETE'; Uri = "$script:tableSetName($($_.Id))"
                    Headers = @{ 'If-Match' = '*' } }
            } | Invoke-DataverseRequest -MaxDop $dop @connectionParameters
        }
    }

    Invoke-TimedScenario DELETE "Batch($BatchSize)" $Count 1 {
        $null = $batchRows | ForEach-Object {
            @{ ContentId = $_.Id.ToString(); Method = 'DELETE'; Uri = "$script:tableSetName($($_.Id))"
                Headers = @{ 'If-Match' = '*' } }
        } | Invoke-DataverseRequest -BatchSize $BatchSize -MaxDop 1 @connectionParameters
    }

    Invoke-TimedScenario DELETE Individual $Count 20 {
        $null = $bulkRows | ForEach-Object {
            @{ ContentId = $_.Id.ToString(); Method = 'DELETE'; Uri = "$script:tableSetName($($_.Id))"
                Headers = @{ 'If-Match' = '*' } }
        } | Invoke-DataverseRequest -MaxDop 20 @connectionParameters
    }
    Assert-TableCount 0
    $results
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
