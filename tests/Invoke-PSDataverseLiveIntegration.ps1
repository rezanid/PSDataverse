[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)]$Connection,
    [ValidateRange(5, 120)][int]$MetadataRetryCount = 60,
    [ValidateRange(1, 10)][int]$MetadataRetryDelaySeconds = 2
)

$ErrorActionPreference = 'Stop'

foreach ($command in @(
    'Get-DataverseRow',
    'Invoke-DataverseCreateMultiple',
    'Invoke-DataverseRequest',
    'Invoke-DataverseUpdateMultiple',
    'Invoke-DataverseUpsertMultiple',
    'New-DataverseRow',
    'New-DataverseTable',
    'Remove-DataverseRow',
    'Remove-DataverseTable',
    'Set-DataverseRow'
)) {
    if (!(Get-Command $command -ErrorAction Ignore)) {
        throw "Required command '$command' is not loaded. Import the built PSDataverse module first."
    }
}

$connectionParameters = @{ Connection = $Connection }
$suffix = (Get-Date -Format 'MMddHHmmss') + (Get-Random -Minimum 100 -Maximum 999)
$schemaName = "new_PsdvIntegration$suffix"
$logicalName = $schemaName.ToLowerInvariant()
$testFailed = $false
$script:liveState = [pscustomobject]@{
    SchemaName = $schemaName
    LogicalName = $logicalName
    TableCreated = $false
    TableSetName = $null
    PrimaryId = $null
    PrimaryName = $null
    BatchIds = $null
    MultipleIds = $null
    ServiceProtectionSignals = $null
    Results = [Collections.Generic.List[object]]::new()
}

function Assert-LiveCondition {
    param([bool]$Condition, [string]$Message)
    if (!$Condition) { throw "Live integration assertion failed: $Message" }
}

function Invoke-LivePhase {
    param([string]$Name, [scriptblock]$Body)
    Write-Verbose "Starting live integration phase '$Name'."
    $timer = [Diagnostics.Stopwatch]::StartNew()
    & $Body
    $timer.Stop()
    $script:liveState.Results.Add([pscustomobject]@{
        Phase = $Name
        Status = 'Passed'
        Elapsed = $timer.Elapsed
    })
    Write-Verbose "Passed live integration phase '$Name' in $($timer.Elapsed)."
}

function Get-LiveTableCount {
    $response = Invoke-DataverseRequest -Uri "$($script:liveState.TableSetName)?`$count=true&`$top=1" @connectionParameters
    [int](($response.Content | ConvertFrom-Json).'@odata.count')
}

function Assert-LiveTableCount {
    param([int]$Expected)
    $actual = Get-LiveTableCount
    Assert-LiveCondition ($actual -eq $Expected) "Expected $Expected rows, but found $actual."
}

if (!$PSCmdlet.ShouldProcess(
    $schemaName,
    'Create a disposable Dataverse table, run the live integration suite, and delete the table')) {
    return
}

