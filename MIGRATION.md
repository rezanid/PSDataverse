# Migrating from PSDataverse 0.x to PSDataverse 2

PSDataverse 2 keeps the familiar Web API operation model and the
`Send-DataverseOperation` name, while modernizing the runtime, authentication,
connection management, concurrency, and high-level command surface. This guide is
for scripts moving from the latest PSDataverse 0.x release to 2.x.

## Before you upgrade

PSDataverse 2 requires PowerShell 7.6 LTS or later and targets .NET 10. PowerShell
5.1 and PowerShell 7.4 cannot load its binary package. Check the host first:

```powershell
$PSVersionTable.PSVersion
$PSVersionTable.PSEdition
```

Test important automation in a disposable environment, especially scripts that
submit writes concurrently. PowerShell can keep the 0.x and 2.x module versions
installed side by side while you validate the migration.

The PSDataverse 2 release-candidate package is version `2.0.0-rc.1`. Because it is a
prerelease, include `-Prerelease` when discovering or acquiring it from a PowerShell
repository. Use `Import-Module PSDataverse -RequiredVersion 2.0.0` to make test
scripts select the 2.x base version explicitly.

## Fast migration checklist

1. Move the host to PowerShell 7.6 LTS or later.
2. Import PSDataverse 2 in a clean PowerShell session.
3. Keep `Send-DataverseOperation`, or rename it to the preferred
   `Invoke-DataverseRequest` command.
4. Stop reading undocumented `Dataverse-*` global variables. Store the connection
   returned by `Connect-Dataverse` instead.
5. Select an explicit authentication parameter set for new scripts.
6. Set `-MaxDop 1` wherever serial execution is required.
7. Set `-OutputOrder Input` wherever pipeline result order matters.
8. Review write retry assumptions: PSDataverse no longer automatically replays
   ambiguous POST, PATCH, DELETE, or write-batch requests.
9. Prefer the row, metadata, import/export, and multiple-operation commands where
   they replace hand-built requests.
10. Run `Get-Help <command> -Full` for packaged syntax and examples.

## Commands and compatibility

| 0.x usage | PSDataverse 2 guidance | Compatibility |
|---|---|---|
| `Connect-Dataverse` | Continue using it; prefer an explicit authentication switch | Retained |
| `Send-DataverseOperation` | Prefer `Invoke-DataverseRequest` in new scripts | Retained as an alias for at least one major release |
| `Disconnect-Dataverse` | Can disconnect one named connection or all connections | Retained |
| `Clear-DataverseTable` | Continue using it | Retained |
| `Get-DataverseAttributes` | Prefer `Get-DataverseTableMetadata -IncludeColumns` for new metadata work | Retained |
| `Get-DataverseTableRowCount` | Continue using it; the requested table is now queried correctly | Retained and corrected |
| `Export-DataverseOptionSet` | Continue using it | Retained and corrected for pipeline input |
| `Connect-DataverseNew` | Use `Connect-Dataverse` | Removed; it was an unexported experiment |
| `ConvertTo-CustomText` | Use a dedicated templating module or application | Removed; it was internal and unexported |

PSDataverse 2 adds named connection commands, row CRUD, table metadata and
provisioning, actions and functions, CSV/JSON import and export, and
CreateMultiple/UpdateMultiple/UpsertMultiple commands. See the
[command reference](docs/reference/README.md).

## Authentication

### Interactive and passwordless sign-in

On Windows, interactive authentication uses Web Account Manager (WAM) by default.
On other platforms it uses the system browser. Passkeys and other passwordless
methods are offered by that operating-system or browser experience; they are not a
separate OAuth flow.

```powershell
$connection = Connect-Dataverse $url -Interactive
$connection = Connect-Dataverse $url -Interactive -UseWebAccountManager # -UseWam also works
$connection = Connect-Dataverse $url -Interactive -UseSystemBrowser
$connection = Connect-Dataverse $url -Interactive -ForceAuthentication
```

### Device code and IWA

Device code is suitable for terminals without an interactive browser. An explicit
device-code connection always starts the interaction; later refreshes use the
cache when possible.

```powershell
$connection = Connect-Dataverse $url -DeviceCode
```

IWA remains available on Windows for federated, Active Directory-backed users:

```powershell
$connection = Connect-Dataverse $url `
    -IntegratedWindowsAuthentication `
    -TenantId $tenantId `
    -UserPrincipalName 'user@contoso.com'
