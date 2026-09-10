# Table of Contents
* [What is PSDataverse](#what-is-psdataverse)
* [Features](#features)
* [How to install](#how-to-install)
* [How to use](#how-to-use)
  * [Connecting to Dataverse](#connecting-to-dataverse)
  * [Sending operations to Dataverse](#sending-operations-to-dataverse)

# What is PSDataverse?
PSDataverse is a PowerShell module that brings Dataverse's Web API to PowerShell 7+ with features like piping, batching and more. It is designed with ease-of-use and performance in mind and follows the patterns of native PowerShell cmdlets to play nicely with other modules.

> [!IMPORTANT]
> PSDataverse 2 modernization is in progress. See the [implementation plan](docs/PSDataverse-2-plan.md) and [0.x migration guide](MIGRATION.md). The current development build requires PowerShell 7.4 or later.

The generated [command reference](docs/reference/README.md) lists the syntax, parameter sets, pipeline support, and aliases exported by the current package.

# Features
* Securely connect to Dataverse.
* Supports batching.
* Supports parallelism.
* Automatically reconnects when authentication token is about to expire.
* Enhanced pipeline support (accepts different data types as input and emits responses to the pipeline).
* Automatic wait-and-retry for transient errors by default.
* Respects throttling data sent by Dataverse.
* Does not hide the response sent back by Dataverse.

# How to install
You can install the [PSDataverse module directly from PowerShell Gallery](https://www.powershellgallery.com/packages/PSDataverse) using the following command
```powershell
Install-Module -Name PSDataverse
```

To build the latest source locally, install PowerShell 7.4 or later and the .NET 8 SDK, then run:

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
Get-DataverseRow accounts -Select name,accountid -Top 10
New-DataverseRow accounts @{ name = 'Contoso' }
Set-DataverseRow accounts $accountId @{ telephone1 = '+33 1 23 45 67 89' }
Remove-DataverseRow accounts $accountId
Get-DataverseTableMetadata account -IncludeColumns
New-DataverseTable new_Project 'Project' 'Projects'
Invoke-DataverseCreateMultiple new_projects new_project @(
    @{ new_projectid = [guid]::NewGuid(); new_projectname = 'First project' }
)
Remove-DataverseTable new_project -Confirm:$false
Invoke-DataverseAction -Name new_Recalculate -Parameters @{ TargetId = $accountId }
Export-DataverseRows accounts ./accounts.csv -Select name,accountid
Import-DataverseRows accounts ./accounts.csv -BatchSize 10 -MaxDop 4
```

Table creation can be associated with an unmanaged solution by passing
`-SolutionUniqueName` to `New-DataverseTable`. `Invoke-DataverseCreateMultiple`
and `Invoke-DataverseUpdateMultiple` expose Dataverse's bulk APIs for custom
standard tables while preserving `Invoke-DataverseRequest` as the low-level
escape hatch.

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
with `-SummaryOnly`. `EnvelopeCount` makes it clear how many HTTP or `$batch`
requests can actually run concurrently. A batch or bulk DOP above one has no
effect unless `Count` is greater than its corresponding batch or bulk size.

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
