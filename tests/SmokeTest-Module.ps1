[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path $_ -PathType Leaf })]
    [string]$ModulePath
)

$ErrorActionPreference = 'Stop'
$expected = @(
    'Clear-DataverseTable'
    'Connect-Dataverse'
    'Disconnect-Dataverse'
    'Export-DataverseOptionSet'
    'Export-DataverseRows'
    'Get-DataverseAttributes'
    'Get-DataverseConnection'
    'Get-DataverseRow'
    'Get-DataverseTableMetadata'
    'Get-DataverseTableRowCount'
    'Import-DataverseRows'
    'Invoke-DataverseAction'
    'Invoke-DataverseCreateMultiple'
    'Invoke-DataverseFunction'
    'Invoke-DataverseRequest'
    'Invoke-DataverseUpdateMultiple'
    'Invoke-DataverseUpsertMultiple'
    'New-DataverseRow'
    'New-DataverseTable'
    'Remove-DataverseRow'
    'Remove-DataverseTable'
    'Send-DataverseOperation'
    'Set-DataverseDefaultConnection'
    'Set-DataverseRow'
    'Test-DataverseBulkOperationSupport'
    'Test-DataverseConnection'
)

Import-Module $ModulePath -Force
$actual = @(Get-Command -Module PSDataverse | Sort-Object Name | Select-Object -ExpandProperty Name)
$difference = @(Compare-Object $expected $actual)
if ($difference.Count -ne 0) {
    throw "The packaged command surface does not match the manifest: $($difference | Out-String)"
}

$moduleRoot = Split-Path $ModulePath -Parent
if (Test-Path (Join-Path $moduleRoot 'bin/Scriban.dll')) {
    throw 'Scriban.dll must not be present in the PSDataverse package.'
}

$helpPath = Join-Path $moduleRoot 'en-US/PSDataverse.dll-Help.xml'
if (-not (Test-Path $helpPath -PathType Leaf)) {
    throw 'The packaged binary-command help file is missing.'
}

$functionHelpPath = Join-Path $moduleRoot 'PSFunctions/en-US/PSDataverse.PowerShell-Help.xml'
if (-not (Test-Path $functionHelpPath -PathType Leaf)) {
    throw 'The packaged PowerShell-function help file is missing.'
}

foreach ($commandName in 'Connect-Dataverse', 'Test-DataverseConnection') {
    $help = Get-Help $commandName -Full
    if ([string]::IsNullOrWhiteSpace([string]$help.Synopsis) -or
        [string]::IsNullOrWhiteSpace([string]$help.Description.Text)) {
        throw "Packaged command help did not load for $commandName."
    }
}

if (@(Get-ChildItem $moduleRoot -Recurse -File | Where-Object Name -Match 'PlatyPS').Count -ne 0) {
    throw 'Microsoft.PowerShell.PlatyPS must remain a build-only dependency.'
}

[pscustomobject]@{
    PowerShellVersion = $PSVersionTable.PSVersion.ToString()
    ModulePath = (Resolve-Path $ModulePath).Path
    ExportedCommands = $actual
}
