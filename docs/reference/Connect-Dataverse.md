---
document type: cmdlet
external help file: PSDataverse.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: PSDataverse
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Connect-Dataverse
---

# Connect-Dataverse

## SYNOPSIS

Authenticates to Dataverse and registers a reusable connection.

## SYNTAX

### ConnectionString (Default)

```
Connect-Dataverse [-ConnectionString] <string> [-Name <string>] [-ApiVersion <string>] [-NoDefault]
 [<CommonParameters>]
```

### Url

```
Connect-Dataverse [-Url] <string> [-OnPremise] [-UseSystemBrowser] [-Name <string>]
 [-ApiVersion <string>] [-NoDefault] [<CommonParameters>]
```

### Interactive

```
Connect-Dataverse [-Url] <string> -Interactive [-ClientId <string>] [-TenantId <string>]
 [-UseSystemBrowser] [-UseWebAccountManager] [-ForceAuthentication] [-RedirectUri <string>]
 [-Scopes <string[]>] [-Name <string>] [-ApiVersion <string>] [-NoDefault] [<CommonParameters>]
```

### IntegratedWindows

```
Connect-Dataverse [-Url] <string> -IntegratedWindowsAuthentication [-ClientId <string>]
 [-TenantId <string>] [-Username <string>] [-Scopes <string[]>] [-Name <string>]
 [-ApiVersion <string>] [-NoDefault] [<CommonParameters>]
```

### DeviceCode

```
Connect-Dataverse [-Url] <string> -DeviceCode [-ClientId <string>] [-TenantId <string>]
 [-RedirectUri <string>] [-Scopes <string[]>] [-Name <string>] [-ApiVersion <string>] [-NoDefault]
 [<CommonParameters>]
```

### ClientSecret

```
Connect-Dataverse [-Url] <string> -ClientSecret <securestring> -ClientId <string> -TenantId <string>
 [-Scopes <string[]>] [-Name <string>] [-ApiVersion <string>] [-NoDefault] [<CommonParameters>]
```

### ClientSecretProvider

```
Connect-Dataverse [-Url] <string> -ClientSecretProvider <scriptblock> -ClientId <string>
 -TenantId <string> [-Scopes <string[]>] [-Name <string>] [-ApiVersion <string>] [-NoDefault]
 [<CommonParameters>]
```

### Certificate

```
Connect-Dataverse [-Url] <string> -CertificateThumbprint <string> -ClientId <string>
 -TenantId <string> [-Scopes <string[]>] [-CertificateStoreName <StoreName>] [-Name <string>]
 [-ApiVersion <string>] [-NoDefault] [<CommonParameters>]
```

### AccessToken

```
Connect-Dataverse [-Url] <string> -AccessToken <securestring> [-ExpiresOn <DateTimeOffset>]
 [-Name <string>] [-ApiVersion <string>] [-NoDefault] [<CommonParameters>]
```

### TokenProvider

```
Connect-Dataverse [-Url] <string> -TokenProvider <scriptblock> [-Name <string>]
 [-ApiVersion <string>] [-NoDefault] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Connect-Dataverse creates a DataverseConnection that owns authentication, token refresh, and HTTP resources. Interactive authentication uses WAM by default on Windows and the system browser elsewhere. Device code, IWA, application secret, certificate, supplied token, token provider, on-premises, and connection-string flows are available through explicit parameter sets.

## EXAMPLES

### Example 1: Connect interactively

```powershell
$connection = Connect-Dataverse https://contoso.crm.dynamics.com -Interactive
```

Uses WAM on Windows or the system browser on other platforms.

### Example 2: Create a named device-code connection

```powershell
$dev = Connect-Dataverse $url -DeviceCode -Name dev -NoDefault
```

Registers dev without replacing the current default connection.

### Example 3: Use an application secret

```powershell
Connect-Dataverse $url -ClientId $appId -TenantId $tenantId -ClientSecret (Read-Host -AsSecureString)
```

Authenticates a confidential application without placing the secret in command history.

## PARAMETERS

### -AccessToken

A bearer access token stored as a SecureString. Supply ExpiresOn when the token lifetime differs from the default.

```yaml
Type: System.Security.SecureString
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: AccessToken
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ApiVersion

