---
document type: cmdlet
external help file: PSDataverse.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Set-DataverseDefaultConnection
---

# Set-DataverseDefaultConnection

## SYNOPSIS

Selects the default registered Dataverse connection.

## SYNTAX

### __AllParameterSets

```
Set-DataverseDefaultConnection [-Name] <string> [-PassThru] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Set-DataverseDefaultConnection changes which registered connection is used when commands omit Connection and ConnectionName.

## EXAMPLES

### Example 1: Switch the default environment

```powershell
Set-DataverseDefaultConnection production
```

Makes the registered production connection the default.

### Example 2: Return the selected connection

```powershell
Set-DataverseDefaultConnection dev -PassThru
```

Changes the default and emits the connection object.

## PARAMETERS

### -Name

The exact registered connection name to make default.

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
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PassThru

Returns the affected connection or updated row.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
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

### System.Object

Objects with a Name property can bind from the pipeline.

## OUTPUTS

### PSDataverse.DataverseConnection

A DataverseConnection when PassThru is specified; otherwise no output.

## NOTES

Use -Connection or -ConnectionName to avoid relying on the session default when a script works with more than one environment.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
