namespace DataverseModule;

using DataverseModule.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using System;
using System.Management.Automation;

[Cmdlet(VerbsCommunications.Connect, "Dataverse")]
public class ConnectDataverseCmdlet : DataverseCmdlet
{
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "String")]
    public string ConnectionString { get; set; }

    [Parameter(Position = 1, Mandatory = false)]
    public int Retry { get; set; }

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Object")]
    public AuthenticationParameters ConnectionStringObject { get; set; }

    private readonly object _lock = new();

    protected override void ProcessRecord()
    {

        var authParams = ConnectionStringObject ?? AuthenticationParameters.Parse(ConnectionString);

        var serviceProvider = (IServiceProvider)GetVariableValue(Globals.VariableNameServiceProvider);
        if (serviceProvider == null)
        {
            serviceProvider = InitializeServiceProvider(new Uri(authParams.Resource, UriKind.Absolute));
        }

        //var authResult = AuthenticationHelper.Authenticate(dataverseCnnStr);
        AuthenticationResult authResult = null;
        try
        {
            authResult = serviceProvider.GetService<AuthenticationService>()?.AuthenticateAsync(authParams, OnMessageForUser, CancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            WriteInformation("Dataverse authentication cancelled.", new string[] { "dataverse" });
            return;
        }
        catch (System.Exception)
        {
            throw;
        }

        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessToken, authResult.AccessToken, ScopedItemOptions.AllScope));
        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessTokenExpiresOn, authResult.ExpiresOn, ScopedItemOptions.AllScope));
        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameConnectionString, authParams, ScopedItemOptions.AllScope));

        WriteDebug("AccessToken: " + authResult.AccessToken);
        WriteInformation("Dataverse authenticated successfully.", new string[] { "dataverse" });
        WriteObject("Dataverse authenticated successfully.");
        base.ProcessRecord();
    }

    private void OnMessageForUser(string message) => WriteInformation(message, new string[] { "dataverse" });

    private IServiceProvider InitializeServiceProvider(Uri baseUrl)
    {
        lock (_lock)
        {
            var serviceProvider = (IServiceProvider)GetVariableValue(Globals.VariableNameServiceProvider);
            if (serviceProvider == null)
            {
                var startup = new Startup(baseUrl);
                serviceProvider = startup.ConfigureServices(new ServiceCollection()).BuildServiceProvider();
                SessionState.PSVariable.Set(
                    new PSVariable(Globals.VariableNameServiceProvider, serviceProvider, ScopedItemOptions.AllScope));
            }
            return serviceProvider;
        }
    }
}
