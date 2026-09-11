---
document type: cmdlet
external help file: PSDataverse.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Test-DataverseBulkOperationSupport
---

# Test-DataverseBulkOperationSupport

## SYNOPSIS

Tests whether a Dataverse table supports a multiple-row message.

## SYNTAX

### LogicalName (Default)

```
Test-DataverseBulkOperationSupport [-LogicalName] <string> [-Operation] <string> [-Detailed]
 [-Refresh] [-Connection <DataverseConnection>] [-ConnectionName <string>] [<CommonParameters>]
```

### TableSetName

```
Test-DataverseBulkOperationSupport [-TableSetName] <string> [-Operation] <string> [-Detailed]
 [-Refresh] [-Connection <DataverseConnection>] [-ConnectionName <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Test-DataverseBulkOperationSupport performs one combined metadata query for CreateMultiple and UpdateMultiple and caches the result for the owning connection. UpsertMultiple is supported only when both messages are available. Concurrent cold callers share one lookup.

## EXAMPLES

### Example 1: Test CreateMultiple support

```powershell
Test-DataverseBulkOperationSupport account -Operation CreateMultiple
```

Returns a Boolean result.

### Example 2: Inspect the cached capability matrix

```powershell
Test-DataverseBulkOperationSupport account -Operation UpsertMultiple -Detailed
```

Returns support flags, cache source, and check time.

### Example 3: Refresh a result by entity-set name

```powershell
Test-DataverseBulkOperationSupport -TableSetName accounts -Operation UpdateMultiple -Refresh -Detailed
```

Invalidates aliases and queries Dataverse again.

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

### -Detailed

Returns a structured result instead of the default Boolean value.

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

### -Operation

The bulk message to inspect: CreateMultiple, UpdateMultiple, or UpsertMultiple.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Refresh

Discards the cached capability result and queries Dataverse again.

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

You can pipe logical table names.

## OUTPUTS

### System.Boolean

A Boolean by default, or a PSDataverse.DataverseBulkOperationSupport object with Detailed.

### PSDataverse.DataverseBulkOperationSupport

A Boolean by default, or a PSDataverse.DataverseBulkOperationSupport object with Detailed.

## NOTES

The cache is connection-scoped and disposed with the connection. Bulk commands fail open when inspection itself is unavailable, but stop before sending rows for a definitive unsupported result.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