```

Microsoft has deprecated IWA, and it cannot authenticate managed Entra-only users
or satisfy many MFA and Conditional Access policies. PSDataverse converts that
common failure into guidance to use `-Interactive` or `-DeviceCode`.

### Applications, supplied tokens, and token providers

Client secrets are `SecureString` values. Certificates come from the operating
system certificate store.

```powershell
$connection = Connect-Dataverse $url -ClientId $appId -TenantId $tenantId `
    -ClientSecret (Read-Host 'Client secret' -AsSecureString)

$connection = Connect-Dataverse $url -ClientId $appId -TenantId $tenantId `
    -CertificateThumbprint $thumbprint

$connection = Connect-Dataverse $url -AccessToken $secureToken -ExpiresOn $expiresOn

$connection = Connect-Dataverse $url -TokenProvider {
    param($cancellationToken)
    [DataverseAccessToken]::new((Get-Token), (Get-Date).AddMinutes(50))
}
```

A secret provider keeps vault integration outside PSDataverse:

```powershell
$connection = Connect-Dataverse $url -ClientId $appId -TenantId $tenantId `
    -ClientSecretProvider { Get-Secret DataverseAppSecret }
```

### Existing connection strings

Connection strings remain a migration path. PSDataverse accepts its original keys
and familiar XRM tooling names including `Url`, `AuthType`, `ApplicationId`,
`Secret`, `TenantId`, and `CertificateThumbprint`.

```powershell
$connection = Connect-Dataverse `
    'AuthType=ClientSecret;Url=https://contoso.crm.dynamics.com;ApplicationId=00000000-0000-0000-0000-000000000000;Secret=...;TenantId=organizations'
```

Duplicate keys now fail instead of being interpreted ambiguously. Prefer individual
parameters in new scripts because PowerShell can validate the selected flow and
keep secrets out of ordinary strings.

## Connections are first-class objects

`Connect-Dataverse` returns a `DataverseConnection`. The newest connection remains
the default, preserving concise 0.x calls, but scripts can avoid global state:

```powershell
$development = Connect-Dataverse $developmentUrl -Interactive -Name dev
$production = Connect-Dataverse $productionUrl -DeviceCode -Name prod -NoDefault

Invoke-DataverseRequest WhoAmI -Connection $development
Invoke-DataverseRequest WhoAmI -ConnectionName prod
Set-DataverseDefaultConnection prod
Get-DataverseConnection
Disconnect-Dataverse prod
Disconnect-Dataverse -All
```

Connections own and dispose their HTTP, authentication, token-cache, and refresh
resources. Access tokens and client secrets are not public connection properties.
Scripts that read the old undocumented `Dataverse-*` global variables must retain
the returned object or use `Get-DataverseConnection`. Connecting again now replaces
and disposes the previous default resources, so a new environment URL is honored.

## Request migration

The preferred low-level name is `Invoke-DataverseRequest`; the former name is an
alias to exactly the same command.

```powershell
Send-DataverseOperation WhoAmI
Invoke-DataverseRequest WhoAmI

Invoke-DataverseRequest -Uri accounts -Method POST -Body @{
    name = 'Contoso'
} -Headers @{ Prefer = 'return=representation' }

$operations | Invoke-DataverseRequest -BatchSize 20 -MaxDop 5
```

Existing operation objects and hashtables remain supported. Successful content is
retained for every 2xx response, including HTTP 201. Empty, malformed, and non-JSON
failures now preserve the descriptive request error. Ctrl+C propagates through
requests, pagination, batches, concurrency waits, and retry delays.

The removed `-Retry` switches on `Connect-Dataverse` and
`Send-DataverseOperation` never changed behavior. Remove them from scripts.

## Concurrency, ordering, and retries

`-MaxDop` is the client-side concurrency ceiling. Dataverse's `x-ms-dop-hint` may
lower active concurrency for queued work. Omitted or zero `-MaxDop` uses the default
ceiling of 20; use `-MaxDop 1` for intentional serial execution.

Batch mode in 0.x could accidentally run serially when `-MaxDop` was omitted. In
2.x it uses the default ceiling. Add `-MaxDop 1` if that behavior was important.

Results stream in completion order for throughput. Request input order only when a
downstream pipeline depends on it:

```powershell
$operations | Invoke-DataverseRequest -MaxDop 20 -OutputOrder Input
```

Automatic transport retries are replay-safe by default. GET, HEAD, and OPTIONS
retry transient failures and honor `Retry-After`. POST, PATCH, DELETE, and batches
containing writes are sent once because a lost response does not prove the server
failed to apply the change. Before retrying an ambiguous write, perform an
application-specific existence, alternate-key, or version check.

For measured starting points and semantic differences between individual,
`$batch`, and multiple-operation requests, see
[Choosing a Dataverse write transport](docs/guides/choosing-a-write-transport.md).

## Prefer the convenience commands

Many hand-built 0.x requests can become clearer PowerShell:

```powershell
Test-DataverseConnection -Detailed

