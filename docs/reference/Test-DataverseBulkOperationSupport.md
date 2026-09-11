# Test-DataverseBulkOperationSupport

## Syntax

```powershell
Test-DataverseBulkOperationSupport [-Connection <DataverseConnection>] [-ConnectionName <String>] [-Detailed] [-Refresh] -LogicalName <String> -Operation <String>
```

```powershell
Test-DataverseBulkOperationSupport [-Connection <DataverseConnection>] [-ConnectionName <String>] [-Detailed] [-Refresh] -TableSetName <String> -Operation <String>
```

## Parameters

| Name | Type | Required | Pipeline | Aliases |
|---|---|---:|---:|---|
| `-Connection` | `DataverseConnection` | No | No |  |
| `-ConnectionName` | `String` | No | No |  |
| `-Detailed` | `SwitchParameter` | No | No |  |
| `-LogicalName` | `String` | Yes | Yes |  |
| `-Operation` | `String` | Yes | No |  |
| `-Refresh` | `SwitchParameter` | No | No |  |
| `-TableSetName` | `String` | Yes | No |  |
