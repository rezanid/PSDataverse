---
document type: cmdlet
external help file: PSDataverse.PowerShell-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Get-DataverseTableMetadata
---

# Get-DataverseTableMetadata

## SYNOPSIS

Gets Dataverse table metadata by logical or entity-set name.

## SYNTAX

### LogicalName (Default)

```
Get-DataverseTableMetadata [-LogicalName] <string> [-IncludeColumns]
 [-Connection <DataverseConnection>] [-ConnectionName <string>] [<CommonParameters>]
```

### TableSetName

```
Get-DataverseTableMetadata [-TableSetName] <string> [-IncludeColumns]
 [-Connection <DataverseConnection>] [-ConnectionName <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Get-DataverseTableMetadata retrieves EntityDefinitions metadata. It accepts either a singular logical name or a Web API entity-set name and can include attribute metadata.

## EXAMPLES

### Example 1: Get table metadata

```powershell
Get-DataverseTableMetadata account
```

Returns account table metadata.

### Example 2: Resolve an entity-set name and include columns

```powershell
Get-DataverseTableMetadata -TableSetName accounts -IncludeColumns
```

Resolves accounts to account and returns its attributes.

## PARAMETERS

### -Connection

A DataverseConnection returned by Connect-Dataverse. Do not combine this parameter with ConnectionName.

```yaml
Type: PSDataverse.DataverseConnection
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

### -ConnectionName

The registered connection name to use. Omit it to use the default connection.

```yaml
Type: System.String
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

### -IncludeColumns

Returns attribute metadata in addition to table metadata.

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

### -LogicalName

The singular Dataverse table logical name, such as account.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: LogicalName
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TableSetName

The Web API entity-set name, such as accounts.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: TableSetName
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

### System.String

You can pipe logical names to the LogicalName parameter set.

## OUTPUTS

### System.Object

Dataverse table or attribute metadata objects.

## NOTES

Use -Connection or -ConnectionName to avoid relying on the session default when a script works with more than one environment.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
