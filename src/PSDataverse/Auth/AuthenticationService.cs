namespace PSDataverse.Auth;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

internal class AuthenticationService(
    IAuthenticator authenticator,
    HttpClientFactory httpClientFactory)
{
    private HttpClientFactory HttpClientFactory { get; } = httpClientFactory;

    public IAuthenticator Authenticator { get; } = authenticator;

    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationParameters authParams,
        Action<string> onMessageForUser = default,
        CancellationToken cancellationToken = default)
    {
        var current = Authenticator;
        while (current != null && !current.CanAuthenticate(authParams))
        {
            current = current.NextAuthenticator;
        }
        if (current == null)
        {
            throw new InvalidOperationException("Unable to detect required authentication flow. Please check the input parameters and try again.");
        }
        authParams = await EnsureTenantAsync(authParams);
        return await current?.AuthenticateAsync(authParams, onMessageForUser, cancellationToken);
    }

    private async Task<AuthenticationParameters> EnsureTenantAsync(AuthenticationParameters authParams)
    {
        if (string.IsNullOrEmpty(authParams.Tenant))
        {
            var url = authParams.Resource;
            using var httpClient = HttpClientFactory.CreateClient("Dataverse");
            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url)).ConfigureAwait(false);
            var authUrl = response.Headers.Location;
            var tenantId = authUrl.AbsolutePath[1..authUrl.AbsolutePath.IndexOf('/', 1)];
            authParams.Tenant = tenantId;
        }
        return authParams;
    }
}
