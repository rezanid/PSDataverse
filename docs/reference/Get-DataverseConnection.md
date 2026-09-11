---
document type: cmdlet
external help file: PSDataverse.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Get-DataverseConnection
---

# Get-DataverseConnection

## SYNOPSIS

Gets registered Dataverse connections.

## SYNTAX

### __AllParameterSets

```
Get-DataverseConnection [[-Name] <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Get-DataverseConnection reads the session-scoped connection registry. With no name it returns every connection; Name accepts PowerShell wildcards.

## EXAMPLES

### Example 1: List connections

```powershell
Get-DataverseConnection
```

Lists all registered connections and their authentication kinds.

### Example 2: Find development connections

```powershell
Get-DataverseConnection 'dev*'
```

Returns connection names that begin with dev.

## PARAMETERS

### -Name

A connection-name wildcard pattern.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: true
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

This command does not accept pipeline input.

## OUTPUTS

### PSDataverse.DataverseConnection

PSDataverse.DataverseConnection objects.

## NOTES

Connection objects do not expose bearer tokens or client secrets.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
