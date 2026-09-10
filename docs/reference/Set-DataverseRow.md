# Set-DataverseRow

## Syntax

```powershell
Set-DataverseRow [-Confirm] [-Connection <DataverseConnection>] [-ConnectionName <String>] [-IfMatch <String>] [-PassThru] [-WhatIf] -TableSetName <String> -Id <Guid> -Body <Object>
```

## Parameters

| Name | Type | Required | Pipeline | Aliases |
|---|---|---:|---:|---|
| `-Body` | `Object` | Yes | Yes |  |
| `-Confirm` | `SwitchParameter` | No | No | cf |
| `-Connection` | `DataverseConnection` | No | No |  |
| `-ConnectionName` | `String` | No | No |  |
| `-Id` | `Guid` | Yes | No |  |
| `-IfMatch` | `String` | No | No |  |
| `-PassThru` | `SwitchParameter` | No | No |  |
| `-TableSetName` | `String` | Yes | No |  |
| `-WhatIf` | `SwitchParameter` | No | No | wi |
