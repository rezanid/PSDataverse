[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '../output/packages'),

    [string]$BuildPath = (Join-Path $PSScriptRoot '../output/package-build'),

    [string]$DependencyPath = (Join-Path $PSScriptRoot '../output/build-modules'),

    [switch]$SkipModuleTests,

    [switch]$SkipInstallTest
)

$ErrorActionPreference = 'Stop'
$outputRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../output'))
$packageRoot = [IO.Path]::GetFullPath($OutputPath)
$buildRoot = [IO.Path]::GetFullPath($BuildPath)
foreach ($path in $packageRoot, $buildRoot) {
    if (!$path.StartsWith($outputRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Package output and staging paths must be under '$outputRoot'."
    }
}
if ($packageRoot -eq $buildRoot -or
    $packageRoot.StartsWith($buildRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase) -or
    $buildRoot.StartsWith($packageRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Package output and staging paths must not contain one another.'
}

$sourceManifestPath = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '../src/Module/PSDataverse.psd1'))
$manifest = Test-ModuleManifest -Path $sourceManifestPath -ErrorAction Stop
$prerelease = [string]$manifest.PrivateData.PSData.Prerelease
if ([string]::IsNullOrWhiteSpace($prerelease)) {
    throw 'The PSDataverse 2 package must have explicit prerelease metadata.'
}
$packageVersion = "$($manifest.Version)-$prerelease"
$packageFileName = "PSDataverse.$packageVersion.nupkg"

$dependencyRoot = [IO.Path]::GetFullPath($DependencyPath)
$resourceGetManifest = Join-Path $dependencyRoot `
    'Microsoft.PowerShell.PSResourceGet/1.2.0/Microsoft.PowerShell.PSResourceGet.psd1'
if (!(Test-Path -LiteralPath $resourceGetManifest -PathType Leaf)) {
    & (Join-Path $PSScriptRoot 'Install-BuildDependencies.ps1') `
        -Destination $dependencyRoot | Out-Null
}
Import-Module $resourceGetManifest -Force
Import-Module (Join-Path $PSScriptRoot '../build.psm1') -Force

if (Test-Path -LiteralPath $buildRoot) {
    try {
        Remove-Item -LiteralPath $buildRoot -Recurse -Force -ErrorAction Stop
    }
    catch {
        throw "Package staging '$buildRoot' is in use. Close PowerShell sessions that imported a staged copy of PSDataverse, then run packaging again. $($_.Exception.Message)"
    }
}
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

$modulePaths = @(
    (Join-Path $buildRoot 'first/PSDataverse')
    (Join-Path $buildRoot 'second/PSDataverse')
)
foreach ($modulePath in $modulePaths) {
    Start-PSDataverseBuild -Output $modulePath -Configuration Release | Out-Null
}

