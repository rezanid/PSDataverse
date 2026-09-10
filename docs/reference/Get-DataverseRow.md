# Get-DataverseRow

## Syntax

```powershell
Get-DataverseRow [-All] [-Connection <DataverseConnection>] [-ConnectionName <String>] [-Filter <String>] [-Select <String[]>] [-Top <Int32>] -TableSetName <String>
```

```powershell
Get-DataverseRow [-Connection <DataverseConnection>] [-ConnectionName <String>] -TableSetName <String> -Id <Guid>
```

## Parameters

| Name | Type | Required | Pipeline | Aliases |
|---|---|---:|---:|---|
| `-All` | `SwitchParameter` | No | No |  |
| `-Connection` | `DataverseConnection` | No | No |  |
| `-ConnectionName` | `String` | No | No |  |
| `-Filter` | `String` | No | No |  |
| `-Id` | `Guid` | Yes | No |  |
| `-Select` | `String[]` | No | No |  |
| `-TableSetName` | `String` | Yes | No |  |
| `-Top` | `Int32` | No | No |  |
