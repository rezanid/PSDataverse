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
        authParams = await EnsureTenantAsync(authParams, cancellationToken).ConfigureAwait(false);
        var current = Authenticator;
        while (current != null && !current.CanAuthenticate(authParams))
        {
            current = current.NextAuthenticator;
        }
        if (current == null)
        {
            throw new InvalidOperationException("Unable to detect required authentication flow. Please check the input parameters and try again.");
        }
        return await current.AuthenticateAsync(authParams, onMessageForUser, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AuthenticationParameters> EnsureTenantAsync(
        AuthenticationParameters authParams,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(authParams.Tenant))
        {
            var url = authParams.Resource;
            using var httpClient = HttpClientFactory.CreateClient(Globals.DataverseHttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var authUrl = response.Headers.Location;
            if (authUrl is null)
            {
                throw new InvalidOperationException(
                    $"Unable to discover the tenant for '{url}': the response did not contain a redirect location.");
            }

            var secondSlash = authUrl.AbsolutePath.IndexOf('/', 1);
            if (secondSlash <= 1)
            {
                throw new InvalidOperationException(
                    $"Unable to discover the tenant from redirect location '{authUrl}'.");
            }

            var tenantId = authUrl.AbsolutePath[1..secondSlash];
            authParams.Tenant = tenantId;
        }
        return authParams;
    }
}
