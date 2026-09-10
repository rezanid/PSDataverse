# Connect-Dataverse

## Syntax

```powershell
Connect-Dataverse [-ApiVersion <String>] [-Name <String>] [-NoDefault] -ConnectionString <String>
```

```powershell
Connect-Dataverse [-ApiVersion <String>] [-Name <String>] [-NoDefault] [-OnPremise] [-UseSystemBrowser] -Url <String>
```

```powershell
Connect-Dataverse [-ApiVersion <String>] [-ClientId <String>] [-ForceAuthentication] -Interactive [-Name <String>] [-NoDefault] [-RedirectUri <String>] [-Scopes <String[]>] [-TenantId <String>] [-UseSystemBrowser] [-UseWebAccountManager] -Url <String>
```

```powershell
Connect-Dataverse [-ApiVersion <String>] [-ClientId <String>] -IntegratedWindowsAuthentication [-Name <String>] [-NoDefault] [-Scopes <String[]>] [-TenantId <String>] [-Username <String>] -Url <String>
```

```powershell
Connect-Dataverse [-ApiVersion <String>] [-ClientId <String>] -DeviceCode [-Name <String>] [-NoDefault] [-RedirectUri <String>] [-Scopes <String[]>] [-TenantId <String>] -Url <String>
```

```powershell
Connect-Dataverse [-ApiVersion <String>] -ClientId <String> -ClientSecret <SecureString> [-Name <String>] [-NoDefault] [-Scopes <String[]>] -TenantId <String> -Url <String>
```

```powershell
Connect-Dataverse [-ApiVersion <String>] -ClientId <String> -ClientSecretProvider <ScriptBlock> [-Name <String>] [-NoDefault] [-Scopes <String[]>] -TenantId <String> -Url <String>
```

```powershell
Connect-Dataverse [-ApiVersion <String>] [-CertificateStoreName <StoreName>] -CertificateThumbprint <String> -ClientId <String> [-Name <String>] [-NoDefault] [-Scopes <String[]>] -TenantId <String> -Url <String>
```

```powershell
Connect-Dataverse -AccessToken <SecureString> [-ApiVersion <String>] [-ExpiresOn <DateTimeOffset>] [-Name <String>] [-NoDefault] -Url <String>
```

```powershell
Connect-Dataverse [-ApiVersion <String>] [-Name <String>] [-NoDefault] -TokenProvider <ScriptBlock> -Url <String>
```

## Parameters

| Name | Type | Required | Pipeline | Aliases |
|---|---|---:|---:|---|
| `-AccessToken` | `SecureString` | Yes | No |  |
| `-ApiVersion` | `String` | No | No |  |
| `-CertificateStoreName` | `StoreName` | No | No |  |
| `-CertificateThumbprint` | `String` | Yes | No | Thumbprint |
| `-ClientId` | `String` | Yes | No |  |
| `-ClientSecret` | `SecureString` | Yes | No |  |
| `-ClientSecretProvider` | `ScriptBlock` | Yes | No |  |
| `-ConnectionString` | `String` | Yes | No | CdsConnectionString, DataverseConnectionString |
| `-DeviceCode` | `SwitchParameter` | Yes | No |  |
| `-ExpiresOn` | `DateTimeOffset` | No | No |  |
| `-ForceAuthentication` | `SwitchParameter` | No | No |  |
| `-IntegratedWindowsAuthentication` | `SwitchParameter` | Yes | No | IntegratedSecurity |
| `-Interactive` | `SwitchParameter` | Yes | No |  |
| `-Name` | `String` | No | No |  |
| `-NoDefault` | `SwitchParameter` | No | No |  |
| `-OnPremise` | `SwitchParameter` | No | No | OnPremises |
| `-RedirectUri` | `String` | No | No |  |
| `-Scopes` | `String[]` | No | No |  |
| `-TenantId` | `String` | Yes | No | Tenant |
| `-TokenProvider` | `ScriptBlock` | Yes | No |  |
| `-Url` | `String` | Yes | No | ServiceUrl, EnvironmentUrl |
| `-Username` | `String` | No | No |  |
| `-UseSystemBrowser` | `SwitchParameter` | No | No |  |
| `-UseWebAccountManager` | `SwitchParameter` | No | No | UseWam |
