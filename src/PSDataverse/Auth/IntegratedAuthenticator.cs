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
        var firstAccount = accounts.FirstOrDefault();
        try
        {
            result = await app.AcquireTokenSilent(parameters.Scopes, firstAccount)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MsalUiRequiredException)
        {
            // Nothing in cache for this account + scope.
            try
            {
                //var phwnd = Process.GetCurrentProcess().MainWindowHandle;
                var phwnd = WindowHelper.GetConsoleOrTerminalWindow();
                result = await app.AcquireTokenInteractive(parameters.Scopes)
                    .WithAccount(accounts.FirstOrDefault())
                    .WithParentActivityOrWindow(phwnd)
                    .WithPrompt(Prompt.SelectAccount)
                    .WithUseEmbeddedWebView(false)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);
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
                //TODO: Logging
            }
        }
        return result;
    }
    public override bool CanAuthenticate(AuthenticationParameters parameters)
        => !parameters.UseBroker && (parameters.UseCurrentUser || parameters.IsUncertainAuthFlow());

    public override void Dispose()
    {
        apps.Dispose();
        base.Dispose();
    }
}
