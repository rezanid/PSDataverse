$script:PathSeparator = [System.IO.Path]::PathSeparator
$script:ModuleName = "PSDataverse"

Push-Location $PSScriptRoot

function Start-PSDataverseBuild {
    param(
        [string]$Output
    )
    Write-Output $Output
    dotnet build -o (Join-Path $Output bin)
    Copy-Item "$PSScriptRoot\src\Module\*" $Output -Recurse -Force
}
