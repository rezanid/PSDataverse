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
        try
        {
            return await builder.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MsalException exception) when (CreateActionableException(exception) is { } actionable)
        {
            throw actionable;
        }

    }

    internal static InvalidOperationException CreateActionableException(MsalException exception)
        => exception.ErrorCode switch
        {
            "integrated_windows_auth_not_supported_managed_user" => new InvalidOperationException(
                "Integrated Windows Authentication only supports federated, Active Directory-backed users. " +
                "This account is a managed Microsoft Entra user; use -Interactive (recommended) or -DeviceCode instead.",
                exception),
            "unknown_user" => new InvalidOperationException(
                "Integrated Windows Authentication could not identify a supported domain identity. " +
                "Specify -Username for a federated user, or use -Interactive (recommended) or -DeviceCode instead.",
                exception),
            _ => null
        };
}
