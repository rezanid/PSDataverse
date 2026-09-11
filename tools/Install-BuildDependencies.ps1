[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $PSScriptRoot '../output/build-modules'),

    [ValidateSet(
        'Pester',
        'PSScriptAnalyzer',
        'Microsoft.PowerShell.PlatyPS',
        'Microsoft.PowerShell.PSResourceGet')]
    [string[]]$Name
)

$ErrorActionPreference = 'Stop'
$destinationPath = [System.IO.Path]::GetFullPath($Destination)
$dependencies = @(
    @{ Name = 'Pester'; Version = '6.2.0' }
    @{ Name = 'PSScriptAnalyzer'; Version = '1.25.0' }
    @{ Name = 'Microsoft.PowerShell.PlatyPS'; Version = '1.0.1' }
    @{ Name = 'Microsoft.PowerShell.PSResourceGet'; Version = '1.2.0' }
)
if ($Name) {
    $dependencies = @($dependencies | Where-Object Name -In $Name)
}

foreach ($dependency in $dependencies) {
    $modulePath = Join-Path $destinationPath $dependency.Name
    $versionPath = Join-Path $modulePath $dependency.Version
    $manifest = Join-Path $versionPath "$($dependency.Name).psd1"
    if (!(Test-Path -LiteralPath $manifest -PathType Leaf)) {
        Save-Module -Name $dependency.Name -RequiredVersion $dependency.Version `
            -Repository PSGallery -Path $destinationPath -Force
    }
    Get-Item -LiteralPath $manifest
}
