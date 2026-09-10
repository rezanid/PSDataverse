$functionsPath = Join-Path $PSScriptRoot "PSFunctions"
foreach($file in Get-ChildItem -Path $functionsPath -Filter "*.ps1") {
    . $file
}

Set-Alias -Name Send-DataverseOperation -Value Invoke-DataverseRequest -Scope Local
