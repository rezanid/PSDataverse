namespace PSDataverse;

using System;
using System.Management.Automation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using PSDataverse.Auth;

[Cmdlet(VerbsCommunications.Connect, "Dataverse", DefaultParameterSetName = "AuthResult")]
public class ConnectDataverseCmdlet : DataverseCmdlet
{
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "AuthResult")]
    public AuthenticationResult AuthResult { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "AuthResult")]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "OnPremise")]
    public string Url { get; set; }

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "String")]
    public string ConnectionString { get; set; }

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "AuthParams")]
    public AuthenticationParameters ConnectionStringObject { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "OnPremise")]
    public SwitchParameter OnPremise { get; set; }

    [Parameter(DontShow = true, ParameterSetName = "String")]
    [Parameter(DontShow = true, ParameterSetName = "AuthParams")]
    [Parameter(DontShow = true, ParameterSetName = "AuthResult")]
    [Parameter(DontShow = true, ParameterSetName = "OnPremise")]
    public int Retry { get; set; }


    private static readonly object Lock = new();

    protected override void ProcessRecord()
    {
        var serviceProvider = (IServiceProvider)GetVariableValue(Globals.VariableNameServiceProvider);
        var authParams = ConnectionStringObject ??
            (string.IsNullOrWhiteSpace(ConnectionString) ?
            new AuthenticationParameters() :
            AuthenticationParameters.Parse(ConnectionString));

        var endpointUrl =
            string.IsNullOrWhiteSpace(Url) ?
            new Uri(authParams.Resource, UriKind.Absolute) :
            new Uri(Url, UriKind.Absolute);

        serviceProvider ??= InitializeServiceProvider(endpointUrl);

        if (OnPremise)
        {
            SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameConnectionString, "OnPremise", ScopedItemOptions.AllScope));
            SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessToken, string.Empty, ScopedItemOptions.AllScope));
            WriteInformation("Dynamics 365 (On-Prem) authenticated successfully.", ["dataverse"]);
            return;
        }

        // if previously authented, extract the account. It will be required for silent authentication.
        if (SessionState.PSVariable.GetValue(Globals.VariableNameAuthResult) is AuthenticationResult previouAuthResult)
        {
            authParams.Account = previouAuthResult.Account;
        }

        var authResult = AuthResult ?? HandleAuthentication(serviceProvider, authParams);
        if (authResult == null)
        {
            return;
        }

        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAuthResult, authResult, ScopedItemOptions.AllScope));
        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessToken, authResult.AccessToken, ScopedItemOptions.AllScope));
        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessTokenExpiresOn, authResult.ExpiresOn, ScopedItemOptions.AllScope));
        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameConnectionString, authParams, ScopedItemOptions.AllScope));

        WriteDebug("AccessToken: " + authResult.AccessToken);
        WriteInformation("Dataverse authenticated successfully.", ["dataverse"]);
    }

    private AuthenticationResult HandleAuthentication(
        IServiceProvider serviceProvider,
        AuthenticationParameters parameters)
    {
        var service = serviceProvider.GetService<AuthenticationService>();
        try
        {
            return service?.AuthenticateAsync(parameters, OnMessageForUser, CancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            WriteError(
                new ErrorRecord(
                    new InvalidOperationException("Dataverse authentication cancelled."), Globals.ErrorIdAuthenticationFailed, ErrorCategory.AuthenticationError, this));
            return null;
        }
        catch (Exception ex)
        {
            WriteError(
                new ErrorRecord(
                    new InvalidOperationException("Authentication failed. " + ex.ToString(), ex), Globals.ErrorIdAuthenticationFailed, ErrorCategory.AuthenticationError, this));
            return null;
        }
    }

    private void OnMessageForUser(string message) => WriteInformation(message, ["dataverse"]);

    private IServiceProvider InitializeServiceProvider(Uri baseUrl)
    {
        lock (Lock)
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
