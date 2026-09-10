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

    [Theory]
    [InlineData(
        "integrated_windows_auth_not_supported_managed_user",
        "only supports federated, Active Directory-backed users")]
    [InlineData("unknown_user", "could not identify a supported domain identity")]
    public void IntegratedWindowsFailuresProvideActionableGuidance(string errorCode, string expectedMessage)
    {
        var source = new MsalClientException(errorCode, "opaque MSAL failure");

        var result = IntegratedWindowsAuthenticator.CreateActionableException(source);

        result.Should().NotBeNull();
        result.Message.Should().Contain(expectedMessage);
        result.Message.Should().Contain("-Interactive");
        result.InnerException.Should().BeSameAs(source);
    }

    [Fact]
    public void UnrecognizedIntegratedWindowsFailureIsNotRewritten()
    {
        var source = new MsalClientException("something_else", "original failure");

        IntegratedWindowsAuthenticator.CreateActionableException(source).Should().BeNull();
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
