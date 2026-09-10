[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$ModulePath,
    [string]$OutputPath = (Join-Path $PSScriptRoot '../docs/reference')
)

$ErrorActionPreference = 'Stop'
$referencePath = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path $referencePath -Force | Out-Null
Import-Module ([IO.Path]::GetFullPath($ModulePath)) -Force

$commands = @(Get-Command -Module PSDataverse | Sort-Object Name)
$index = @('# PSDataverse command reference', '', 'Generated from the packaged module command metadata.', '')
foreach ($command in $commands) {
    $index += "- [$($command.Name)]($($command.Name).md)"
    $lines = @("# $($command.Name)", '', '## Syntax', '')
    foreach ($set in $command.ParameterSets) {
        $syntax = $command.Name
        foreach ($parameter in $set.Parameters | Sort-Object Position, Name) {
            if ($parameter.Name -in [System.Management.Automation.Cmdlet]::CommonParameters) { continue }
            $token = if ($parameter.ParameterType -eq [switch]) {
                "-$($parameter.Name)"
            } else {
                "-$($parameter.Name) <$($parameter.ParameterType.Name)>"
            }
            $syntax += if ($parameter.IsMandatory) { " $token" } else { " [$token]" }
        }
        $lines += '```powershell', $syntax, '```', ''
    }
    $lines += '## Parameters', '', '| Name | Type | Required | Pipeline | Aliases |', '|---|---|---:|---:|---|'
    foreach ($parameter in $command.Parameters.Values | Sort-Object Name) {
        if ($parameter.Name -in [System.Management.Automation.Cmdlet]::CommonParameters) { continue }
        $attributes = @($parameter.Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] })
        $required = if ($attributes | Where-Object Mandatory) { 'Yes' } else { 'No' }
        $pipeline = if ($attributes | Where-Object { $_.ValueFromPipeline -or $_.ValueFromPipelineByPropertyName }) { 'Yes' } else { 'No' }
        $aliases = ($parameter.Aliases -join ', ')
        $typeName = $parameter.ParameterType.Name.Replace('`', '&#96;')
        $lines += "| ``-$($parameter.Name)`` | ``$typeName`` | $required | $pipeline | $aliases |"
    }
    Set-Content -LiteralPath (Join-Path $referencePath "$($command.Name).md") -Value $lines -Encoding utf8
}
Set-Content -LiteralPath (Join-Path $referencePath 'README.md') -Value $index -Encoding utf8
