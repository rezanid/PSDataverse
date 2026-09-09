namespace PSDataverse.Tests;

using System.Net;
using FluentAssertions;
using Microsoft.Identity.Client;
using PSDataverse.Auth;

public class AuthenticationServiceTests
{
    [Fact]
    public async Task TenantDiscoveryAlsoPopulatesAuthorityBeforeAuthentication()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        response.Headers.Location = new Uri("https://login.microsoftonline.com/tenant-id/oauth2/authorize");
        var handler = new TestHttpMessageHandler().Enqueue(response);
        using var client = TestHttpInfrastructure.CreateClient(handler);
        var authenticator = new RecordingAuthenticator();
        var service = new AuthenticationService(authenticator, new TestHttpClientFactory(client));
        var parameters = new AuthenticationParameters
        {
            Resource = "https://example.crm.dynamics.com/",
            ClientId = "client-id",
            Scopes = ["https://example.crm.dynamics.com/.default"]
        };

        _ = await service.AuthenticateAsync(parameters);

        authenticator.Parameters!.Tenant.Should().Be("tenant-id");
        authenticator.Parameters.Authority.Should().Be("https://login.microsoftonline.com/tenant-id");
        handler.Requests.Single().Uri.Should().Be("https://example.crm.dynamics.com/");
    }

    [Fact]
    public void BrokerAuthenticatorOnlyClaimsInteractiveWindowsFlows()
    {
        using var authenticator = new WamAuthenticator();
        var parameters = new AuthenticationParameters
        {
            Resource = "https://example.crm.dynamics.com/",
            ClientId = "client-id",
            Tenant = "tenant-id",
            UseBroker = true
        };

        authenticator.CanAuthenticate(parameters).Should().Be(OperatingSystem.IsWindows());
        parameters.ClientSecret = "secret";
        authenticator.CanAuthenticate(parameters).Should().BeFalse();
    }

    private sealed class RecordingAuthenticator : IAuthenticator
    {
        public IAuthenticator NextAuthenticator { get; set; } = null!;
        public AuthenticationParameters Parameters { get; private set; } = null!;
        public bool CanAuthenticate(AuthenticationParameters parameters) => true;

        public Task<AuthenticationResult> AuthenticateAsync(
            AuthenticationParameters parameters,
            Action<string> onMessageForUser = null!,
            CancellationToken cancellationToken = default)
        {
            Parameters = parameters;
            return Task.FromResult<AuthenticationResult>(null!);
        }

        public Task<AuthenticationResult> TryAuthenticateAsync(
            AuthenticationParameters parameters,
            Action<string> onMessageForUser = null!,
            CancellationToken cancellationToken = default)
            => AuthenticateAsync(parameters, onMessageForUser, cancellationToken);
    }
}
