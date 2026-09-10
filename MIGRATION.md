# Migrating from PSDataverse 0.x to PSDataverse 2

This guide tracks breaking and behavior-changing work as PSDataverse 2 is developed. It is intentionally updated alongside the implementation; items marked “planned” are not available yet.

## Milestone 0 changes

### Runtime support

The transition build requires PowerShell 7.4 or later and targets .NET 8. PowerShell 5.1 is not supported. The stable PSDataverse 2 release is expected to require PowerShell 7.6 LTS and .NET 10; that final runtime decision has not yet been applied.

### Removed template command

The internal `ConvertTo-CustomText` cmdlet and its Scriban/Humanizer dependencies were removed. The cmdlet was not exported by the module manifest, so ordinary PSDataverse scripts could not call it after a normal module import. If you loaded the assembly directly and used this cmdlet, invoke Scriban from a dedicated module or application before upgrading.

### Removed experimental connection command

The unexported `Connect-DataverseNew` experiment was removed. Continue using `Connect-Dataverse`. Its useful ideas will return as tested parameter sets on the primary command in the connection/authentication milestone.

### Removed no-op parameters

The unused `-Retry` parameters were removed from `Connect-Dataverse` and `Send-DataverseOperation`. Retry behavior remains automatic. A later release will expose meaningful resilience settings rather than accepting a switch that did nothing.

### Exported command corrections

The manifest now exports the commands implemented by the module:

```text
Clear-DataverseTable
Connect-Dataverse
Disconnect-Dataverse
Export-DataverseOptionSet
Get-DataverseAttributes
Get-DataverseTableRowCount
Send-DataverseOperation
```

Scripts do not need to dot-source helper files to access those commands. If a script depended on a helper remaining private, qualify or rename its own function to avoid a command-name collision.

### Correctness changes

- Calling `Connect-Dataverse` again now replaces and disposes the previous connection resources, so requests use the newly supplied environment URL.
- Access tokens are no longer printed by `-Debug`.
- Batch mode with no explicit `-MaxDop` now actually runs with the default concurrency of 20. Use `-MaxDop 1` if serial batch submission was intentional.
- Ctrl+C cancellation now propagates through requests, pagination, batches, semaphore waits, and retries.
- Tokens are refreshed only when they are within five minutes of expiry.
- Successful response content is retained for all 2xx statuses, including 201.
- Empty, malformed, and non-JSON server errors now produce descriptive batch/parse exceptions instead of secondary null-reference failures.
- `Get-DataverseTableRowCount -TableName` now queries the requested table and obtains the count with one minimal request.
- `Export-DataverseOptionSet` now uses each pipeline name when multiple names are supplied.
- `ChangeSet.RemoveOperation(string)` now removes the exact matching content ID.

## Milestone 2 connection and authentication changes

`Connect-Dataverse` now returns a `DataverseConnection`. The connection owns its HTTP, authentication, and token-refresh resources but does not publicly expose its access token or client secret. The most recently connected environment remains the default, so concise existing calls to `Send-DataverseOperation` continue to work.

Connections can be named and selected explicitly:

```powershell
$development = Connect-Dataverse https://dev.crm.dynamics.com -Interactive -Name dev
$production = Connect-Dataverse https://prod.crm.dynamics.com -DeviceCode -Name prod -NoDefault

Get-DataverseConnection
Set-DataverseDefaultConnection prod
Send-DataverseOperation accounts -ConnectionName dev
Send-DataverseOperation accounts -Connection $production
Disconnect-Dataverse dev
Disconnect-Dataverse -All
```

The supported authentication forms are:

```powershell
# WAM on Windows; system browser on other platforms
Connect-Dataverse $url -Interactive -TenantId $tenantId

# Explicit WAM selection on Windows (WAM remains the default there)
Connect-Dataverse $url -Interactive -UseWam -TenantId $tenantId

# Force the system browser on Windows
Connect-Dataverse $url -Interactive -UseSystemBrowser

# Bypass a cached identity and display the account chooser
Connect-Dataverse $url -Interactive -ForceAuthentication

Connect-Dataverse $url -DeviceCode -TenantId $tenantId

# Windows-only IWA (subject to tenant policy, federation, and MFA constraints)
Connect-Dataverse $url -IntegratedWindowsAuthentication -TenantId $tenantId

Connect-Dataverse $url -ClientId $appId -TenantId $tenantId `
    -ClientSecret (Read-Host -AsSecureString)

Connect-Dataverse $url -ClientId $appId -TenantId $tenantId `
    -CertificateThumbprint $thumbprint

Connect-Dataverse $url -AccessToken $secureToken -ExpiresOn $expiry

Connect-Dataverse $url -TokenProvider {
    param($cancellationToken)
    [DataverseAccessToken]::new((Get-Token), (Get-Date).AddMinutes(50))
}
```

Secret stores remain optional. A provider can resolve a secret without PSDataverse depending on a particular vault module:

```powershell
Connect-Dataverse $url -ClientId $appId -TenantId $tenantId `
    -ClientSecretProvider { Get-Secret DataverseAppSecret }
```

Legacy connection strings remain supported. Familiar XRM tooling names such as `Url`, `AuthType`, `ApplicationId`, `Secret`, `TenantId`, and `CertificateThumbprint` are accepted alongside the original PSDataverse spellings. Duplicate keys now fail with a targeted error rather than being interpreted ambiguously.

`Disconnect-Dataverse` disconnects the default connection when no name is supplied. Use `-All` to dispose every connection. The old `Dataverse-*` global token/service-provider variables are no longer the source of truth; scripts that read those undocumented variables should migrate to `Get-DataverseConnection`.

Passkeys are available through the operating system or browser interactive sign-in experience. There is intentionally no separate “passkey OAuth flow.”

Explicit device-code connections now always perform the device-code interaction instead of silently selecting the first cached account. Token refreshes for the resulting connection still use the cache. Interactive connections retain silent single sign-on by default; use `-ForceAuthentication` when an account chooser is required. In connection strings, the equivalent option is `ForceAuthentication=true`.

Traditional IWA is deprecated by Microsoft in favor of WAM and only supports federated, Active Directory-backed users. Managed Entra-only identities now receive a targeted error recommending `-Interactive` or `-DeviceCode` instead of the raw MSAL failure.

## Milestone 3 request-engine changes

Batch submission now uses a bounded channel rather than an ever-growing task list and polling loop. `-MaxDop` remains the client-side ceiling; when Dataverse returns `x-ms-dop-hint`, PSDataverse lowers the active concurrency for subsequent queued batches. Completion order remains the fast default. Use `-OutputOrder Input` when downstream pipeline processing must match the original batch order.

Automatic transport retries are now replay-safe by default. GET, HEAD, and OPTIONS requests retry transient failures and honor `Retry-After`. POST, PATCH, DELETE, and batches containing those methods are sent once because a missing response does not prove that Dataverse failed to apply the write. Scripts that previously relied on implicit write replay should perform an application-specific existence/version check before retrying.

`Invoke-DataverseRequest` is now the preferred low-level command. `Send-DataverseOperation` is an exported alias to the same implementation and will remain available for at least one major release. Existing operation objects and hashtables continue to work, while new scripts can use direct parameters:

```powershell
Invoke-DataverseRequest -Uri 'accounts?$select=name&$top=5'

Invoke-DataverseRequest -Uri accounts -Method POST -Body @{
    name = 'Contoso'
} -Headers @{ Prefer = 'return=representation' }
```

Convenience CRUD, metadata, action/function, and bulk commands are still planned on top of this request engine.
