# Table of Contents
* [What is PSDataverse](#what-is-psdataverse)
* [Features](#features)
* [How to install](#how-to-install)
* [How to use](#how-to-use)
  * [Connecting to Dataverse](#connecting-to-dataverse)
  * [Sending operations to Dataverse](#sending-operations-to-dataverse)

# What is PSDataverse?
PSDataverse is a PowerShell module that brings Dataverse's Web API to PowerShell 7+ with features like piping, batching and more. It is designed with ease-of-use and performance in mind and follows the patterns of native PowerShell cmdlets to play nicely with other modules.

# Features
* Securely connect to Dataverse.
* Supports bacthing.
* Supports parallelism.
* Automatically reconnects when authentication token is about to expire.
* Enhanced pipeline support (accepts different data types as input and emits responses to the pipeline).
* Automatic wait-and-retry for transient errors by default.
* Repects throttling data sent by Dataverse.
* Does not hide the response sent back by Dataverse.

# How to install
You can install the [PSDataverse module directly from PowerShell Gallery](https://www.powershellgallery.com/packages/PSDataverse) using the following command
```powershell
Install-Module -Name PSDataverse
```

As an alternative, you can also download the dll and module or clone the repository and build it locally. After that, to import the module to your current session you can run the following command.

```powershell
if (-not(Get-Module -ListAvailable -Name MigrationModule)) { 
  Import-Module .\PSDataverse.psd1
}
```

> **NOTE!**
> PSDataverse is a hybrid module that is a mix of PSDataverse.dll and PSDataverse.psd1 module definition. Only the commands that made more sense to be implemented as binary are included in the dll, and the rest of the implementation is done using PowerShell language.

# How to use
The first thing to do is to connect to your Dataverse environment using `Connect-Dataverse` cmdlet. Currently there are three ways that you can connect: 
* Using a Client ID (aka Application ID) and a Client Password
* Using a Client ID and a certificate that you have installed in OS's certificate store
* Using a device authentication flow (interactive login)

## Connecting to Dataverse

**Example 1 - Connecting to Dataverse using a client ID and a client certificate installed in certificate store.**
```powershell
Connect-Dataverse "authority=https://login.microsoftonline.com/<your-tenant-id>/oauth2/authorize;clientid=<your-client-id>;thumbprint=<thumbprint-of-your-certificate>;resource=https://<your-environment-name>.crm4.dynamics.com/"
```

**Example 2 - Connecting to Dataverse using a client ID and a client secret.**
```powershell
Connect-Dataverse "authority=https://login.microsoftonline.com/<your-tenant-id>/oauth2/authorize;clientid=<your-client-id>;clientsecret=<your-client-secret>;resource=https://<your-environment-name>.crm4.dynamics.com/"
```

**Example 3 - Connecting to Dataverse using device authentication flow.**
```powershell
Connect-Dataverse "authority=https://login.microsoftonline.com/<your-tenant-id>/oauth2/authorize;clientid=1950a258-227b-4e31-a9cf-717495945fc2;device=true;resource=https://<your-environment-name>.crm4.dynamics.com/" -InformationAction Continue
```
When you run the above command, a message like the following will be printed in the console, and you just need to do what is asked. After that, you will be prompted to use your credentials and that's it.
```
To sign in, use a web browser to open the page https://microsoft.com/devicelogin and enter the code CSPUJ9S7K to authenticate.
```
This is the easiest way to log in, when your just need to do ad-hoc operations.

> [!NOTE]
> 
> For any of first two examples to work you need an application user in your Power Platform environment. To learn how to create an application user, please read the following article from the official documentation: [Manage application users in the Power Platform admin center](https://docs.microsoft.com/en-us/power-platform/admin/manage-application-users).

The third one is using a wellknown client id, but if you want you can also use the client id of your own app registration. If you wish to use your own app registration for device authentication flow, you will need to enable "Allow public client flows" for your app registration. 

After connecting to the Dataverse, you can send any number of operations to your Dataverse environment. If the authentication expires, PSDataverse will automatically reauthenticate behind the scene. 

## Sending operations to Dataverse

Let's look at a simple operation.

**Example 1: Running a global action using piping**
 ```powershell
 @{Uri="WhoAmI"} | Send-DataverseOperation
 ```
 Or:
 ```powershell
 Send-DataverseOperation @{Uri="WhoAmI"}
 ```
 
This will result in an OperationResponse like the following:

```
ContentId  :
Content    : {"@odata.context":"https://yourdynamic-environment.crm4.dynamics.com/api/data/v9.2/$metadata#Microsoft.Dy
             namics.CRM.WhoAmIResponse","BusinessUnitId":"6f202e6c-e471-ec11-8941-000d3adf0002","UserId":"88057198-a9b1
             -ec11-9840-00567ab5c181","OrganizationId":"e34c95a5-f34c-430c-a05e-a23437e5b9fa"}
Error      :
Headers    : {[Cache-Control, no-cache], [x-ms-service-request-id, c9af118d-b483-48c8-887a-baa4de679bf,9b44cd2f-d34f-4
             908-bed4-edc12c44857d], [Set-Cookie, ARRAffinity=49fcec1e1d435e207a47447f2a47260b469a63eb4e69bdcc0610e04c1
             24a7f15; domain=yourdynamic-environment.crm4.dynamics.com; path=/; secure; HttpOnly], [Strict-Transport-S
             ecurity, max-age=31536000; includeSubDomains]…}
StatusCode : OK
```

You can always see the original headers and status code from dynamics. If there's any error, it will be reflected in 'Error' property. The most important property that you will often need is the 'Content' that includes the payload in JSON format.

The following command will have exactly the same result, but it is sending a string which is considered as JSON.
```powershell
'{"Uri":"WhoAmI"}' | Send-DataverseOperation
```
or:
```powershell
Send-DataverseOperation '{"Uri":"WhoAmI"}'
```


> **ℹ NOTE**
> When the input is a Hashtable like object, it will be converted to JSON equivalent before sending to Dataverse. To have more control over the conversion to JSON, it is recommended to use the native `[ConvertTo-Json](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/convertto-json)` before Send-DataverseOperation.

**Example 2: Running a global action using piping and display the returned object**

Now, let's see how we can get to the 'Content' property, convert it to a PowerShell object and then display it as a list, all in one line.

```powershell
@{Uri="WhoAmI"} | Send-DataverseOperation | select -ExpandProperty Content | ConvertFrom-Json | Format-List
```

This will reult in the following output:

```
@odata.context : https://helloworld.crm4.dynamics.com/api/data/v9.2/$metadata#Microsoft.Dynamics.CRM.WhoA
                 mIResponse
BusinessUnitId : 6f202e6c-e471-ec11-8941-000d3adf0002
UserId         : 88057198-a9b1-ec11-9840-00567ab5c181
OrganizationId : e34c95a5-f34c-430c-a05e-a23437e5b9fa
```

**Example 3: Running a global action and accessing the result**

When the result is converted to an object, you can access any of the properties like any other PowerShell object.

```powershell
$whoAmI = ConvertTo-Json ([pscustomobject]@{Uri="WhoAmI"}) | Send-DataverseOperation | ConvertFrom-Json
Write-Host $whoAmI.UserId
```
The above example sends a WhoAmI request to the Dataverse and gets back the result. If you check carefully this is what happens in each step:
1. An operation is defined as a Hashtable i.e. `@{Uri="WhoAmI";Method="GET"}` and using `ConvertTo-Json` this Hashtable is converted to JSON.
2. The operation is piped to `Send-DataverseOperation` that sends the operation to Dataverse and gets back the result.
3. The result of `Send-Operation` is then converted back to a Hashtable. The table will contain three properties as per documentation. BusinessUnitId, UserId, and OrganizationId
4. The second line is just printing the UserId to the host.

# Status

[![PSScriptAnalyzer](https://github.com/rezanid/PSDataverse/actions/workflows/powershell.yml/badge.svg)](https://github.com/rezanid/PSDataverse/actions/workflows/powershell.yml)
