---
document type: cmdlet
external help file: PSDataverse.PowerShell-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Get-DataverseTableRowCount
---

# Get-DataverseTableRowCount

## SYNOPSIS

Counts rows in a Dataverse table.

## SYNTAX

### __AllParameterSets

```
Get-DataverseTableRowCount [-TableName] <string> [[-Filter] <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Get-DataverseTableRowCount resolves the table collection and primary key, then issues a minimal `$count` query with `$top=1`. An optional OData filter limits the count.

## EXAMPLES

### Example 1: Count accounts

```powershell
Get-DataverseTableRowCount account
```

Returns the account table row count.

### Example 2: Count active accounts

```powershell
Get-DataverseTableRowCount account -Filter 'statecode eq 0'
```

Counts only active accounts.

## PARAMETERS

### -Filter

An OData filter expression without the `$filter=` prefix.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TableName

The singular logical name of the Dataverse table.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
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

### System.Object

An integer-compatible Dataverse @odata.count value.

## NOTES

This compatibility command uses the current default connection.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
