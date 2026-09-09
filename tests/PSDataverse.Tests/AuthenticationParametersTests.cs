namespace PSDataverse.Tests;

using FluentAssertions;

public class AuthenticationParametersTests
{
    [Fact]
    public void CanParseClientThumbprint()
    {
        var str = "authority=https://login.microsoftonline.com/tenant-id/oauth2/authorize;clientid=client-id;thumbprint=certificate-thumbprint;resource=https://environment-name.crm4.dynamics.com/";
        var cnnString = AuthenticationParameters.Parse(str);
        Assert.Equal(expected: "https://login.microsoftonline.com/tenant-id/oauth2/authorize", cnnString.Authority);
        Assert.Equal(expected: "client-id", cnnString.ClientId);
        Assert.Equal(expected: "certificate-thumbprint", cnnString.CertificateThumbprint);
        Assert.Equal(expected: "https://environment-name.crm4.dynamics.com/", cnnString.Resource);
    }

    [Fact]
    public void CanParseClientIdAndSecret()
    {
        var str = "authority=https://login.microsoftonline.com/tenant-id/oauth2/authorize;clientid=client-id;clientsecret=client-secret;resource=https://environment-name.crm4.dynamics.com/";
        var cnnString = AuthenticationParameters.Parse(str);
        Assert.Equal(expected: "https://login.microsoftonline.com/tenant-id/oauth2/authorize", cnnString.Authority);
        Assert.Equal(expected: "client-id", cnnString.ClientId);
        Assert.Equal(expected: "client-secret", cnnString.ClientSecret);
        Assert.Equal(expected: "https://environment-name.crm4.dynamics.com/", cnnString.Resource);
    }

    [Fact]
    public void CanParseDeviceCode()
    {
        var str = "authority=https://login.microsoftonline.com/tenant-id/oauth2/authorize;clientid=client-id;resource=https://environment-name.crm4.dynamics.com/;device=true";
        var cnnString = AuthenticationParameters.Parse(str);
        Assert.Equal(expected: "https://login.microsoftonline.com/tenant-id/oauth2/authorize", cnnString.Authority);
        Assert.Equal(expected: "client-id", cnnString.ClientId);
        Assert.True(cnnString.UseDeviceFlow);
        Assert.Equal(expected: "https://environment-name.crm4.dynamics.com/", cnnString.Resource);
    }

    [Fact]
    public void SupportsXrmToolingStyleAliases()
    {
        var value = AuthenticationParameters.Parse(
            "AuthType=ClientSecret;Url=https://example.crm.dynamics.com;" +
            "ApplicationId=app-id;Secret=secret;TenantId=tenant-id");

        value.Resource.Should().Be("https://example.crm.dynamics.com/");
        value.ClientId.Should().Be("app-id");
        value.ClientSecret.Should().Be("secret");
        value.Tenant.Should().Be("tenant-id");
    }

    [Fact]
    public void RejectsDuplicateConnectionStringKeys()
    {
        var action = () => AuthenticationParameters.Parse(
            "Url=https://one.crm.dynamics.com;URL=https://two.crm.dynamics.com");

        action.Should().Throw<ArgumentException>().WithMessage("*specified more than once*");
    }

    [Theory]
    [InlineData("AuthType=ClientSecret;Url=https://example.crm.dynamics.com;ClientId=id;TenantId=tenant", "requires ClientSecret")]
    [InlineData("AuthType=Office365;Url=https://example.crm.dynamics.com", "is not supported")]
    public void RejectsIncompleteOrUnsupportedXrmAuthenticationTypes(string value, string message)
    {
        var action = () => AuthenticationParameters.Parse(value);

        action.Should().Throw<ArgumentException>().WithMessage($"*{message}*");
    }
}
