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

    if ($outputPath -eq [System.IO.Path]::GetPathRoot($outputPath) -or
        $outputPath -eq [System.IO.Path]::GetFullPath($PSScriptRoot)) {
        throw "Refusing to use unsafe build output path '$outputPath'."
    }

    $managedEntries = @('bin', 'PSFunctions', 'PSDataverse.psd1', 'PSDataverse.psm1')
    if (Test-Path $outputPath) {
        $existingEntries = @(Get-ChildItem -LiteralPath $outputPath -Force)
        $unexpectedEntries = @($existingEntries | Where-Object Name -NotIn $managedEntries)
        if ($unexpectedEntries.Count -ne 0) {
            throw "Build output contains unmanaged entries and cannot be cleaned safely: $($unexpectedEntries.Name -join ', ')."
        }
        if ($existingEntries.Count -ne 0) {
            $existingManifestPath = Join-Path $outputPath 'PSDataverse.psd1'
            if (!(Test-Path -LiteralPath $existingManifestPath -PathType Leaf)) {
                throw "Refusing to clean non-empty output without a PSDataverse manifest: '$outputPath'."
            }
            $existingManifest = Import-PowerShellDataFile -LiteralPath $existingManifestPath
            if ($existingManifest.GUID -ne '081185a0-92be-4624-85e8-4903acb07e03') {
                throw "Refusing to clean output containing a different module: '$outputPath'."
            }
        }
        foreach ($entry in $managedEntries) {
            $managedPath = Join-Path $outputPath $entry
            if (Test-Path -LiteralPath $managedPath) {
                Remove-Item -LiteralPath $managedPath -Recurse -Force
            }
        }
    }

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
