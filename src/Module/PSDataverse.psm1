$functionsPath = Join-Path $PSScriptRoot "PSFunctions"
foreach($file in Get-ChildItem -Path $functionsPath -Filter "*.ps1") {
    . $file
}
