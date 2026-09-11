---
document type: cmdlet
external help file: PSDataverse.PowerShell-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Invoke-DataverseCreateMultiple
---

# Invoke-DataverseCreateMultiple

## SYNOPSIS

Creates homogeneous Dataverse rows using concurrent CreateMultiple chunks.

## SYNTAX

### ResolveLogicalName (Default)

```
Invoke-DataverseCreateMultiple [-TableSetName] <string> [-Rows] <Object[]> [-ChunkSize <int>]
 [-MaxDop <int>] [-Connection <DataverseConnection>] [-ConnectionName <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### ExplicitLogicalName

```
Invoke-DataverseCreateMultiple [-TableSetName] <string> [-TableLogicalName] <string>
 [-Rows] <Object[]> [-ChunkSize <int>] [-MaxDop <int>] [-Connection <DataverseConnection>]
 [-ConnectionName <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Invoke-DataverseCreateMultiple divides rows into configurable chunks, verifies table support through the connection cache, and sends multiple CreateMultiple requests with bounded concurrency. A standard-table chunk is transactional.

## EXAMPLES

### Example 1: Create rows in concurrent chunks

```powershell
Invoke-DataverseCreateMultiple new_projects $rows -ChunkSize 100 -MaxDop 4
```

Resolves the logical name once and submits CreateMultiple chunks.

### Example 2: Avoid table-name resolution

```powershell
Invoke-DataverseCreateMultiple new_projects new_project $rows -ChunkSize 250 -MaxDop 2
```

Supplies both entity-set and logical names explicitly.

## PARAMETERS

### -ChunkSize

The maximum number of rows in each CreateMultiple, UpdateMultiple, or UpsertMultiple request.

```yaml
Type: System.Int32
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

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- cf
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

### -MaxDop

The client-side concurrency ceiling. Zero uses the module default; Dataverse can lower it with its DOP hint.

```yaml
Type: System.Int32
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

### -Rows

The rows to submit. Every row in a request must target the same table.

```yaml
Type: System.Object[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ExplicitLogicalName
  Position: 2
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ResolveLogicalName
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TableLogicalName

The singular table logical name. Supplying it avoids entity-name resolution.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ExplicitLogicalName
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
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

### -WhatIf

Runs the command in a mode that only reports what would happen without performing the actions.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- wi
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

### None

Pass rows using the Rows parameter.

## OUTPUTS

### System.Object

Converted CreateMultiple responses. Failures use DVERR-1020 with source-row context.

## NOTES

Unsupported tables stop with DVERR-1021 before any rows are sent. Use `$batch when the table or workload is unsuitable for CreateMultiple.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
