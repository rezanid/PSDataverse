# Invoke-DataverseRequest

## Syntax

```powershell
Invoke-DataverseRequest [-Connection <DataverseConnection>] [-ConnectionName <String>] [-OutputOrder <String>] -InputObject <PSObject> [-BatchSize <Int32>] [-MaxDop <Int32>] [-OutputTable] [-AutoPaginate]
```

```powershell
Invoke-DataverseRequest [-Connection <DataverseConnection>] [-ConnectionName <String>] [-OutputOrder <String>] -InputOperation <Operation`1> [-BatchSize <Int32>] [-MaxDop <Int32>] [-OutputTable] [-AutoPaginate]
```

```powershell
Invoke-DataverseRequest [-Body <Object>] [-Connection <DataverseConnection>] [-ConnectionName <String>] [-ContentId <String>] [-Headers <IDictionary>] [-Method <String>] [-OutputOrder <String>] -Uri <String> [-BatchSize <Int32>] [-MaxDop <Int32>] [-OutputTable] [-AutoPaginate]
```

## Parameters

| Name | Type | Required | Pipeline | Aliases |
|---|---|---:|---:|---|
| `-AutoPaginate` | `SwitchParameter` | No | No |  |
| `-BatchSize` | `Int32` | No | No | BatchCapacity |
| `-Body` | `Object` | No | No |  |
| `-Connection` | `DataverseConnection` | No | No |  |
| `-ConnectionName` | `String` | No | No |  |
| `-ContentId` | `String` | No | No |  |
| `-Headers` | `IDictionary` | No | No |  |
| `-InputObject` | `PSObject` | Yes | Yes |  |
| `-InputOperation` | `Operation&#96;1` | Yes | Yes |  |
| `-MaxDop` | `Int32` | No | No | ThrottleLimit |
| `-Method` | `String` | No | No |  |
| `-OutputOrder` | `String` | No | No |  |
| `-OutputTable` | `SwitchParameter` | No | No |  |
| `-Uri` | `String` | Yes | Yes |  |
