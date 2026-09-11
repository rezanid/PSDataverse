[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$PackagePath,

    [string]$InstallPath = (Join-Path $PSScriptRoot '../output/package-install'),

    [string]$RepositoryPath = (Join-Path $PSScriptRoot '../output/package-repository'),

    [string]$DependencyPath = (Join-Path $PSScriptRoot '../output/build-modules'),

    [string]$PowerShellPath,

    [switch]$RepositoryWorker
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($PowerShellPath)) {
    $PowerShellPath = @(Get-Command pwsh -CommandType Application `
        -ErrorAction Stop)[0].Source
}
$PowerShellPath = [IO.Path]::GetFullPath($PowerShellPath)
if (!(Test-Path -LiteralPath $PowerShellPath -PathType Leaf)) {
    throw "PowerShell executable '$PowerShellPath' was not found."
}
$packageFile = [IO.Path]::GetFullPath($PackagePath)
$installRoot = [IO.Path]::GetFullPath($InstallPath)
$repositoryRoot = [IO.Path]::GetFullPath($RepositoryPath)
$outputRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../output'))
foreach ($path in $installRoot, $repositoryRoot) {
    if (!$path.StartsWith($outputRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Package test paths must be under '$outputRoot'."
    }
}
if ($installRoot -eq $repositoryRoot -or
    $installRoot.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase) -or
    $repositoryRoot.StartsWith($installRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase) -or
    $packageFile.StartsWith($installRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase) -or
    $packageFile.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Package, repository, and isolated install paths must not contain one another.'
}

$dependencyRoot = [IO.Path]::GetFullPath($DependencyPath)
$resourceGetManifest = Join-Path $dependencyRoot `
    'Microsoft.PowerShell.PSResourceGet/1.2.0/Microsoft.PowerShell.PSResourceGet.psd1'
if (!(Test-Path -LiteralPath $resourceGetManifest -PathType Leaf)) {
    & (Join-Path $PSScriptRoot 'Install-BuildDependencies.ps1') `
        -Destination $dependencyRoot -Name Microsoft.PowerShell.PSResourceGet | Out-Null
}
Import-Module $resourceGetManifest -Force

$archive = [IO.Compression.ZipFile]::OpenRead($packageFile)
try {
    $nuspecEntries = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
    if ($nuspecEntries.Count -ne 1) {
        throw "Expected one nuspec in '$packageFile'; found $($nuspecEntries.Count)."
    }
    $reader = [IO.StreamReader]::new($nuspecEntries[0].Open())
    try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
}
finally {
    $archive.Dispose()
}

$moduleName = [string]$nuspec.package.metadata.id
$packageVersion = [string]$nuspec.package.metadata.version
if ($moduleName -ne 'PSDataverse' -or [string]::IsNullOrWhiteSpace($packageVersion)) {
    throw "The package identity '$moduleName' version '$packageVersion' is invalid."
}

if ($RepositoryWorker) {
    $repositoryName = 'PSDataverseLocal-' + [guid]::NewGuid().ToString('N')
    try {
        Register-PSResourceRepository -Name $repositoryName -Uri $repositoryRoot `
            -ApiVersion Local -Trusted
        Save-PSResource -Name $moduleName -Version $packageVersion -Prerelease `
            -Repository $repositoryName -Path $installRoot -TrustRepository
    }
    finally {
        Unregister-PSResourceRepository -Name $repositoryName `
            -ErrorAction SilentlyContinue
    }
    return
}

if (Test-Path -LiteralPath $installRoot) {
    try {
        Remove-Item -LiteralPath $installRoot -Recurse -Force -ErrorAction Stop
    }
    catch {
        throw "Isolated install '$installRoot' is in use. Close PowerShell sessions that imported it, then run the package test again. $($_.Exception.Message)"
    }
}
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
if (Test-Path -LiteralPath $repositoryRoot) {
    try {
        Remove-Item -LiteralPath $repositoryRoot -Recurse -Force -ErrorAction Stop
    }
    catch {
        throw "Temporary repository '$repositoryRoot' is in use. Close sessions using it, then run the package test again. $($_.Exception.Message)"
    }
}
New-Item -ItemType Directory -Path $repositoryRoot -Force | Out-Null
Copy-Item -LiteralPath $packageFile -Destination $repositoryRoot

& $PowerShellPath -NoLogo -NoProfile -File $PSCommandPath `
    -PackagePath $packageFile -InstallPath $installRoot `
    -RepositoryPath $repositoryRoot -DependencyPath $dependencyRoot `
    -PowerShellPath $PowerShellPath -RepositoryWorker
if ($LASTEXITCODE -ne 0) {
    throw "Local repository acquisition failed with exit code $LASTEXITCODE."
}
try {
    Remove-Item -LiteralPath $repositoryRoot -Recurse -Force -ErrorAction Stop
}
catch {
    throw "Temporary repository '$repositoryRoot' could not be removed. Close sessions using it, then run the package test again. $($_.Exception.Message)"
}

$manifests = @(Get-ChildItem -LiteralPath $installRoot -Filter 'PSDataverse.psd1' `
    -File -Recurse)
if ($manifests.Count -ne 1) {
    throw "Expected one installed PSDataverse manifest; found $($manifests.Count)."
}
$manifest = Test-ModuleManifest -Path $manifests[0].FullName -ErrorAction Stop
$actualVersion = $manifest.Version.ToString()
$actualPrerelease = [string]$manifest.PrivateData.PSData.Prerelease
$actualPackageVersion = if ($actualPrerelease) {
    "$actualVersion-$actualPrerelease"
} else {
    $actualVersion
}
if ($actualPackageVersion -ne $packageVersion) {
    throw "Installed version '$actualPackageVersion' differs from package version '$packageVersion'."
}

$smokeTest = Join-Path $PSScriptRoot '../tests/SmokeTest-Module.ps1'
& $PowerShellPath -NoLogo -NoProfile -File $smokeTest -ModulePath $manifests[0].FullName
if ($LASTEXITCODE -ne 0) {
    throw "The isolated package smoke test failed with exit code $LASTEXITCODE."
}

[pscustomobject]@{
    PackagePath = $packageFile
    PackageVersion = $packageVersion
    InstalledModulePath = $manifests[0].FullName
    PowerShellPath = [IO.Path]::GetFullPath($PowerShellPath)
}
