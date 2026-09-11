---
document type: cmdlet
external help file: PSDataverse.PowerShell-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Get-DataverseRow
---

# Get-DataverseRow

## SYNOPSIS

Gets one row by ID or lists rows from a Dataverse table.

## SYNTAX

### List (Default)

```
Get-DataverseRow [-TableSetName] <string> [-Select <string[]>] [-Filter <string>] [-Top <int>]
 [-All] [-Connection <DataverseConnection>] [-ConnectionName <string>] [<CommonParameters>]
```

### ById

```
Get-DataverseRow [-TableSetName] <string> [-Id] <guid> [-Connection <DataverseConnection>]
 [-ConnectionName <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Get-DataverseRow addresses a Web API entity set. The ById parameter set retrieves one row. The List parameter set supports projection, filtering, a top limit, or automatic retrieval of all pages.

## EXAMPLES

### Example 1: Get one account

```powershell
$account = Get-DataverseRow accounts $accountId
```

Retrieves one account by its primary ID.

### Example 2: List active accounts

```powershell
Get-DataverseRow accounts -Select name,accountid -Filter 'statecode eq 0' -Top 100
```

Returns at most 100 projected rows.

### Example 3: Read every page

```powershell
Get-DataverseRow new_projects -All
```

Follows every Dataverse next link.

## PARAMETERS

### -All

Ignores Top and follows every Dataverse next link.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: List
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

### -Filter

An OData filter expression without the `$filter=` prefix.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: List
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Id

The Dataverse row identifier.

```yaml
Type: System.Guid
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ById
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Select

The columns or metadata properties to return.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: List
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

### -Top

The maximum number of rows requested when All is not specified.

```yaml
Type: System.Int32
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: List
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

This command does not accept pipeline input.

## OUTPUTS

### System.Object

Dataverse row objects. List requests emit individual rows rather than the response envelope.

## NOTES

Use -Connection or -ConnectionName to avoid relying on the session default when a script works with more than one environment.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
