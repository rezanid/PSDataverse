namespace PSDataverse.Auth;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

internal class AuthenticationService(
    IAuthenticator authenticator,
    IHttpClientFactory httpClientFactory)
{
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;

    public IAuthenticator Authenticator { get; } = authenticator;

    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationParameters authParams,
        Action<string> onMessageForUser = default,
        CancellationToken cancellationToken = default)
    {
        authParams = await EnsureTenantAsync(authParams);
        var current = Authenticator;
        while (current != null && !current.CanAuthenticate(authParams))
        {
            current = current.NextAuthenticator;
        }
        if (current == null)
        {
            throw new InvalidOperationException("Unable to detect required authentication flow. Please check the input parameters and try again.");
        }
        return await current?.AuthenticateAsync(authParams, onMessageForUser, cancellationToken);
    }

    private async Task<AuthenticationParameters> EnsureTenantAsync(AuthenticationParameters authParams)
    {
        if (string.IsNullOrEmpty(authParams.Tenant))
        {
            var url = authParams.Resource;
            using var httpClient = HttpClientFactory.CreateClient(Globals.DataverseHttpClientName);
            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url)).ConfigureAwait(false);
            var authUrl = response.Headers.Location;
            var tenantId = authUrl.AbsolutePath[1..authUrl.AbsolutePath.IndexOf('/', 1)];
            authParams.Tenant = tenantId;
        }
        return authParams;
    }
}
