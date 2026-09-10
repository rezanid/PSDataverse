namespace PSDataverse.Auth;

using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal class IntegratedAuthenticator : DelegatingAuthenticator
{
    private readonly AsyncDictionary<AuthenticationParameters, IPublicClientApplication> apps = new();

    public override async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationParameters parameters,
        Action<string> onMessageForUser = default,
        CancellationToken cancellationToken = default)
    {
        AuthenticationResult result = null;
        var app = await apps.GetOrAddAsync(
            parameters, 
            async (k, ct) => (await GetClientAppAsync(k, ct)).AsPublicClient(),
            cancellationToken);
        var accounts = await app.GetAccountsAsync().ConfigureAwait(false);
        var firstAccount = parameters.Account ?? accounts.FirstOrDefault();
        if (!parameters.ForceAuthentication && firstAccount is not null)
        {
            try
            {
                result = await app.AcquireTokenSilent(parameters.Scopes, firstAccount)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (MsalUiRequiredException)
            {
                // Nothing usable in the cache; continue interactively.
            }
        }

        if (result is not null)
        {
            return result;
        }

        try
        {
            var phwnd = WindowHelper.GetConsoleOrTerminalWindow();
            var builder = app.AcquireTokenInteractive(parameters.Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .WithUseEmbeddedWebView(false);
            if (!parameters.ForceAuthentication && firstAccount is not null)
            {
                builder = builder.WithAccount(firstAccount);
            }
            if (phwnd != IntPtr.Zero)
            {
                builder = builder.WithParentActivityOrWindow(phwnd);
            }
            return await builder.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MsalException ex) when (
            ex.ErrorCode == "authentication_canceled" ||
            ex.ErrorCode == "access_denied" ||
            ex.ErrorCode == "user_canceled")
        {
            throw new OperationCanceledException("User cancelled Dataverse authentication.", ex, cancellationToken);
        }
        catch (MsalException ex)
        {
            onMessageForUser?.Invoke(ex.Message);
            throw;
        }
    }
    public override bool CanAuthenticate(AuthenticationParameters parameters)
        => !parameters.UseBroker && (parameters.UseCurrentUser || parameters.IsUncertainAuthFlow());

    public override void Dispose()
    {
        apps.Dispose();
        base.Dispose();
    }
}
