[CmdletBinding()]
param(
    [string]$Uri = 'WhoAmI',
    [ValidateRange(1, 10000)][int]$Count = 100,
    [ValidateNotNullOrEmpty()][int[]]$MaxDop = @(1, 4, 8, 16, 32),
    [string]$ConnectionName
)

$ErrorActionPreference = 'Stop'
$connectionParameters = @{}
if ($ConnectionName) { $connectionParameters.ConnectionName = $ConnectionName }

# Warm authentication, token refresh, HTTP connection establishment, and JIT costs before measuring.
$null = Invoke-DataverseRequest -Uri $Uri -MaxDop 1 @connectionParameters

foreach ($dop in $MaxDop) {
    if ($dop -lt 1 -or $dop -gt 1024) { throw "MaxDop value '$dop' must be between 1 and 1024." }
    $operations = 1..$Count | ForEach-Object {
        @{ ContentId = $_.ToString(); Method = 'GET'; Uri = $Uri }
    }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $responses = @($operations | Invoke-DataverseRequest -MaxDop $dop @connectionParameters)
    $timer.Stop()
    [pscustomobject]@{
        Scenario = 'ParallelIndividualRequests'
        Uri = $Uri
        RequestedMaxDop = $dop
        RequestCount = $Count
        ResponseCount = $responses.Count
        Elapsed = $timer.Elapsed
        RequestsPerSecond = [math]::Round($Count / $timer.Elapsed.TotalSeconds, 2)
        AllSuccessful = @($responses | Where-Object { !$_.StatusCode -or [int]$_.StatusCode -notin 200..299 }).Count -eq 0
    }
}
