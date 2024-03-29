namespace PSDataverse.Tests;

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
}
