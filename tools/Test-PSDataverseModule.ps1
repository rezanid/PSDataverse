[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$ModulePath,

    [switch]$CI,

    [string]$DependencyPath = (Join-Path $PSScriptRoot '../output/build-modules')
)

$ErrorActionPreference = 'Stop'
$dependencyRoot = [System.IO.Path]::GetFullPath($DependencyPath)
$pesterManifest = Join-Path $dependencyRoot 'Pester/6.2.0/Pester.psd1'
$analyzerManifest = Join-Path $dependencyRoot 'PSScriptAnalyzer/1.25.0/PSScriptAnalyzer.psd1'
$platyPSManifest = Join-Path $dependencyRoot 'Microsoft.PowerShell.PlatyPS/1.0.1/Microsoft.PowerShell.PlatyPS.psd1'

if (!(Test-Path -LiteralPath $pesterManifest) -or
    !(Test-Path -LiteralPath $analyzerManifest) -or
    !(Test-Path -LiteralPath $platyPSManifest)) {
    & (Join-Path $PSScriptRoot 'Install-BuildDependencies.ps1') -Destination $dependencyRoot | Out-Null
}

Import-Module $pesterManifest -Force
Import-Module $analyzerManifest -Force

$analysisPaths = @(
    (Join-Path $PSScriptRoot '../src/Module')
    $PSScriptRoot
    (Join-Path $PSScriptRoot '../build.psm1')
)
$issues = @($analysisPaths | ForEach-Object {
    Invoke-ScriptAnalyzer -Path $_ -Recurse -Severity Error
})
if ($issues.Count -ne 0) {
    $issues | Format-Table -AutoSize
    throw "$($issues.Count) PowerShell analyzer errors found."
}

& (Join-Path $PSScriptRoot 'Build-CommandHelp.ps1') `
    -ModulePath $ModulePath -DependencyPath $dependencyRoot -Check | Out-Null

$testPath = Join-Path $PSScriptRoot '../tests/PSDataverse.Module.Tests.ps1'
$container = New-PesterContainer -Path $testPath -Data @{
    ModulePath = [System.IO.Path]::GetFullPath($ModulePath)
}
$configuration = New-PesterConfiguration
$configuration.Run.Container = $container
$configuration.Run.PassThru = $true
$configuration.Output.CIFormat = if ($CI) { 'GithubActions' } else { 'None' }
$result = Invoke-Pester -Configuration $configuration
if ($result.FailedCount -ne 0) {
    throw "$($result.FailedCount) packaged-module tests failed."
}
