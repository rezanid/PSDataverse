# New-DataverseTable

## Syntax

```powershell
New-DataverseTable [-Confirm] [-Connection <DataverseConnection>] [-ConnectionName <String>] [-LanguageCode <Int32>] [-OwnershipType <String>] [-PrimaryNameMaxLength <Int32>] [-PrimaryNameSchemaName <String>] [-SolutionUniqueName <String>] [-WhatIf] -SchemaName <String> -DisplayName <String> -DisplayCollectionName <String>
```

## Parameters

| Name | Type | Required | Pipeline | Aliases |
|---|---|---:|---:|---|
| `-Confirm` | `SwitchParameter` | No | No | cf |
| `-Connection` | `DataverseConnection` | No | No |  |
| `-ConnectionName` | `String` | No | No |  |
| `-DisplayCollectionName` | `String` | Yes | No |  |
| `-DisplayName` | `String` | Yes | No |  |
| `-LanguageCode` | `Int32` | No | No |  |
| `-OwnershipType` | `String` | No | No |  |
| `-PrimaryNameMaxLength` | `Int32` | No | No |  |
| `-PrimaryNameSchemaName` | `String` | No | No |  |
| `-SchemaName` | `String` | Yes | No |  |
| `-SolutionUniqueName` | `String` | No | No |  |
| `-WhatIf` | `SwitchParameter` | No | No | wi |
