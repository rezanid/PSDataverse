# PSDataverse

![PSDataverse logo](media/PSDataverse-Logo.png)

[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/PSDataverse?include_prereleases&label=PowerShell%20Gallery)](https://www.powershellgallery.com/packages/PSDataverse)
[![Downloads](https://img.shields.io/powershellgallery/dt/PSDataverse)](https://www.powershellgallery.com/packages/PSDataverse)
[![Build and test](https://github.com/rezanid/PSDataverse/actions/workflows/build-and-test.yml/badge.svg?branch=main)](https://github.com/rezanid/PSDataverse/actions/workflows/build-and-test.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

High-performance, pipeline-friendly PowerShell access to Microsoft Dataverse.

> [!IMPORTANT]
> PSDataverse `2.0.0-rc1` is a public release candidate requiring PowerShell
> 7.6 or later. It contains intentional breaking changes from 0.x; review the
> [migration guide](MIGRATION.md) before upgrading production automation.

See [what is new in PSDataverse 2.0.0-rc1](docs/releases/2.0.0-rc1.md), the
[complete command reference](docs/reference/README.md), and the
[measured write-transport guide](docs/guides/choosing-a-write-transport.md).

## Table of Contents
* [What is PSDataverse](#what-is-psdataverse)
* [Features](#features)
* [How to install](#how-to-install)
* [How to use](#how-to-use)
  * [Connecting to Dataverse](#connecting-to-dataverse)
  * [Sending operations to Dataverse](#sending-operations-to-dataverse)

# What is PSDataverse?
PSDataverse brings Microsoft Dataverse's Web API to PowerShell with secure
authentication, first-class connection objects, controlled concurrency, batching,
bulk operations, and commands designed for composition in PowerShell pipelines.

The [command reference](docs/reference/README.md) is the source for the detailed help packaged with the module and lists its syntax, examples, pipeline support, and aliases.
For write-heavy workloads, see [choosing a write transport](docs/guides/choosing-a-write-transport.md).

# Features
* Authenticate with WAM, system browser, device code, IWA, client secrets,
  certificates, access tokens, token providers, or compatible connection strings.
* Keep named Dataverse connections and choose an explicit default connection.
* Run individual requests through a bounded, cancellation-aware parallel scheduler.
* Send concurrent `$batch` envelopes or CreateMultiple, UpdateMultiple, and
  UpsertMultiple workloads with explicit chunking and degree of parallelism.
* Use pipeline-friendly CRUD, metadata, action/function, import, and export commands.
* Honor Dataverse concurrency hints and transient retry guidance while avoiding
  unsafe automatic replay of ambiguous writes.
* Diagnose failed batch and multiple-operation rows without hiding Dataverse's
  original response.
* Run the same verified package on Windows, Linux, and macOS.

# How to install

Install the PSDataverse 2 release candidate from
[PowerShell Gallery](https://www.powershellgallery.com/packages/PSDataverse) with
PSResourceGet:

```powershell
Install-PSResource -Name PSDataverse -Version 2.0.0-rc1 -Prerelease
```

Or use PowerShellGet:

```powershell
Install-Module -Name PSDataverse -RequiredVersion 2.0.0-rc1 -AllowPrerelease
```

The following command continues to install the latest stable 0.x release until
PSDataverse 2 reaches general availability:

```powershell
Install-Module -Name PSDataverse
```

To build the latest source locally, install PowerShell 7.6 or later and the .NET 10 SDK, then run:

```powershell
git clone https://github.com/rezanid/PSDataverse.git
Set-Location ./PSDataverse

Import-Module ./build.psm1 -Force
Start-PSDataverseBuild -Output ./output/PSDataverse

Import-Module ./output/PSDataverse/PSDataverse.psd1 -Force
Get-Command -Module PSDataverse
```

If you downloaded a prebuilt module package instead, extract it and import its `PSDataverse.psd1` manifest.

> **NOTE!**
> PSDataverse is a hybrid module that is a mix of PSDataverse.dll and PSDataverse.psd1 module definition. Only the commands that made more sense to be implemented as binary are included in the dll, and the rest of the implementation is done using PowerShell language.

## Developing and testing

Build and test dependencies are pinned and installed under the ignored `output` directory, so a machine-wide Pester installation is not used:

```powershell
Import-Module ./build.psm1 -Force
Start-PSDataverseBuild -Output ./output/PSDataverse
./tools/Install-BuildDependencies.ps1
./tools/Test-PSDataverseModule.ps1 -ModulePath ./output/PSDataverse/PSDataverse.psd1
dotnet test PSDataverse.sln
```

When command metadata or reference documentation changes, regenerate the packaged
help, rebuild, and validate it:

```powershell
./tools/Build-CommandHelp.ps1 `
    -ModulePath ./output/PSDataverse/PSDataverse.psd1
Start-PSDataverseBuild -Output ./output/PSDataverse
./tools/Test-PSDataverseModule.ps1 `
    -ModulePath ./output/PSDataverse/PSDataverse.psd1
```

The help generator uses the pinned build-only PlatyPS dependency under `output`.
The published module contains only the generated MAML help, not PlatyPS itself.

## Building a prerelease package

The packaging command performs two clean Release builds, compares every module
file, creates canonical PowerShell packages, compares their SHA-256 hashes, and
acquires the result from a temporary local repository before smoke-testing the
isolated copy:

```powershell
./tools/New-PSDataversePackage.ps1
```

The verified `.nupkg` and its `.sha256` sidecar are written to `output/packages`.
To repeat only the repository acquisition and package smoke test:

```powershell
./tools/Test-PSDataversePackage.ps1 `
    -PackagePath ./output/packages/PSDataverse.2.0.0-rc1.nupkg
```

The scripts use pinned build dependencies and do not install PSDataverse into the
machine-wide or current-user module locations. Pull requests and manual releases use
the same reusable verification workflow: it builds one package and installs that
exact artifact on Windows, Linux, and macOS with PowerShell 7.6. The release workflow
can publish the validated file only when its `publish` input is explicitly enabled.
Publishing is additionally restricted to runs started from the `main` branch.

The opt-in live integration suite creates and removes a uniquely named disposable
table. Import the built module, connect using any supported authentication flow, and
pass that connection explicitly:

```powershell
Import-Module ./output/PSDataverse/PSDataverse.psd1 -Force
$connection = Connect-Dataverse https://<environment>.crm.dynamics.com -Interactive
./tests/Invoke-PSDataverseLiveIntegration.ps1 -Connection $connection -Confirm:$false
```

The suite covers convenience CRUD commands, forced pagination, concurrent `$batch`
envelopes, CreateMultiple, UpdateMultiple, and UpsertMultiple. It always attempts to
remove its disposable table. Throttling retries are tested deterministically in the
.NET transport tests; the live suite records Dataverse service-protection hints but
does not intentionally overload the environment to produce HTTP 429 responses.

If the build reports that its output is in use, close every PowerShell session that imported that copy of PSDataverse and rerun it.

# How to use
Start by connecting to a Dataverse environment with `Connect-Dataverse`. PSDataverse supports browser or broker interaction, Integrated Windows Authentication, device code, application credentials, supplied tokens, token providers, and legacy connection strings.

## Connecting to Dataverse

**Interactive authentication** uses Windows Web Account Manager when available and the system browser on other platforms. Passkeys and other passwordless methods are presented by that sign-in experience.

```powershell
$connection = Connect-Dataverse https://<environment>.crm.dynamics.com -Interactive
```

On Windows, the preceding command uses Web Account Manager (WAM) by default. You can state that choice explicitly with `-UseWebAccountManager` (alias `-UseWam`), or opt out with `-UseSystemBrowser`.

Interactive authentication normally reuses a suitable cached identity. Add `-ForceAuthentication` to display the account chooser when the wrong account is cached or when you need to change identity:

```powershell
$connection = Connect-Dataverse https://<environment>.crm.dynamics.com `
  -Interactive -UseWam -ForceAuthentication
```

**Device-code authentication** is convenient for remote terminals and hosts without a browser.

```powershell
Connect-Dataverse https://<environment>.crm.dynamics.com -DeviceCode -InformationAction Continue
```

An explicit `-DeviceCode` connection always displays a new device code. Later token refreshes remain silent while MSAL can refresh the selected identity.

> [!NOTE]
> Traditional Integrated Windows Authentication is retained for federated, Active Directory-backed users, but it is deprecated by Microsoft and does not support managed Entra-only users. Prefer `-Interactive`, which uses WAM on Windows.

**Application authentication** accepts a secure client secret or a certificate from the operating-system certificate store.

```powershell
Connect-Dataverse https://<environment>.crm.dynamics.com `
  -ClientId <application-id> -TenantId <tenant-id> `
  -ClientSecret (Read-Host 'Client secret' -AsSecureString)

Connect-Dataverse https://<environment>.crm.dynamics.com `
  -ClientId <application-id> -TenantId <tenant-id> `
  -CertificateThumbprint <certificate-thumbprint>
```

> [!NOTE]
> Application authentication requires an application user in the Power Platform environment. See [Manage application users in the Power Platform admin center](https://learn.microsoft.com/en-us/power-platform/admin/manage-application-users).

Connections can be named when a script works with multiple environments:

```powershell
$dev = Connect-Dataverse $devUrl -Interactive -Name dev
Connect-Dataverse $testUrl -Interactive -Name test -NoDefault

Get-DataverseConnection
Send-DataverseOperation WhoAmI -ConnectionName test
Set-DataverseDefaultConnection test
Disconnect-Dataverse -All
```

The latest default connection is used when `-Connection` and `-ConnectionName` are omitted. PSDataverse refreshes renewable credentials shortly before expiry and serializes concurrent refresh attempts. Legacy connection strings remain available for migration; see [Migrating from PSDataverse 0.x](MIGRATION.md) for every parameter set and compatibility alias.

## Sending operations to Dataverse

`Invoke-DataverseRequest` is the preferred low-level command. The familiar `Send-DataverseOperation` name remains available as a compatibility alias.

The concise form performs a GET:

```powershell
Invoke-DataverseRequest WhoAmI
```

Methods, bodies, and headers can be supplied directly:

```powershell
Invoke-DataverseRequest -Uri accounts -Method POST `
    -Body @{ name = 'Contoso' } `
    -Headers @{ Prefer = 'return=representation' }
```

Operation objects and hashtables remain useful for generated pipelines and batching.

**Example 1: Running a global action using piping**
 ```powershell
 @{Uri="WhoAmI"} | Send-DataverseOperation
 ```
 Or:
 ```powershell
 Send-DataverseOperation @{Uri="WhoAmI"}
 ```

Or even:

```powershell
Send-DataverseOperation WhoAmI
```

For batched pipelines, `-MaxDop` is the client-side concurrency ceiling. PSDataverse automatically follows Dataverse's lower concurrency hint when one is returned. Results stream in completion order by default; specify `-OutputOrder Input` when stable input ordering is required.

Common tasks now have PowerShell-native wrappers:

```powershell
Test-DataverseConnection -Detailed
Test-DataverseBulkOperationSupport account -Operation UpsertMultiple -Detailed
Get-DataverseRow accounts -Select name,accountid -Top 10
New-DataverseRow accounts @{ name = 'Contoso' }
Set-DataverseRow accounts $accountId @{ telephone1 = '+33 1 23 45 67 89' }
Remove-DataverseRow accounts $accountId
Get-DataverseTableMetadata account -IncludeColumns
New-DataverseTable new_Project 'Project' 'Projects'
Invoke-DataverseCreateMultiple new_projects @(
    @{ new_projectid = [guid]::NewGuid(); new_projectname = 'First project' }
) -ChunkSize 100 -MaxDop 4
Remove-DataverseTable new_project -Confirm:$false
Invoke-DataverseAction -Name new_Recalculate -Parameters @{ TargetId = $accountId }
Export-DataverseRows accounts ./accounts.csv -Select name,accountid
Import-DataverseRows accounts ./accounts.csv -BatchSize 10 -MaxDop 4
```

Table creation can be associated with an unmanaged solution by passing
`-SolutionUniqueName` to `New-DataverseTable`. `Invoke-DataverseCreateMultiple`,
`Invoke-DataverseUpdateMultiple`, and `Invoke-DataverseUpsertMultiple` expose
Dataverse's homogeneous multiple-row APIs while preserving
`Invoke-DataverseRequest` as the low-level escape hatch. They split rows into
`-ChunkSize` groups and run those requests concurrently up to `-MaxDop`. The table
logical name is normally resolved from the table-set name; supply
`-TableLogicalName` explicitly to avoid entity-name resolution.

Bulk commands verify table support before preparing or sending chunks. The first
operation for a table and connection performs one combined capability query for
`CreateMultiple` and `UpdateMultiple`; subsequent operations use the connection's
in-memory cache. Concurrent first-time callers share the same lookup. `UpsertMultiple`
support is derived from both messages being available. Inspect or refresh the result
explicitly when needed:

```powershell
Test-DataverseBulkOperationSupport account -Operation CreateMultiple
Test-DataverseBulkOperationSupport account -Operation UpsertMultiple -Detailed
Test-DataverseBulkOperationSupport account -Operation UpdateMultiple -Refresh
```

A definitive unsupported result stops before any rows are sent with error ID
`DVERR-1021` and recommends `$batch`. If metadata inspection itself is unavailable,
the bulk command preserves existing behavior and attempts the requested operation;
use `-Verbose` to see that diagnostic. Capability results live only for the owning
connection, so environments never share them.

`Import-DataverseRows` makes the transport choice explicit:

```powershell
# One independent HTTP request per row.
Import-DataverseRows accounts ./accounts.csv -Mode Individual -MaxDop 20

# Multipart $batch requests containing transactional change sets.
Import-DataverseRows accounts ./accounts.csv -Mode Batch -BatchSize 20 -MaxDop 5

# CreateMultiple requests, automatically chunked and sent concurrently.
Import-DataverseRows new_projects ./projects.csv -Mode Bulk -ChunkSize 50 -MaxDop 2
```

`Batch` remains the import default for compatibility. `Bulk` means
`CreateMultiple` for imports; it is not Dataverse's asynchronous bulk-delete job.
Use `UpsertMultiple` when rows may already exist, normally with primary IDs or
alternate keys that identify them.

When a multiple-operation chunk fails, error ID `DVERR-1020` identifies its original
one-based row range without printing row contents. The structured target keeps the
rows and successful sibling chunks available for logging, correction, or retry:

```powershell
$bulkErrors = @()
Invoke-DataverseCreateMultiple new_projects $rows `
    -ChunkSize 100 -MaxDop 4 -ErrorAction SilentlyContinue -ErrorVariable bulkErrors

$failure = $bulkErrors[0].TargetObject
$failure | Select-Object ActionName, ChunkNumber, StartRow, EndRow, ContentId
$failure.InputRows | Export-Csv ./failed-rows.csv -NoTypeInformation
$failure.SuccessfulChunkNumbers
```

### Performance testing

The read benchmark only characterizes GET requests. A separate destructive
benchmark measures POST, PATCH, and DELETE using individual requests, transactional
`$batch` change sets, and the supported CreateMultiple/UpdateMultiple APIs:

```powershell
Connect-Dataverse https://<environment>.crm.dynamics.com -DeviceCode
$results = ./tools/Measure-DataverseWritePerformance.ps1 `
    -Count 100 `
    -MaxDop 1,8,20,32 `
    -BatchSize 20 -BatchMaxDop 1,4,8 `
    -BulkSize 50 -BulkMaxDop 1,4 `
    -RepeatCount 3 -SummaryOnly `
    -Confirm:$false
$results | Format-Table -AutoSize
```

Run this only in a disposable development environment. It creates a uniquely
named `new_PsdvBenchmark...` table, verifies row counts between phases, and
removes the entire table in a `finally` block. It warms each transport, randomizes
scenario order within each verb, repeats the measurements, and reports medians
with `-SummaryOnly`. `EnvelopeCount` and `EffectiveMaxDop` distinguish requested
parallelism from the number of HTTP or `$batch` requests that can actually run
concurrently. A batch or bulk DOP above one has no effect unless `Count` is greater
than its corresponding batch or bulk size.

Dataverse does not support DeleteMultiple for standard tables, so DELETE compares
individual requests with `$batch` only. Concurrent batch envelopes are appropriate
only when the envelopes are independent; operations inside each transactional
change set remain ordered, but separate envelopes can complete out of order.



This will result in an OperationResponse like the following:

```
ContentId  :
Content    : {"@odata.context":"https://yourdynamic-environment.crm4.dynamics.com/api/data/v9.2/$metadata#Microsoft.Dy
             namics.CRM.WhoAmIResponse","BusinessUnitId":"6f202e6c-e471-ec11-8941-000d3adf0002","UserId":"88057198-a9b1
             -ec11-9840-00567ab5c181","OrganizationId":"e34c95a5-f34c-430c-a05e-a23437e5b9fa"}
Error      :
Headers    : {[Cache-Control, no-cache], [x-ms-service-request-id, c9af118d-b483-48c8-887a-baa4de679bf,9b44cd2f-d34f-4
             908-bed4-edc12c44857d], [Set-Cookie, ARRAffinity=49fcec1e1d435e207a47447f2a47260b469a63eb4e69bdcc0610e04c1
             24a7f15; domain=yourdynamic-environment.crm4.dynamics.com; path=/; secure; HttpOnly], [Strict-Transport-S
             ecurity, max-age=31536000; includeSubDomains]…}
StatusCode : OK
```

You can always see the original headers and status code from dynamics. If there's any error, it will be reflected in 'Error' property. The most important property that you will often need is the 'Content' that includes the payload in JSON format.

The following command will have exactly the same result, but it is sending a string which is considered as JSON.
```powershell
'{"Uri":"WhoAmI"}' | Send-DataverseOperation
```
or:
```powershell
Send-DataverseOperation '{"Uri":"WhoAmI"}'
```


> **ℹ NOTE**
> When the input is a Hashtable like object, it will be converted to JSON equivalent before sending to Dataverse. To have more control over the conversion to JSON, it is recommended to use the native `[ConvertTo-Json](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/convertto-json)` before Send-DataverseOperation.

**Example 2: Running a global action using piping and display the returned object**

Now, let's see how we can get to the 'Content' property, convert it to a PowerShell object and then display it as a list, all in one line.

```powershell
@{Uri="WhoAmI"} | Send-DataverseOperation | select -ExpandProperty Content | ConvertFrom-Json | Format-List
```

This will reult in the following output:

```
@odata.context : https://helloworld.crm4.dynamics.com/api/data/v9.2/$metadata#Microsoft.Dynamics.CRM.WhoA
                 mIResponse
BusinessUnitId : 6f202e6c-e471-ec11-8941-000d3adf0002
UserId         : 88057198-a9b1-ec11-9840-00567ab5c181
OrganizationId : e34c95a5-f34c-430c-a05e-a23437e5b9fa
```

**Example 3: Running a global action and accessing the result**

When the result is converted to an object, you can access any of the properties like any other PowerShell object.

```powershell
$whoAmI = ConvertTo-Json ([pscustomobject]@{Uri="WhoAmI"}) | Send-DataverseOperation | ConvertFrom-Json
Write-Host $whoAmI.UserId
```
The above example sends a WhoAmI request to the Dataverse and gets back the result. If you check carefully this is what happens in each step:
1. An operation is defined as a Hashtable i.e. `@{Uri="WhoAmI";Method="GET"}` and using `ConvertTo-Json` this Hashtable is converted to JSON.
2. The operation is piped to `Send-DataverseOperation` that sends the operation to Dataverse and gets back the result.
3. The result of `Send-Operation` is then converted back to a Hashtable. The table will contain three properties as per documentation. BusinessUnitId, UserId, and OrganizationId
4. The second line is just printing the UserId to the host.

# Status

[![PSScriptAnalyzer](https://github.com/rezanid/PSDataverse/actions/workflows/powershell.yml/badge.svg)](https://github.com/rezanid/PSDataverse/actions/workflows/powershell.yml)
