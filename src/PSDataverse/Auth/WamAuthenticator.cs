namespace PSDataverse.Auth;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

internal sealed class WamAuthenticator : DelegatingAuthenticator
{
    private readonly AsyncDictionary<AuthenticationParameters, IPublicClientApplication> apps = new();

    public override bool CanAuthenticate(AuthenticationParameters parameters)
        => OperatingSystem.IsWindows() &&
           parameters.UseBroker &&
           !parameters.UseDeviceFlow &&
           string.IsNullOrWhiteSpace(parameters.ClientSecret) &&
           string.IsNullOrWhiteSpace(parameters.CertificateThumbprint);

    public override async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationParameters parameters,
        Action<string> onMessageForUser = default,
        CancellationToken cancellationToken = default)
    {
        var app = await apps.GetOrAddAsync(parameters, CreateApplicationAsync, cancellationToken).ConfigureAwait(false);
        var accounts = await app.GetAccountsAsync().ConfigureAwait(false);
        var account = parameters.Account ?? accounts.FirstOrDefault(candidate =>
            string.Equals(candidate.HomeAccountId?.TenantId, parameters.Tenant, StringComparison.OrdinalIgnoreCase))
            ?? accounts.FirstOrDefault();
        if (!parameters.ForceAuthentication && account is not null)
        {
            try
            {
                return await app.AcquireTokenSilent(parameters.Scopes, account)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (MsalUiRequiredException)
            {
                // Nothing usable in the cache; continue interactively.
            }
        }

        var builder = app.AcquireTokenInteractive(parameters.Scopes)
            .WithPrompt(Prompt.SelectAccount);
        if (!parameters.ForceAuthentication && account is not null)
        {
            builder = builder.WithAccount(account);
        }
        var parent = WindowHelper.GetConsoleOrTerminalWindow();
        if (parent != IntPtr.Zero)
        {
            builder = builder.WithParentActivityOrWindow(parent);
        }
        return await builder.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Task<IPublicClientApplication> CreateApplicationAsync(
        AuthenticationParameters parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var builder = PublicClientApplicationBuilder.Create(parameters.ClientId)
            .WithAuthority(parameters.Authority)
            .WithDefaultRedirectUri();
        builder = BrokerExtension.WithBroker(
            builder,
            new BrokerOptions(BrokerOptions.OperatingSystems.Windows));
        return Task.FromResult(builder.Build());
    }

    public override void Dispose()
    {
        apps.Dispose();
        base.Dispose();
    }
}
