---
document type: cmdlet
external help file: PSDataverse.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Disconnect-Dataverse
---

# Disconnect-Dataverse

## SYNOPSIS

Disposes one or all registered Dataverse connections.

## SYNTAX

### Name (Default)

```
Disconnect-Dataverse [[-Name] <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### All

```
Disconnect-Dataverse -All [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Disconnect-Dataverse removes connections from the session registry and disposes their HTTP, authentication, token-lock, and capability-cache resources. With no name, it disconnects the current default connection.

## EXAMPLES

### Example 1: Disconnect the default connection

```powershell
Disconnect-Dataverse
```

Disposes the current default connection.

### Example 2: Disconnect every connection

```powershell
Disconnect-Dataverse -All -Confirm:$false
```

Disposes and removes every registered connection.

## PARAMETERS

### -All

Disconnects every registered Dataverse connection.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: All
  Position: Named
  IsRequired: true
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

### -Name

The registered connection name. Omit it to select the default connection.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Name
  Position: 0
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
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

### System.Object

Objects with a Name property can bind that property from the pipeline.

## OUTPUTS

### System.Object

No output.

## NOTES

Use -Connection or -ConnectionName to avoid relying on the session default when a script works with more than one environment.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
