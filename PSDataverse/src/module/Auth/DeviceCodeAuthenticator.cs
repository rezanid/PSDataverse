namespace PSDataverse.Auth;

using System;
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
        IClientApplicationBase app = GetClient(parameters);

        //TODO: Implement logging
        //ServiceClientTracing.Information($"[DeviceCodeAuthenticator] Calling AcquireTokenWithDeviceCode - Scopes: '{string.Join(", ", parameters.Scopes)}'");

        return await app.AsPublicClient().AcquireTokenWithDeviceCode(parameters.Scopes, deviceCodeResult =>
        {
            onMessageForUser(deviceCodeResult.Message);
            return Task.FromResult(0);
        }).ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    public override bool CanAuthenticate(AuthenticationParameters parameters) =>
        parameters.UseDeviceFlow &&
        !string.IsNullOrEmpty(parameters.ClientId);
}