$row = New-DataverseRow accounts @{ name = 'Contoso' } -PassThru
Get-DataverseRow accounts $row.Id
Set-DataverseRow accounts $row.Id @{ telephone1 = '555-0100' }
Remove-DataverseRow accounts $row.Id

Get-DataverseTableMetadata account -IncludeColumns
Invoke-DataverseAction -Name 'WhoAmI'
Export-DataverseRows accounts ./accounts.json -Format Json
```

Row commands take the Web API entity-set name such as `accounts`. Metadata and
multiple-operation capability checks take the singular logical name such as
`account` unless their syntax asks for a table-set name.

## Imports and high-throughput writes

`Import-DataverseRows` makes its transport explicit. `$batch` remains the default
for compatibility.

```powershell
Import-DataverseRows accounts ./accounts.csv -Mode Individual -MaxDop 20
Import-DataverseRows accounts ./accounts.csv -Mode Batch -BatchSize 20 -MaxDop 5
Import-DataverseRows new_projects ./projects.csv -Mode Bulk -ChunkSize 100 -MaxDop 4
```

Here `Bulk` means Dataverse `CreateMultiple`, not the asynchronous bulk-delete job.
Dedicated commands expose `CreateMultiple`, `UpdateMultiple`, and `UpsertMultiple`.
Not every table supports every multiple message, so capability is detected and
cached per connection:

```powershell
Test-DataverseBulkOperationSupport account -Operation UpsertMultiple -Detailed
Invoke-DataverseUpsertMultiple accounts $rows -ChunkSize 100 -MaxDop 4
```

When a multiple-operation chunk fails, error ID `DVERR-1020` identifies its
original one-based row range without printing row contents. The target object keeps
the input rows and successful sibling chunk numbers for correction or retry:

```powershell
$bulkErrors = @()
Invoke-DataverseCreateMultiple new_projects $rows `
    -ChunkSize 100 -MaxDop 4 `
    -ErrorAction SilentlyContinue -ErrorVariable bulkErrors

$failure = $bulkErrors[0].TargetObject
$failure | Select-Object ActionName, ChunkNumber, StartRow, EndRow, ContentId
$failure.InputRows | Export-Csv ./failed-rows.csv -NoTypeInformation
$failure.SuccessfulChunkNumbers
```

A definitively unsupported operation fails before rows are submitted with error ID
`DVERR-1021`. If metadata inspection is unavailable, PSDataverse preserves the
request attempt rather than treating uncertainty as lack of support.

## Correctness changes to check

- `Get-DataverseTableRowCount -TableName` now queries the requested table with one
  minimal request.
- `Export-DataverseOptionSet` now uses each pipeline name when several are supplied.
- `ChangeSet.RemoveOperation(string)` removes the exact matching content ID.
- Access tokens are no longer printed by debug output.
- Tokens refresh only within five minutes of expiry; concurrent requests share one
  refresh.
- Connection-specific service-protection hints and bulk capability results do not
  leak between environments.

## Troubleshooting

Confirm that the intended package is imported in a clean session:

```powershell
Get-Module PSDataverse -All | Select-Object Name, Version, Path
Get-Command Connect-Dataverse -Syntax
Get-Help Connect-Dataverse -Full
```

Close every PowerShell session that imported a development build before replacing
its files. PSDataverse does not work around locked assemblies.

If IWA says the user is managed, use `Connect-Dataverse $url -Interactive` or
`Connect-Dataverse $url -DeviceCode`. If concurrent results arrive out of order,
add `-OutputOrder Input`; use `-MaxDop 1` when the operations themselves must be
serialized.
