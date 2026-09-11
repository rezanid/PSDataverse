---
document type: cmdlet
external help file: PSDataverse.PowerShell-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Invoke-DataverseUpsertMultiple
---

# Invoke-DataverseUpsertMultiple

## SYNOPSIS

Creates or updates homogeneous Dataverse rows using concurrent UpsertMultiple chunks.

## SYNTAX

### ResolveLogicalName (Default)

```
Invoke-DataverseUpsertMultiple [-TableSetName] <string> [-Rows] <Object[]> [-ChunkSize <int>]
 [-MaxDop <int>] [-Connection <DataverseConnection>] [-ConnectionName <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### ExplicitLogicalName

```
Invoke-DataverseUpsertMultiple [-TableSetName] <string> [-TableLogicalName] <string>
 [-Rows] <Object[]> [-ChunkSize <int>] [-MaxDop <int>] [-Connection <DataverseConnection>]
 [-ConnectionName <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Invoke-DataverseUpsertMultiple divides rows into configurable chunks, verifies that both CreateMultiple and UpdateMultiple are supported, and sends UpsertMultiple requests with bounded concurrency. Identify rows with primary IDs or alternate-key OData IDs as appropriate.

## EXAMPLES

### Example 1: Upsert rows in bulk

```powershell
Invoke-DataverseUpsertMultiple new_projects new_project $rows -ChunkSize 100 -MaxDop 4
```

Creates missing rows and updates existing rows.

### Example 2: Inspect support first

```powershell
Test-DataverseBulkOperationSupport new_project -Operation UpsertMultiple
```

Returns true only when both create and update multiple messages are available.

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

Converted UpsertMultiple responses. Failures use DVERR-1020 with source-row context.

## NOTES

Unsupported tables stop with DVERR-1021 before any rows are sent.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
