namespace PSDataverse;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

[Cmdlet(VerbsCommunications.Connect, "DataverseNew", SupportsShouldProcess = true)]
public class ConnectDataverseNewCmdlet : PSCmdlet
{
    private const string AuthorityBase = "https://login.microsoftonline.com/";
    private static readonly HttpClient HttpClient = new HttpClient();

    [Parameter(Position = 0, Mandatory = true)]
    public string EnvironmentUrl { get; set; }

    [Parameter(ParameterSetName = "DeviceCode", Mandatory = true)]
    public SwitchParameter DeviceCode { get; set; }

    [Parameter(ParameterSetName = "Interactive", Mandatory = true)]
    public SwitchParameter Interactive { get; set; }

    [Parameter(ParameterSetName = "ClientCredentials", Mandatory = true)]
    public string ClientId { get; set; }

    [Parameter(ParameterSetName = "ClientCredentials", Mandatory = false)]
    public SecureString ClientSecret { get; set; }

    [Parameter(ParameterSetName = "ClientCredentials", Mandatory = false)]
    public string TenantId { get; set; }

    [Parameter(ParameterSetName = "ClientCertificate", Mandatory = true)]
    public string CertificateThumbprint { get; set; }

    [Parameter(ParameterSetName = "ClientCertificate", Mandatory = true)]
    public string CertificateFilePath { get; set; }

    [Parameter(ParameterSetName = "ClientCertificate", Mandatory = false)]
    public SecureString CertificatePassword { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter UseCachedAccount { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter ForceLogin { get; set; }

    private IConfidentialClientApplication confidentialClientApp;
    private IPublicClientApplication publicClientApp;
    private AuthenticationResult authResult;

    protected override void ProcessRecord()
    {
        if (string.IsNullOrEmpty(TenantId))
        {
            WriteVerbose("No TenantId provided. Attempting auto-discovery...");
            TenantId = DiscoverTenantId(EnvironmentUrl).GetAwaiter().GetResult();
            WriteVerbose($"Discovered TenantId: {TenantId}");
        }

        authResult = AuthenticateAsync().GetAwaiter().GetResult();

        if (authResult != null)
        {
            SessionState.PSVariable.Set("DataverseAuthToken", authResult.AccessToken);
            SessionState.PSVariable.Set("DataverseEnvironment", EnvironmentUrl);
            SessionState.PSVariable.Set("DataverseTenantId", TenantId);
            WriteVerbose($"Successfully authenticated to Dataverse: {EnvironmentUrl}");
            WriteObject(new { authResult.AccessToken, authResult.ExpiresOn, TenantId });
        }
    }

    private async Task<string> DiscoverTenantId(string environmentUrl)
    {
        try
        {
            var requestUri = new Uri(new Uri(environmentUrl), "api/data/v9.0/");
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode == System.Net.HttpStatusCode.Found) // 302 Redirect
            {
                if (response.Headers.Location != null)
                {
                    var match = Regex.Match(response.Headers.Location.AbsoluteUri, @"https://login\.microsoftonline\.com/([^/]+)/");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            WriteWarning($"Failed to auto-discover TenantId: {ex.Message}");
        }

        throw new InvalidOperationException("Tenant ID auto-discovery failed. Please provide a TenantId explicitly.");
    }

    private async Task<AuthenticationResult> AuthenticateAsync()
    {
        var scopes = new[] { $"{EnvironmentUrl}/.default" };

        if (DeviceCode)
        {
            publicClientApp ??= PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority($"{AuthorityBase}{TenantId}")
                .WithDefaultRedirectUri()
                .Build();

            return await publicClientApp.AcquireTokenWithDeviceCode(scopes, async deviceCode =>
            {
                WriteInformation(new($"Go to {deviceCode.VerificationUrl} and enter the code: {deviceCode.UserCode}", "dataverse"));
            }).ExecuteAsync();
        }
        else if (Interactive)
        {
            publicClientApp ??= PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority($"{AuthorityBase}{TenantId}")
                .WithDefaultRedirectUri()
                .Build();

            return await publicClientApp.AcquireTokenInteractive(scopes).ExecuteAsync();
        }
        else if (!string.IsNullOrEmpty(ClientId) && ClientSecret != null)
        {
            confidentialClientApp ??= ConfidentialClientApplicationBuilder
                .Create(ClientId)
                .WithClientSecret(ClientSecret.ConvertToUnsecureString())
                .WithAuthority($"{AuthorityBase}{TenantId}")
                .Build();

            return await confidentialClientApp.AcquireTokenForClient(scopes).ExecuteAsync();
        }
        else if (!string.IsNullOrEmpty(ClientId) && (!string.IsNullOrEmpty(CertificateThumbprint) || !string.IsNullOrEmpty(CertificateFilePath)))
        {
            X509Certificate2 cert = LoadCertificate();
            confidentialClientApp ??= ConfidentialClientApplicationBuilder
                .Create(ClientId)
                .WithCertificate(cert)
                .WithAuthority($"{AuthorityBase}{TenantId}")
                .Build();

            return await confidentialClientApp.AcquireTokenForClient(scopes).ExecuteAsync();
        }

        throw new InvalidOperationException("Invalid authentication method.");
    }

    private X509Certificate2 LoadCertificate()
    {
        if (!string.IsNullOrEmpty(CertificateThumbprint))
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var cert = store.Certificates
                .Find(X509FindType.FindByThumbprint, CertificateThumbprint, false)
                .OfType<X509Certificate2>()
                .FirstOrDefault();

            if (cert == null)
            {
                throw new ArgumentException($"Certificate with thumbprint {CertificateThumbprint} not found.");
            }

            return cert;
        }
        else if (!string.IsNullOrEmpty(CertificateFilePath))
        {
            if (!File.Exists(CertificateFilePath))
            {
                throw new FileNotFoundException("Certificate file not found.", CertificateFilePath);
            }

            return new X509Certificate2(
                CertificateFilePath,
                CertificatePassword?.ConvertToUnsecureString(),
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
        }

        throw new ArgumentException("No valid certificate parameters provided.");
    }
}

public static class SecureStringExtensions
{
    public static string ConvertToUnsecureString(this SecureString secureString)
    {
        if (secureString == null)
        {
            return null;
        }

        var unmanagedString = IntPtr.Zero;
        try
        {
            unmanagedString = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(secureString);
            return System.Runtime.InteropServices.Marshal.PtrToStringUni(unmanagedString);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
        }
    }
}
