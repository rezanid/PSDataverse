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
At the moment the module is not published on any package library. The easiest way to use this module is to download the latest released version and load the module using the following command.
```powershell
if (-not(Get-Module -ListAvailable -Name MigrationModule)) { 
  Import-Module .\MigrationModule.dll
}
```
# How to use
The first thing to do is to connect to your Dataverse environment `Connect-Dataverse` cmdlet. Currently there are two ways that you can connect, using a Client ID (aka Application ID) and a Client Password or using a Client ID and a certificate that you have installed in OS's certificate store. 

## Example 1 - Connecting to Dataverse using a client ID and a client certificate installed in certificate store. 
```powershell
Connect-Dataverse "authority=https://login.microsoftonline.com/<your-tenant-id>/oauth2/authorize;clientid=<your-client-id>;thumbprint=<thumbprint-of-your-certificate>;resource=https://<your-environment-name>.crm4.dynamics.com/"
```

## Example 2 - Connecting to Dataverse using a client ID and a client secret installed in certificate store. 
```powershell
Connect-Dataverse "authority=https://login.microsoftonline.com/<your-tenant-id>/oauth2/authorize;clientid=<your-client-id>;clientsecret=<your-client-secret>;resource=https://<your-environment-name>.crm4.dynamics.com/"
```

> 🚧 I am working on bringing the same connection string format as supported by [Xrm Tooling](https://docs.microsoft.com/en-us/dynamics365/customerengagement/on-premises/developer/xrm-tooling/use-powershell-cmdlets-xrm-tooling-connect?view=op-9-1). Ultimately you will be able to either provide a connection string as the only parameter or provide each property of the connection string, separately as a parameter.

After that you can send any number of operations to your Dataverse environment. Let's look at a simple operation.

**Example 3: Running a global action using piping**
 ```powershell
 @{Uri="WhoAmI"} | Send-DataverseOperation
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
'{"Uri":"WhoAmI"} | Send-DataverseOperation
```

> **ℹ NOTE**
> When the input is a Hashtable like object, it will be converted to JSON equivalent before sending to Dataverse. To have more control over the conversion to JSON, it is recommended to use the native `[ConvertTo-Json](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/convertto-json)` before Send-DataverseOperation.

**Example 4: Running a global action using piping and display the returned object**
Now, let's see how we can get to the of the 'Content' property, convert it to a PowerShell object and then display it as a list, all in one line.

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

**Example 5: Running a global action and accessing the result**
When the result is converted to an object, you can access any of the properties like any other PowerShell object.

```powershell
$whoAmI = ConvertTo-Json ([pscustomobject]@{Uri="WhoAmI"}) | Send-DataverseOperation | ConvertFrom-Json
Write-Host $whoAmI.UserId
```
The above example sends a WhoAmI request to the Dataverse and gets back the result. If you check carefully this is what happens in each step:
1. An operation is defined as a Hashtable i.e. `@{Uri="WhoAmI";Method="GET"}` and using `ConvertTo-Json` this Hashtable is converted to JSON, because at the moment `Send-Operation` only support operations in JSON format.
2. The operation is piped to `Send-DataverseOperation` that sends the operation to Dataverse and gets back the result.
3. The result of `Send-Operation` is then converted back to a Hashtable. The table will contain thres properties as per documentation. BusinessUnitId, UserId, and OrganizationId
4. The second line is just printing the UserId to the host.
