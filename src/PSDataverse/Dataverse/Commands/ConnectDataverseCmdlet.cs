namespace PSDataverse;

using System;
using System.Management.Automation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using PSDataverse.Auth;

[Cmdlet(VerbsCommunications.Connect, "Dataverse", DefaultParameterSetName = "ConnectionString")]
public class ConnectDataverseCmdlet : DataverseCmdlet
{
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Url")]
    public string Url { get; set; }

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "ConnectionString")]
    public string ConnectionString { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "Url")]
    public SwitchParameter OnPremise { get; set; }

    private static readonly object Lock = new();

    protected override void ProcessRecord()
    {
        var authParams = string.IsNullOrWhiteSpace(ConnectionString) ?
            new AuthenticationParameters
            {
                Resource = Url
            } :
            AuthenticationParameters.Parse(ConnectionString);

        var endpointUrl =
            string.IsNullOrWhiteSpace(Url) ?
            new Uri(authParams.Resource, UriKind.Absolute) :
            new Uri(Url, UriKind.Absolute);

        var serviceProvider = CreateServiceProvider(endpointUrl);

        if (OnPremise)
        {
            ReplaceServiceProvider(serviceProvider);
            SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameIsOnPremise, true, ScopedItemOptions.AllScope));
            SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessToken, string.Empty, ScopedItemOptions.AllScope));
            WriteInformation("Dynamics 365 (On-Prem) authenticated successfully.", ["dataverse"]);
            return;
        }
        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameIsOnPremise, false, ScopedItemOptions.AllScope));

        // if previously authented, extract the account. It will be required for silent authentication.
        if (SessionState.PSVariable.GetValue(Globals.VariableNameAuthResult) is AuthenticationResult previouAuthResult)
        {
            authParams.Account = previouAuthResult.Account;
        }

        var authResult = HandleAuthentication(serviceProvider, authParams);
        if (authResult == null)
        {
            (serviceProvider as IDisposable)?.Dispose();
            return;
        }

        ReplaceServiceProvider(serviceProvider);
        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAuthResult, authResult, ScopedItemOptions.AllScope));
        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessToken, authResult.AccessToken, ScopedItemOptions.AllScope));
        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessTokenExpiresOn, authResult.ExpiresOn, ScopedItemOptions.AllScope));
        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameConnectionString, authParams, ScopedItemOptions.AllScope));

        WriteDebug($"Authenticated account '{authResult.Account?.Username ?? "application"}' until {authResult.ExpiresOn:u}.");
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

    private IServiceProvider CreateServiceProvider(Uri baseUrl)
    {
        var startup = new Startup(baseUrl, OnPremise ? "v9.1" : "v9.2");
        return startup.ConfigureServices(new ServiceCollection()).BuildServiceProvider();
    }

    private void ReplaceServiceProvider(IServiceProvider serviceProvider)
    {
        lock (Lock)
        {
            var previousServiceProvider = (IServiceProvider)GetVariableValue(Globals.VariableNameServiceProvider);
            SessionState.PSVariable.Set(
                new PSVariable(Globals.VariableNameServiceProvider, serviceProvider, ScopedItemOptions.AllScope));
            (previousServiceProvider as IDisposable)?.Dispose();
        }
    }
}
