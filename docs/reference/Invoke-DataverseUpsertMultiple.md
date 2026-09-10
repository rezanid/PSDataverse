# Invoke-DataverseUpsertMultiple

## Syntax

```powershell
Invoke-DataverseUpsertMultiple [-ChunkSize <Int32>] [-Confirm] [-Connection <DataverseConnection>] [-ConnectionName <String>] [-MaxDop <Int32>] [-WhatIf] -TableSetName <String> -Rows <Object[]>
```

```powershell
Invoke-DataverseUpsertMultiple [-ChunkSize <Int32>] [-Confirm] [-Connection <DataverseConnection>] [-ConnectionName <String>] [-MaxDop <Int32>] [-WhatIf] -TableSetName <String> -TableLogicalName <String> -Rows <Object[]>
```

## Parameters

| Name | Type | Required | Pipeline | Aliases |
|---|---|---:|---:|---|
| `-ChunkSize` | `Int32` | No | No |  |
| `-Confirm` | `SwitchParameter` | No | No | cf |
| `-Connection` | `DataverseConnection` | No | No |  |
| `-ConnectionName` | `String` | No | No |  |
| `-MaxDop` | `Int32` | No | No |  |
| `-Rows` | `Object[]` | Yes | No |  |
| `-TableLogicalName` | `String` | Yes | No |  |
| `-TableSetName` | `String` | Yes | No |  |
| `-WhatIf` | `SwitchParameter` | No | No | wi |
