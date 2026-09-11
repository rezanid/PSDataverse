---
document type: cmdlet
external help file: PSDataverse.PowerShell-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Export-DataverseOptionSet
---

# Export-DataverseOptionSet

## SYNOPSIS

Returns English labels and values from Dataverse global option sets.

## SYNTAX

### __AllParameterSets

```
Export-DataverseOptionSet [-OptionSet] <string[]> [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Export-DataverseOptionSet retrieves each named GlobalOptionSetDefinition and emits its option values with localized English labels (language code 1033). Despite its historical name, the command writes objects to the pipeline rather than a file.

## EXAMPLES

### Example 1: Read one global option set

```powershell
Export-DataverseOptionSet -OptionSet new_projectstatus
```

Returns Value and Label properties.

### Example 2: Export labels to CSV

```powershell
'new_status','new_priority' | Export-DataverseOptionSet | Export-Csv ./options.csv -NoTypeInformation
```

Combines options from two global option sets in one CSV file.

## PARAMETERS

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

### -OptionSet

Enter one or more OptionSet names separated by commas.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
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

### System.String[]

You can pipe global option-set logical names.

## OUTPUTS

### System.Object

Objects containing Value and Label properties.

## NOTES

The current command selects language code 1033. Use Invoke-DataverseRequest for other localization requirements.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