try {
    Invoke-LivePhase 'ConnectionAndServiceProtectionSignals' {
        $response = Invoke-DataverseRequest -Uri WhoAmI @connectionParameters
        Assert-LiveCondition ($response.StatusCode -eq 'OK') 'WhoAmI did not return HTTP 200.'
        $script:liveState.ServiceProtectionSignals = [pscustomobject]@{
            DegreeOfParallelismHint = $response.Headers['x-ms-dop-hint']
            BurstRequestsRemaining = $response.Headers['x-ms-ratelimit-burst-remaining-xrm-requests']
            TimeRemaining = $response.Headers['x-ms-ratelimit-time-remaining-xrm-requests']
        }
    }

    Invoke-LivePhase 'CreateDisposableTable' {
        $null = New-DataverseTable -SchemaName $script:liveState.SchemaName -DisplayName 'PSDataverse integration test' `
            -DisplayCollectionName 'PSDataverse integration tests' `
            -PrimaryNameSchemaName "$($script:liveState.SchemaName)Name" -OwnershipType OrganizationOwned `
            @connectionParameters -Confirm:$false
        $script:liveState.TableCreated = $true

        $metadataUri = "EntityDefinitions(LogicalName='$($script:liveState.LogicalName)')?`$select=LogicalName,EntitySetName,PrimaryIdAttribute,PrimaryNameAttribute"
        $metadata = $null
        for ($attempt = 1; $attempt -le $MetadataRetryCount -and !$metadata; $attempt++) {
            try {
                $metadataResponse = Invoke-DataverseRequest -Uri $metadataUri @connectionParameters
                $metadata = $metadataResponse.Content | ConvertFrom-Json
            }
            catch {
                if ($attempt -eq $MetadataRetryCount) { throw }
                Start-Sleep -Seconds $MetadataRetryDelaySeconds
            }
        }
        $script:liveState.TableSetName = $metadata.EntitySetName
        $script:liveState.PrimaryId = $metadata.PrimaryIdAttribute
        $script:liveState.PrimaryName = $metadata.PrimaryNameAttribute
        Assert-LiveCondition (
            [string]::IsNullOrWhiteSpace($metadata.LogicalName) -or
            $metadata.LogicalName -eq $script:liveState.LogicalName
        ) "Metadata returned logical name '$($metadata.LogicalName)' instead of '$($script:liveState.LogicalName)'."
        Assert-LiveCondition (![string]::IsNullOrWhiteSpace($script:liveState.TableSetName)) 'EntitySetName was not returned.'
    }

    Invoke-LivePhase 'ConvenienceCrud' {
        $id = [guid]::NewGuid()
        $body = @{ $script:liveState.PrimaryId = $id; $script:liveState.PrimaryName = 'crud-created' }
        $null = New-DataverseRow $script:liveState.TableSetName $body @connectionParameters -Confirm:$false

        $created = Get-DataverseRow $script:liveState.TableSetName $id @connectionParameters
        Assert-LiveCondition ($created.($script:liveState.PrimaryName) -eq 'crud-created') 'New-DataverseRow or Get-DataverseRow failed.'

        $updated = Set-DataverseRow $script:liveState.TableSetName $id `
            @{ $script:liveState.PrimaryName = 'crud-updated' } -PassThru @connectionParameters -Confirm:$false
        Assert-LiveCondition ($updated.($script:liveState.PrimaryName) -eq 'crud-updated') 'Set-DataverseRow did not return the updated row.'

        $null = Remove-DataverseRow $script:liveState.TableSetName $id @connectionParameters -Confirm:$false
        Assert-LiveTableCount 0
    }

    Invoke-LivePhase 'BatchCreate' {
        $script:liveState.BatchIds = @(1..3 | ForEach-Object { [guid]::NewGuid() })
        $operations = for ($index = 0; $index -lt $script:liveState.BatchIds.Count; $index++) {
            @{
                ContentId = "batch-$index"
                Method = 'POST'
                Uri = $script:liveState.TableSetName
                Value = @{
                    $script:liveState.PrimaryId = $script:liveState.BatchIds[$index]
                    $script:liveState.PrimaryName = "batch-$index"
                }
            }
        }
        $batchResults = @($operations |
            Invoke-DataverseRequest -BatchSize 2 -MaxDop 2 @connectionParameters)
        Assert-LiveCondition ($batchResults.Count -eq 2) 'Expected two batch envelopes.'
        Assert-LiveCondition (@($batchResults | Where-Object { !$_.Response.IsSuccessful }).Count -eq 0) `
            'At least one batch envelope failed.'
        Assert-LiveTableCount 3
    }

    Invoke-LivePhase 'CreateAndUpdateMultiple' {
        $script:liveState.MultipleIds = @(1..4 | ForEach-Object { [guid]::NewGuid() })
        $createRows = for ($index = 0; $index -lt $script:liveState.MultipleIds.Count; $index++) {
            @{
                $script:liveState.PrimaryId = $script:liveState.MultipleIds[$index]
                $script:liveState.PrimaryName = "multiple-created-$index"
            }
        }
        $null = Invoke-DataverseCreateMultiple -TableSetName $script:liveState.TableSetName `
            -TableLogicalName $script:liveState.LogicalName -Rows $createRows `
            -ChunkSize 4 -MaxDop 1 @connectionParameters -Confirm:$false
        Assert-LiveTableCount 7

        $updateRows = for ($index = 0; $index -lt $script:liveState.MultipleIds.Count; $index++) {
            @{
                $script:liveState.PrimaryId = $script:liveState.MultipleIds[$index]
                $script:liveState.PrimaryName = "multiple-updated-$index"
            }
        }
        $null = Invoke-DataverseUpdateMultiple -TableSetName $script:liveState.TableSetName `
            -TableLogicalName $script:liveState.LogicalName -Rows $updateRows `
            -ChunkSize 4 -MaxDop 1 @connectionParameters -Confirm:$false
        $updated = Get-DataverseRow $script:liveState.TableSetName $script:liveState.MultipleIds[0] @connectionParameters
        Assert-LiveCondition ($updated.($script:liveState.PrimaryName) -eq 'multiple-updated-0') 'UpdateMultiple did not update the row.'
    }

    Invoke-LivePhase 'UpsertMultiple' {
        $newIds = @([guid]::NewGuid(), [guid]::NewGuid())
        $rows = @(
            @{ $script:liveState.PrimaryId = $script:liveState.MultipleIds[0]; $script:liveState.PrimaryName = 'upsert-updated-0' }
            @{ $script:liveState.PrimaryId = $script:liveState.MultipleIds[1]; $script:liveState.PrimaryName = 'upsert-updated-1' }
            @{ $script:liveState.PrimaryId = $newIds[0]; $script:liveState.PrimaryName = 'upsert-created-0' }
            @{ $script:liveState.PrimaryId = $newIds[1]; $script:liveState.PrimaryName = 'upsert-created-1' }
        )
        $null = Invoke-DataverseUpsertMultiple -TableSetName $script:liveState.TableSetName `
            -TableLogicalName $script:liveState.LogicalName -Rows $rows `
            -ChunkSize 4 -MaxDop 1 @connectionParameters -Confirm:$false
        Assert-LiveTableCount 9
        $upserted = Get-DataverseRow $script:liveState.TableSetName $script:liveState.MultipleIds[0] @connectionParameters
        Assert-LiveCondition ($upserted.($script:liveState.PrimaryName) -eq 'upsert-updated-0') 'UpsertMultiple did not update the existing row.'
        $inserted = Get-DataverseRow $script:liveState.TableSetName $newIds[0] @connectionParameters
        Assert-LiveCondition ($inserted.($script:liveState.PrimaryName) -eq 'upsert-created-0') 'UpsertMultiple did not create the new row.'
    }

    Invoke-LivePhase 'ForcedPagination' {
        $request = @{
            Uri = "$($script:liveState.TableSetName)?`$select=$($script:liveState.PrimaryId),$($script:liveState.PrimaryName)&`$orderby=$($script:liveState.PrimaryName)"
            Method = 'GET'
            Headers = @{ Prefer = 'odata.maxpagesize=2' }
        }
        $pages = @($request | Invoke-DataverseRequest -AutoPaginate @connectionParameters)
        $rows = @($pages | ForEach-Object { $_.value })
        Assert-LiveCondition ($pages.Count -ge 2) 'The server did not return multiple pages with odata.maxpagesize=2.'
        Assert-LiveCondition ($rows.Count -eq 9) "Pagination returned $($rows.Count) rows instead of 9."
        Assert-LiveCondition (@($rows.($script:liveState.PrimaryId) | Sort-Object -Unique).Count -eq 9) `
            'Pagination returned duplicate or missing row identifiers.'
    }

    $script:liveState.Results | Add-Member -NotePropertyName ServiceProtectionSignals `
        -NotePropertyValue $script:liveState.ServiceProtectionSignals -PassThru
}
catch {
    $testFailed = $true
    throw
}
finally {
    if ($script:liveState.TableCreated) {
        if ($script:liveState.LogicalName -notmatch '^new_psdvintegration[0-9]{13}$') {
            throw "Refusing to clean up unexpected table '$($script:liveState.LogicalName)'."
        }
        try {
            Write-Verbose "Removing disposable table '$($script:liveState.LogicalName)'."
            Remove-DataverseTable $script:liveState.LogicalName @connectionParameters -Confirm:$false -ErrorAction Stop | Out-Null
            Write-Verbose "Removed disposable table '$($script:liveState.LogicalName)'."
        }
        catch {
            if ($testFailed) {
                Write-Warning "The integration suite failed and cleanup also failed for '$($script:liveState.LogicalName)': $($_.Exception.Message)"
            }
            else {
                throw
            }
        }
    }
}
