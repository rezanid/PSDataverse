# PSDataverse
PowerShell module that brings Dataverse's Web API to PowerShell 7 with features like piping, batching and more.

# How to install
At the moment the module is not published on any package library. The easier way is to download the latest released version and load the module using the following command.
```powershell
    if (-not(Get-Module -ListAvailable -Name MigrationModule)) { 
      Import-Module .\MigrationModule.dll
    }
```
# How to use
The first cmdlet that you would need is `Connect-Dataverse` to connect to your Dataverse environment. For example the following command uses a certificate that is installed in Windows to connect.
```powershell
    Connect-Dataverse "authority=https://login.microsoftonline.com/<your-tenant-id>/oauth2/authorize;clientid=<your-client-id>;thumbprint=<thumbprint-of-your-certificate>;resource=https://<your-environment-name>.crm4.dynamics.com/"

```
After that you can send any number of operations to your Dataverse environment. Let's look at a simpler operation.
```powershell
    $whoAmI = ConvertTo-Json ([pscustomobject]@{Uri="WhoAmI";Method="GET"}) | Send-DataverseOperation | ConvertFrom-Json
    Write-Host $whoAmI.UserId
```
The above example sends a WhoAmI request to the Dataverse and gets back the result. If you check carefully this is what happens in each step:
1. An operation is defined as a Hashtable i.e. `@{Uri="WhoAmI";Method="GET"}` and using `ConvertTo-Json` this Hashtable is converted to JSON, because at the moment `Send-Operation` only support operations in JSON format.
2. The operation is piped to `Send-DataverseOperation` that sends the operation to Dataverse and gets back the result.
3. The result of `Send-Operation` is then converted back to a Hashtable. The table will contain thres properties as per documentation. BusinessUnitId, UserId, and OrganizationId
4. The second line is just printing the UserId to the host.
