[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$ModulePath,

    [string]$SourcePath = (Join-Path $PSScriptRoot '../docs/reference'),

    [string]$OutputPath = (Join-Path $PSScriptRoot '../src/Module/en-US'),

    [string]$FunctionOutputPath = (Join-Path $PSScriptRoot '../src/Module/PSFunctions/en-US'),

    [string]$DependencyPath = (Join-Path $PSScriptRoot '../output/build-modules'),

    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath($SourcePath)
$outputRoot = [IO.Path]::GetFullPath($OutputPath)
$functionOutputRoot = [IO.Path]::GetFullPath($FunctionOutputPath)
$dependencyRoot = [IO.Path]::GetFullPath($DependencyPath)
$platyPSManifest = Join-Path $dependencyRoot 'Microsoft.PowerShell.PlatyPS/1.0.1/Microsoft.PowerShell.PlatyPS.psd1'
if (!(Test-Path -LiteralPath $platyPSManifest -PathType Leaf)) {
    throw "Microsoft.PowerShell.PlatyPS 1.0.1 is missing. Run tools/Install-BuildDependencies.ps1."
}

Import-Module $platyPSManifest -Force
Import-Module ([IO.Path]::GetFullPath($ModulePath)) -Force

$commands = @(Get-Command -Module PSDataverse | Where-Object CommandType -ne Alias | Sort-Object Name)
$topicPaths = @($commands | ForEach-Object {
    $path = Join-Path $sourceRoot "$($_.Name).md"
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing command-help topic: '$path'."
    }
    $path
})
$unexpectedTopics = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*-Dataverse*.md' |
    Where-Object BaseName -notin @($commands.Name + 'Send-DataverseOperation'))
if ($unexpectedTopics.Count) {
    throw "Command-help topics do not match exported commands: $($unexpectedTopics.Name -join ', ')."
}

$invalid = @(Test-MarkdownCommandHelp -LiteralPath $topicPaths | Where-Object { !$_.IsValid })
if ($invalid.Count) {
    $invalid | Format-Table -AutoSize
    throw "$($invalid.Count) command-help topics failed PlatyPS schema validation."
}

$rawTopics = $topicPaths | ForEach-Object { Get-Content -LiteralPath $_ -Raw }
if ($rawTopics -match '\{\{|Fill in|Add example|Insert list') {
    throw 'Command help contains an unresolved template placeholder.'
}

$help = @($topicPaths | Import-MarkdownCommandHelp -Path { $_ })
$helpByName = @{}
foreach ($topic in $help) { $helpByName[$topic.Title] = $topic }
foreach ($command in $commands) {
    $topic = $helpByName[$command.Name]
    if ($null -eq $topic) { throw "No imported help topic exists for '$($command.Name)'." }
    if ([string]::IsNullOrWhiteSpace($topic.Synopsis) -or [string]::IsNullOrWhiteSpace($topic.Description)) {
        throw "Help for '$($command.Name)' requires a synopsis and description."
    }
    if ($topic.Examples.Count -eq 0) { throw "Help for '$($command.Name)' requires at least one example." }

    $runtimeSyntax = @((New-CommandHelp -CommandInfo $command).Syntax | ForEach-Object ToString)
    $documentedSyntax = @($topic.Syntax | ForEach-Object ToString)
    if (Compare-Object $runtimeSyntax $documentedSyntax) {
        throw "Documented syntax for '$($command.Name)' differs from the packaged command metadata."
    }
    $runtimeParameters = @($command.Parameters.Keys |
        Where-Object { $_ -notin [System.Management.Automation.Cmdlet]::CommonParameters } |
        Sort-Object)
    $documentedParameters = @($topic.Parameters.Name | Sort-Object)
    if (Compare-Object $runtimeParameters $documentedParameters) {
        throw "Documented parameters for '$($command.Name)' differ from the packaged command metadata."
    }
}

$stageRoot = Join-Path $PSScriptRoot '../output/help-build'
$stageRoot = [IO.Path]::GetFullPath($stageRoot)
$expectedStageRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../output'))
if (!$stageRoot.StartsWith($expectedStageRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe help staging path '$stageRoot'."
}
if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
$help | Export-MamlCommandHelp -OutputFolder $stageRoot -Encoding utf8 -Force -Confirm:$false | Out-Null
$helpFiles = @(
    [pscustomobject]@{
        FileName = 'PSDataverse.dll-Help.xml'
        TargetPath = Join-Path $outputRoot 'PSDataverse.dll-Help.xml'
    }
    [pscustomobject]@{
        FileName = 'PSDataverse.PowerShell-Help.xml'
        TargetPath = Join-Path $functionOutputRoot 'PSDataverse.PowerShell-Help.xml'
    }
)
foreach ($helpFile in $helpFiles) {
    $helpFile | Add-Member GeneratedPath (Join-Path $stageRoot "PSDataverse/$($helpFile.FileName)")
    if (!(Test-Path -LiteralPath $helpFile.GeneratedPath -PathType Leaf)) {
        throw "PlatyPS did not generate '$($helpFile.GeneratedPath)'."
    }
    $normalized = ([IO.File]::ReadAllText($helpFile.GeneratedPath) -replace "`r`n", "`n").TrimEnd() + "`n"
    $helpFile | Add-Member GeneratedContent $normalized
}

$index = [Collections.Generic.List[string]]::new()
$index.Add('# PSDataverse command reference')
$index.Add('')
$index.Add('These Markdown topics are the source for the packaged `Get-Help` content.')
$index.Add('')
foreach ($command in $commands) {
    $topic = $helpByName[$command.Name]
    $index.Add("- [$($command.Name)]($($command.Name).md) — $($topic.Synopsis)")
}
$index.Add('- [Send-DataverseOperation](Send-DataverseOperation.md) — Compatibility alias for `Invoke-DataverseRequest`.')
$generatedIndex = ($index -join "`n") + "`n"
$targetIndexPath = Join-Path $sourceRoot 'README.md'

if ($Check) {
    foreach ($helpFile in $helpFiles) {
        if (!(Test-Path -LiteralPath $helpFile.TargetPath -PathType Leaf)) {
            throw "Generated MAML is missing: '$($helpFile.TargetPath)'."
        }
        $existingMaml = ([IO.File]::ReadAllText($helpFile.TargetPath) -replace "`r`n", "`n")
        if ($existingMaml -cne $helpFile.GeneratedContent) {
            throw "Generated MAML '$($helpFile.FileName)' differs from the committed help. Run tools/Build-CommandHelp.ps1 without -Check."
        }
    }
    $existingIndex = ([IO.File]::ReadAllText($targetIndexPath) -replace "`r`n", "`n")
    if ($existingIndex -cne $generatedIndex) {
        throw 'The command-reference index is stale. Run tools/Build-CommandHelp.ps1 without -Check.'
    }
} else {
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $functionOutputRoot -Force | Out-Null
    foreach ($helpFile in $helpFiles) {
        [IO.File]::WriteAllText(
            $helpFile.TargetPath,
            $helpFile.GeneratedContent,
            [Text.UTF8Encoding]::new($false))
    }
    [IO.File]::WriteAllText($targetIndexPath, $generatedIndex, [Text.UTF8Encoding]::new($false))
}

$helpFiles | ForEach-Object { Get-Item -LiteralPath $_.TargetPath }
