---
document type: cmdlet
external help file: PSDataverse.PowerShell-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Invoke-DataverseUpdateMultiple
---

# Invoke-DataverseUpdateMultiple

## SYNOPSIS

Updates homogeneous Dataverse rows using concurrent UpdateMultiple chunks.

## SYNTAX

### ResolveLogicalName (Default)

```
Invoke-DataverseUpdateMultiple [-TableSetName] <string> [-Rows] <Object[]> [-ChunkSize <int>]
 [-MaxDop <int>] [-Connection <DataverseConnection>] [-ConnectionName <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### ExplicitLogicalName

```
Invoke-DataverseUpdateMultiple [-TableSetName] <string> [-TableLogicalName] <string>
 [-Rows] <Object[]> [-ChunkSize <int>] [-MaxDop <int>] [-Connection <DataverseConnection>]
 [-ConnectionName <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Invoke-DataverseUpdateMultiple divides rows into configurable chunks, verifies table support through the connection cache, and sends UpdateMultiple requests with bounded concurrency. Include each row primary ID. Web API UpdateMultiple does not support alternate keys.

## EXAMPLES

### Example 1: Update rows in bulk

```powershell
Invoke-DataverseUpdateMultiple new_projects new_project $rows -ChunkSize 100 -MaxDop 4
```

Updates homogeneous rows in concurrent chunks.

### Example 2: Capture failed source rows

```powershell
Invoke-DataverseUpdateMultiple new_projects new_project $rows -ErrorVariable bulkErrors -ErrorAction SilentlyContinue
```

The error TargetObject retains the failed chunk input rows.

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

Converted UpdateMultiple responses. Failures use DVERR-1020 with source-row context.

## NOTES

Unsupported tables stop with DVERR-1021 before any rows are sent.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
