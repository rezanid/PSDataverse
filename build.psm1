function Start-PSDataverseBuild {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Output,

        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = 'Release'
    )

    $outputPath = [System.IO.Path]::GetFullPath($Output)
    $binPath = Join-Path $outputPath 'bin'
    $modulePath = Join-Path $PSScriptRoot 'src\Module'
    $projectPath = Join-Path $PSScriptRoot 'src\PSDataverse\PSDataverse.csproj'

    New-Item -Path $outputPath -ItemType Directory -Force | Out-Null
    New-Item -Path $binPath -ItemType Directory -Force | Out-Null

    dotnet build $projectPath --configuration $Configuration --output $binPath
    if ($LASTEXITCODE -ne 0) {
        throw "PSDataverse build failed with exit code $LASTEXITCODE."
    }

    Copy-Item (Join-Path $modulePath 'PSDataverse.psd1') $outputPath -Force
    Copy-Item (Join-Path $modulePath 'PSDataverse.psm1') $outputPath -Force
    Copy-Item (Join-Path $modulePath 'PSFunctions') $outputPath -Recurse -Force

    Get-Item $outputPath
}
