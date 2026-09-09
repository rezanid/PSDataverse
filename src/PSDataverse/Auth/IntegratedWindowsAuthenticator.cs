namespace PSDataverse.Auth;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

internal sealed class IntegratedWindowsAuthenticator : DelegatingAuthenticator
{
    public override bool CanAuthenticate(AuthenticationParameters parameters)
        => parameters.UseIntegratedWindowsAuthentication;

    public override async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationParameters parameters,
        Action<string> onMessageForUser = default,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Integrated Windows Authentication is only supported on Windows. Use -Interactive or -DeviceCode on this platform.");
        }
        var app = (await GetClientAppAsync(parameters, cancellationToken).ConfigureAwait(false)).AsPublicClient()
            ?? throw new InvalidOperationException("Integrated Windows Authentication requires a public client application.");
        var builder = app.AcquireTokenByIntegratedWindowsAuth(parameters.Scopes);
        if (!string.IsNullOrWhiteSpace(parameters.Username))
        {
            builder = builder.WithUsername(parameters.Username);
        }
        return await builder.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
}
