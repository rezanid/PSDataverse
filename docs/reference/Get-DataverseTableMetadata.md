# Get-DataverseTableMetadata

## Syntax

```powershell
Get-DataverseTableMetadata [-Connection <DataverseConnection>] [-ConnectionName <String>] [-IncludeColumns] -LogicalName <String>
```

```powershell
Get-DataverseTableMetadata [-Connection <DataverseConnection>] [-ConnectionName <String>] [-IncludeColumns] -TableSetName <String>
```

## Parameters

| Name | Type | Required | Pipeline | Aliases |
|---|---|---:|---:|---|
| `-Connection` | `DataverseConnection` | No | No |  |
| `-ConnectionName` | `String` | No | No |  |
| `-IncludeColumns` | `SwitchParameter` | No | No |  |
| `-LogicalName` | `String` | Yes | Yes |  |
| `-TableSetName` | `String` | Yes | No |  |