The Dataverse Web API version. The default is v9.2.

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

### -CertificateStoreName

The Windows certificate store containing the client certificate. The default is My.

```yaml
Type: System.Security.Cryptography.X509Certificates.StoreName
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Certificate
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -CertificateThumbprint

The thumbprint of the certificate used for application authentication.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Thumbprint
ParameterSets:
- Name: Certificate
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ClientId

The Microsoft Entra application (client) ID. Interactive flows use the PSDataverse public-client ID by default.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Interactive
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: IntegratedWindows
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: DeviceCode
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ClientSecret
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ClientSecretProvider
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Certificate
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ClientSecret

The application secret stored as a SecureString.

```yaml
Type: System.Security.SecureString
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ClientSecret
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ClientSecretProvider

A script block that returns exactly one SecureString, PSCredential, or non-empty string containing the application secret.

```yaml
Type: System.Management.Automation.ScriptBlock
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ClientSecretProvider
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ConnectionString

A PSDataverse or XRM-style connection string. Duplicate keys are rejected.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- CdsConnectionString
- DataverseConnectionString
ParameterSets:
- Name: ConnectionString
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -DeviceCode

Uses device-code authentication and displays instructions in the host.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: DeviceCode
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ExpiresOn

The UTC expiration time of a supplied access token.

```yaml
Type: System.DateTimeOffset
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: AccessToken
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ForceAuthentication

Bypasses silent account selection and requests an interactive account choice.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Interactive
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -IntegratedWindowsAuthentication

Uses Integrated Windows Authentication. This Windows-only flow is intended for compatible federated identities and does not support managed Entra-only users.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- IntegratedSecurity
ParameterSets:
- Name: IntegratedWindows
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Interactive

Uses WAM on supported Windows systems or the system browser elsewhere.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Interactive
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Name

The registry name for the connection. The default is default.

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

### -NoDefault

Registers the connection without making it the default connection.

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

### -OnPremise

Uses Windows credentials with an on-premises Dataverse deployment.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- OnPremises
ParameterSets:
- Name: Url
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -RedirectUri

The public-client redirect URI used by interactive or device-code authentication.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Interactive
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: DeviceCode
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Scopes

OAuth scopes to request. The Dataverse resource `.default` scope is used when omitted.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Interactive
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: IntegratedWindows
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: DeviceCode
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ClientSecret
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ClientSecretProvider
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Certificate
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TenantId

The Microsoft Entra tenant ID or verified tenant domain.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Tenant
ParameterSets:
- Name: Interactive
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: IntegratedWindows
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: DeviceCode
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ClientSecret
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ClientSecretProvider
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Certificate
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TokenProvider

A script block accepting a CancellationToken and returning a token value with an expiration time.

```yaml
Type: System.Management.Automation.ScriptBlock
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: TokenProvider
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Url

The Dataverse environment URL.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ServiceUrl
- EnvironmentUrl
ParameterSets:
- Name: Url
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Interactive
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: IntegratedWindows
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: DeviceCode
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ClientSecret
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ClientSecretProvider
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Certificate
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: AccessToken
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: TokenProvider
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Username

The user principal name supplied to Integrated Windows Authentication.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: IntegratedWindows
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -UseSystemBrowser

Uses the system browser instead of WAM for interactive authentication.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Interactive
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Url
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -UseWebAccountManager

Explicitly selects Windows Web Account Manager. The alias is UseWam.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- UseWam
ParameterSets:
- Name: Interactive
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

### PSDataverse.DataverseConnection

A PSDataverse.DataverseConnection object. Tokens and secrets are not exposed as public properties.

## NOTES

Prefer Interactive/WAM for managed user identities. Retained IWA support is Windows-only and is unsuitable for managed Entra-only identities or MFA requirements.

## RELATED LINKS

- [PSDataverse README](https://github.com/rezanid/PSDataverse#readme)
- [Migration guide](https://github.com/rezanid/PSDataverse/blob/main/MIGRATION.md)