function Get-ModuleFileInventory {
    param([Parameter(Mandatory)][string]$Path)

    $root = [IO.Path]::GetFullPath($Path).TrimEnd(
        [IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object {
        $relativePath = $_.FullName.Substring($root.Length + 1).Replace('\', '/')
        "$relativePath|$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
    } | Sort-Object
}

$firstInventory = @(Get-ModuleFileInventory $modulePaths[0])
$secondInventory = @(Get-ModuleFileInventory $modulePaths[1])
$inventoryDifference = @(Compare-Object $firstInventory $secondInventory)
if ($inventoryDifference.Count) {
    throw "Two clean Release builds produced different module trees:`n$($inventoryDifference | Out-String)"
}

if (!$SkipModuleTests) {
    & (Join-Path $PSScriptRoot 'Test-PSDataverseModule.ps1') `
        -ModulePath (Join-Path $modulePaths[0] 'PSDataverse.psd1') `
        -DependencyPath $dependencyRoot
}

function Export-CanonicalPackage {
    param(
        [Parameter(Mandatory)][string]$ModulePath,
        [Parameter(Mandatory)][string]$StagePath,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    New-Item -ItemType Directory -Path $StagePath -Force | Out-Null
    $repositoryName = 'PSDataversePack-' + [guid]::NewGuid().ToString('N')
    try {
        Register-PSResourceRepository -Name $repositoryName -Uri $StagePath `
            -ApiVersion Local -Trusted
        Publish-PSResource -Path $ModulePath -Repository $repositoryName `
            -SkipDependenciesCheck
    }
    finally {
        Unregister-PSResourceRepository -Name $repositoryName `
            -ErrorAction SilentlyContinue
    }
    $rawPackages = @(Get-ChildItem -LiteralPath $StagePath -Filter '*.nupkg' -File)
    if ($rawPackages.Count -ne 1) {
        throw "Expected one staged package in '$StagePath'; found $($rawPackages.Count)."
    }

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }
    $sourceArchive = [IO.Compression.ZipFile]::OpenRead($rawPackages[0].FullName)
    $destinationStream = [IO.File]::Open(
        $DestinationPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite)
    $destinationArchive = [IO.Compression.ZipArchive]::new(
        $destinationStream, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        $fixedTimestamp = [datetimeoffset]'2000-01-01T00:00:00Z'
        $canonicalEntries = @($sourceArchive.Entries | ForEach-Object {
            $name = if ($_.FullName -like 'package/services/metadata/core-properties/*.psmdcp') {
                'package/services/metadata/core-properties/PSDataverse.psmdcp'
            } else {
                $_.FullName
            }
            [pscustomobject]@{ Source = $_; Name = $name }
        } | Sort-Object Name)
        foreach ($canonicalEntry in $canonicalEntries) {
            $entry = $canonicalEntry.Source
            $newEntry = $destinationArchive.CreateEntry(
                $canonicalEntry.Name, [IO.Compression.CompressionLevel]::Optimal)
            $newEntry.LastWriteTime = $fixedTimestamp
            $newEntry.ExternalAttributes = 0
            if (!$entry.FullName.EndsWith('/')) {
                $inputStream = $entry.Open()
                $outputStream = $newEntry.Open()
                try {
                    if ($entry.FullName -eq '_rels/.rels') {
                        $reader = [IO.StreamReader]::new($inputStream)
                        try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
                        $content = [regex]::Replace(
                            $content,
                            '(<Relationship Type="[^"]+/metadata/core-properties" Target=")[^"]+(" Id=")[^"]+(" />)',
                            '$1/package/services/metadata/core-properties/PSDataverse.psmdcp$2RPSDataverseCoreProperties$3')
                        $bytes = [Text.UTF8Encoding]::new($false).GetBytes(
                            ($content -replace "`r`n", "`n"))
                        $outputStream.Write($bytes, 0, $bytes.Length)
                    }
                    elseif ($entry.FullName -eq '[Content_Types].xml') {
                        $reader = [IO.StreamReader]::new($inputStream)
                        try { [xml]$document = $reader.ReadToEnd() } finally { $reader.Dispose() }
                        $root = $document.DocumentElement
                        $defaultNodes = @($root.ChildNodes | Where-Object LocalName -eq 'Default')
                        foreach ($node in $defaultNodes) { [void]$root.RemoveChild($node) }
                        foreach ($node in @($defaultNodes | Sort-Object Extension)) {
                            [void]$root.AppendChild($node)
                        }
                        $settings = [Xml.XmlWriterSettings]@{
                            Encoding = [Text.UTF8Encoding]::new($false)
                            Indent = $true
                            NewLineChars = "`n"
                            NewLineHandling = [Xml.NewLineHandling]::Replace
                        }
                        $writer = [Xml.XmlWriter]::Create($outputStream, $settings)
                        try { $document.Save($writer) } finally { $writer.Dispose() }
                    }
                    else {
                        $inputStream.CopyTo($outputStream)
                    }
                }
                finally {
                    $outputStream.Dispose()
                    $inputStream.Dispose()
                }
            }
        }
    }
    finally {
        $destinationArchive.Dispose()
        $destinationStream.Dispose()
        $sourceArchive.Dispose()
    }
}

$candidatePaths = @(
    (Join-Path $buildRoot "first/$packageFileName")
    (Join-Path $buildRoot "second/$packageFileName")
)
for ($index = 0; $index -lt 2; $index++) {
    Export-CanonicalPackage -ModulePath $modulePaths[$index] `
        -StagePath (Join-Path $buildRoot "raw-$index") `
        -DestinationPath $candidatePaths[$index]
}

$candidateHashes = @($candidatePaths | ForEach-Object {
    (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
})
if ($candidateHashes[0] -ne $candidateHashes[1]) {
    throw 'Two clean Release builds produced different canonical package hashes.'
}

$finalPackagePath = Join-Path $packageRoot $packageFileName
Copy-Item -LiteralPath $candidatePaths[0] -Destination $finalPackagePath -Force
$finalHash = (Get-FileHash -LiteralPath $finalPackagePath -Algorithm SHA256).Hash
if ($finalHash -ne $candidateHashes[0]) {
    throw 'The retained package differs from the verified package candidate.'
}
$hashPath = "$finalPackagePath.sha256"
$hashLine = "$($candidateHashes[0].ToLowerInvariant())  $packageFileName`n"
[IO.File]::WriteAllText($hashPath, $hashLine, [Text.UTF8Encoding]::new($false))

$installResult = $null
if (!$SkipInstallTest) {
    $installResult = & (Join-Path $PSScriptRoot 'Test-PSDataversePackage.ps1') `
        -PackagePath $finalPackagePath -DependencyPath $dependencyRoot
}

[pscustomobject]@{
    Name = 'PSDataverse'
    Version = $packageVersion
    PackagePath = $finalPackagePath
    Sha256 = $candidateHashes[0]
    ModuleFileCount = $firstInventory.Count
    InstallTested = !$SkipInstallTest
    InstalledModulePath = $installResult.InstalledModulePath
}
