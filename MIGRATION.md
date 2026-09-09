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

## Planned PSDataverse 2 migration

The following design is planned but not implemented in Milestone 0:

- `Connect-Dataverse` will accept both PowerShell-native parameters and legacy connection strings.
- Connections will become explicit objects and may be selected by object or name. A default connection will preserve concise scripts.
- `Invoke-DataverseRequest` will become the preferred low-level verb-noun name. `Send-DataverseOperation` is expected to remain as a compatibility alias during migration.
- WAM/system-browser interactive authentication, device code, application credentials, supplied access tokens, and token-provider callbacks will use explicit parameter sets.
- Convenience CRUD, metadata, action/function, and bulk commands will layer on the same transparent request engine.

Migration examples and deprecation periods will be added when those surfaces are implemented.
