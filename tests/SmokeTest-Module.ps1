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
    'Get-DataverseAttributes'
    'Get-DataverseConnection'
    'Get-DataverseTableRowCount'
    'Send-DataverseOperation'
    'Set-DataverseDefaultConnection'
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

[pscustomobject]@{
    PowerShellVersion = $PSVersionTable.PSVersion.ToString()
    ModulePath = (Resolve-Path $ModulePath).Path
    ExportedCommands = $actual
}
