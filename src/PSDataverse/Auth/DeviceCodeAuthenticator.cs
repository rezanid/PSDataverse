namespace PSDataverse.Auth;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

internal class DeviceCodeAuthenticator : DelegatingAuthenticator
{
    public override async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationParameters parameters,
        Action<string> onMessageForUser = default,
        CancellationToken cancellationToken = default)
    {
        var app = await GetClientAppAsync(parameters, cancellationToken).ConfigureAwait(false);

        // Attempt to get a token silently from the cache
        //TODO: Check if accounts returned from GetAccountAsync is always null.
        var accounts = await app.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.FirstOrDefault();
        if (parameters.Account is not null)
        {
            try
            {
                var silentResult = await app.AcquireTokenSilent(parameters.Scopes, account).ExecuteAsync(cancellationToken).ConfigureAwait(false);
                if (silentResult != null) { return silentResult; }
            }
            catch (MsalUiRequiredException)
            {
                // Silent acquisition failed, user interaction required
            }
        }

        return await app.AsPublicClient().AcquireTokenWithDeviceCode(parameters.Scopes, callback =>
        {
            // Provide the user instructions
            onMessageForUser(callback.Message);
            return Task.CompletedTask;
        }).ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    public override bool CanAuthenticate(AuthenticationParameters parameters) =>
        parameters.UseDeviceFlow &&
        !string.IsNullOrEmpty(parameters.ClientId);
}
