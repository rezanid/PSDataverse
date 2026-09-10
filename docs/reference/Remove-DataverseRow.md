# Remove-DataverseRow

## Syntax

```powershell
Remove-DataverseRow [-Confirm] [-Connection <DataverseConnection>] [-ConnectionName <String>] [-IfMatch <String>] [-WhatIf] -TableSetName <String> -Id <Guid>
```

## Parameters

| Name | Type | Required | Pipeline | Aliases |
|---|---|---:|---:|---|
| `-Confirm` | `SwitchParameter` | No | No | cf |
| `-Connection` | `DataverseConnection` | No | No |  |
| `-ConnectionName` | `String` | No | No |  |
| `-Id` | `Guid` | Yes | Yes |  |
| `-IfMatch` | `String` | No | No |  |
| `-TableSetName` | `String` | Yes | No |  |
| `-WhatIf` | `SwitchParameter` | No | No | wi |
