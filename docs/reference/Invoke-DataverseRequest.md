---
document type: cmdlet
external help file: PSDataverse.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Invoke-DataverseRequest
---

# Invoke-DataverseRequest

## SYNOPSIS

Sends one or more low-level Dataverse Web API operations with bounded concurrency.

## SYNTAX

### Object (Default)

```
Invoke-DataverseRequest [-InputObject] <psobject> [[-BatchSize] <int>] [[-MaxDop] <int>]
 [-OutputTable] [-AutoPaginate] [-OutputOrder <string>] [-Connection <DataverseConnection>]
 [-ConnectionName <string>] [<CommonParameters>]
```

### Operation

```
Invoke-DataverseRequest [-InputOperation] <Operation`1[string]> [[-BatchSize] <int>]
 [[-MaxDop] <int>] [-OutputTable] [-AutoPaginate] [-OutputOrder <string>]
 [-Connection <DataverseConnection>] [-ConnectionName <string>] [<CommonParameters>]
```

### Request

```
Invoke-DataverseRequest [-Uri] <string> [[-BatchSize] <int>] [[-MaxDop] <int>] [-OutputTable]
 [-AutoPaginate] [-Method <string>] [-Body <Object>] [-Headers <IDictionary>] [-ContentId <string>]
 [-OutputOrder <string>] [-Connection <DataverseConnection>] [-ConnectionName <string>]
 [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Invoke-DataverseRequest is the low-level PSDataverse request engine. It accepts direct request parameters, operation dictionaries, or strongly typed operations. Pipelines use bounded concurrency, honor Dataverse DOP hints, support optional `$batch` envelopes and pagination, and emit results in completion order unless input order is requested.

## EXAMPLES

### Example 1: Retrieve rows

```powershell
Invoke-DataverseRequest -Uri 'accounts?$select=name&$top=5'
```

Sends a GET request.

### Example 2: Create a row

```powershell
Invoke-DataverseRequest -Uri accounts -Method POST -Body @{ name = 'Contoso' }
```

Serializes the body as JSON and sends one POST.

### Example 3: Send transactional batches concurrently

```powershell
$operations | Invoke-DataverseRequest -BatchSize 20 -MaxDop 5
```

Builds change sets of 20 operations and runs up to five envelopes concurrently.

### Example 4: Preserve pipeline order

```powershell
$operations | Invoke-DataverseRequest -MaxDop 20 -OutputOrder Input
```

Buffers completed responses until earlier inputs can be emitted.

## PARAMETERS

### -AutoPaginate

Follows every @odata.nextLink and emits each response page.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 4
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -BatchSize

The number of operations placed in each transactional `$batch` change set. Zero sends individual requests.

```yaml
Type: System.Int32
DefaultValue: ''
SupportsWildcards: false
Aliases:
- BatchCapacity
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

### -Body

The request or row body. PowerShell objects and dictionaries are serialized as JSON.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Request
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

### -ContentId

A caller-defined identifier used to correlate the operation with its response or error.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Request
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Headers

Additional HTTP request headers.

```yaml
Type: System.Collections.IDictionary
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Request
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -InputObject

A pipeline object or dictionary describing an operation.

```yaml
Type: System.Management.Automation.PSObject
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Object
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -InputOperation

A strongly typed PSDataverse operation supplied through the pipeline.

```yaml
Type: PSDataverse.Dataverse.Model.Operation`1[System.String]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Operation
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
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
Aliases:
- ThrottleLimit
ParameterSets:
- Name: (All)
  Position: 2
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Method

The HTTP method. GET is inferred when no body is present and POST when a body is present.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Request
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -OutputOrder

Controls whether results are emitted in Completion order for throughput or buffered into Input order.

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

### -OutputTable

Formats operation responses as a PowerShell table through the host.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 3
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Uri

A relative Dataverse Web API URI or an absolute next-link URI.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Request
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
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

### System.Management.Automation.PSObject

Operation dictionaries and strongly typed operations can be supplied through the pipeline.

## OUTPUTS

### System.Object

PSDataverse operation or batch response envelopes.

## NOTES

Automatic retries are limited to replay-safe methods. POST, PATCH, DELETE, and write batches are sent once because a missing response does not prove the server rejected the write.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
