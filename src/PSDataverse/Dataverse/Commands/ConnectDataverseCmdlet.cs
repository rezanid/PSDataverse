namespace PSDataverse;

using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

[Cmdlet(VerbsCommunications.Connect, "Dataverse", DefaultParameterSetName = ConnectionStringSet)]
[OutputType(typeof(DataverseConnection))]
public sealed class ConnectDataverseCmdlet : DataverseCmdlet
{
    private const string ConnectionStringSet = "ConnectionString";
    private const string UrlSet = "Url";
    private const string InteractiveSet = "Interactive";
    private const string IntegratedWindowsSet = "IntegratedWindows";
    private const string DeviceCodeSet = "DeviceCode";
    private const string ClientSecretSet = "ClientSecret";
    private const string ClientSecretProviderSet = "ClientSecretProvider";
    private const string CertificateSet = "Certificate";
    private const string AccessTokenSet = "AccessToken";
    private const string TokenProviderSet = "TokenProvider";

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = UrlSet)]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = InteractiveSet)]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = IntegratedWindowsSet)]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = DeviceCodeSet)]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = ClientSecretSet)]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = ClientSecretProviderSet)]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = CertificateSet)]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = AccessTokenSet)]
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = TokenProviderSet)]
    [Alias("ServiceUrl", "EnvironmentUrl")]
    public string Url { get; set; }

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = ConnectionStringSet)]
    [Alias("CdsConnectionString", "DataverseConnectionString")]
    public string ConnectionString { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = InteractiveSet)]
    public SwitchParameter Interactive { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = IntegratedWindowsSet)]
    [Alias("IntegratedSecurity")]
    public SwitchParameter IntegratedWindowsAuthentication { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = DeviceCodeSet)]
    public SwitchParameter DeviceCode { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = ClientSecretSet)]
    public SecureString ClientSecret { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = ClientSecretProviderSet)]
    public ScriptBlock ClientSecretProvider { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = CertificateSet)]
    [Alias("Thumbprint")]
    public string CertificateThumbprint { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = AccessTokenSet)]
    public SecureString AccessToken { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = TokenProviderSet)]
    public ScriptBlock TokenProvider { get; set; }

    [Parameter(ParameterSetName = UrlSet)]
    [Alias("OnPremises")]
    public SwitchParameter OnPremise { get; set; }

    [Parameter(ParameterSetName = InteractiveSet)]
    [Parameter(ParameterSetName = IntegratedWindowsSet)]
    [Parameter(ParameterSetName = DeviceCodeSet)]
    [Parameter(Mandatory = true, ParameterSetName = ClientSecretSet)]
    [Parameter(Mandatory = true, ParameterSetName = ClientSecretProviderSet)]
    [Parameter(Mandatory = true, ParameterSetName = CertificateSet)]
    public string ClientId { get; set; } = AuthenticationParameters.DefaultClientId;

    [Parameter(ParameterSetName = InteractiveSet)]
    [Parameter(ParameterSetName = IntegratedWindowsSet)]
    [Parameter(ParameterSetName = DeviceCodeSet)]
    [Parameter(Mandatory = true, ParameterSetName = ClientSecretSet)]
    [Parameter(Mandatory = true, ParameterSetName = ClientSecretProviderSet)]
    [Parameter(Mandatory = true, ParameterSetName = CertificateSet)]
    [Alias("Tenant")]
    public string TenantId { get; set; }

    [Parameter(ParameterSetName = InteractiveSet)]
    [Parameter(ParameterSetName = UrlSet)]
    public SwitchParameter UseSystemBrowser { get; set; }

    [Parameter(ParameterSetName = InteractiveSet)]
    [Alias("UseWam")]
    public SwitchParameter UseWebAccountManager { get; set; }

    [Parameter(ParameterSetName = InteractiveSet)]
    public SwitchParameter ForceAuthentication { get; set; }

    [Parameter(ParameterSetName = InteractiveSet)]
    [Parameter(ParameterSetName = DeviceCodeSet)]
    public string RedirectUri { get; set; } = AuthenticationParameters.DefaultRedirectUrl;

    [Parameter(ParameterSetName = IntegratedWindowsSet)]
    public string Username { get; set; }

    [Parameter(ParameterSetName = InteractiveSet)]
    [Parameter(ParameterSetName = IntegratedWindowsSet)]
    [Parameter(ParameterSetName = DeviceCodeSet)]
    [Parameter(ParameterSetName = ClientSecretSet)]
    [Parameter(ParameterSetName = ClientSecretProviderSet)]
    [Parameter(ParameterSetName = CertificateSet)]
    public string[] Scopes { get; set; }

    [Parameter(ParameterSetName = CertificateSet)]
    public StoreName CertificateStoreName { get; set; } = StoreName.My;

    [Parameter(ParameterSetName = AccessTokenSet)]
    public DateTimeOffset ExpiresOn { get; set; } = DateTimeOffset.UtcNow.AddMinutes(50);

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = "default";

    [Parameter]
    [ValidatePattern(@"^v[0-9]+\.[0-9]+$")]
    public string ApiVersion { get; set; } = "v9.2";

    [Parameter]
    public SwitchParameter NoDefault { get; set; }

    protected override void ProcessRecord()
    {
        base.ProcessRecord();
        DataverseConnection connection = null;
        try
        {
            connection = CreateConnection();
            DisposeLegacyConnection();
            GetConnectionRegistry().Add(connection, setDefault: !NoDefault);
            WriteInformation(
                $"Dataverse connection '{connection.Name}' authenticated using {connection.AuthenticationKind}.",
                ["dataverse"]);
            WriteObject(connection);
        }
        catch (OperationCanceledException exception)
        {
            connection?.Dispose();
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Dataverse authentication cancelled.", exception),
                Globals.ErrorIdAuthenticationFailed,
                ErrorCategory.OperationStopped,
                Url));
        }
        catch (Exception exception)
        {
            connection?.Dispose();
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException($"Dataverse connection failed: {exception.Message}", exception),
                Globals.ErrorIdAuthenticationFailed,
                ErrorCategory.AuthenticationError,
                Url ?? ConnectionString));
        }
    }

    private DataverseConnection CreateConnection()
    {
        var messages = new ConcurrentQueue<string>();
        var connectionTask = CreateConnectionAsync(messages.Enqueue);
        while (!connectionTask.IsCompleted)
        {
            WritePendingMessages(messages);
            CancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(25);
        }
        WritePendingMessages(messages);
        return connectionTask.ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private void WritePendingMessages(ConcurrentQueue<string> messages)
    {
        while (messages.TryDequeue(out var message))
        {
            WriteInformation(
                new HostInformationMessage { Message = message },
                ["PSHOST", "dataverse"]);
        }
    }

    private async Task<DataverseConnection> CreateConnectionAsync(Action<string> onMessageForUser)
    {
        if (ParameterSetName == ConnectionStringSet)
        {
            var connectionParameters = AuthenticationParameters.Parse(ConnectionString);
            if (!connectionParameters.BrokerPreferenceSpecified)
            {
                connectionParameters.UseBroker = OperatingSystem.IsWindows() &&
                    ResolveAuthenticationKind(connectionParameters) == DataverseAuthenticationKind.Interactive;
            }
            var connectionKind = ResolveAuthenticationKind(connectionParameters);
            return await DataverseConnectionFactory.CreateMsalAsync(
                Name, new Uri(connectionParameters.Resource), ApiVersion, connectionKind, connectionParameters, onMessageForUser, CancellationToken)
                .ConfigureAwait(false);
        }

        var serviceUrl = NormalizeServiceUrl(Url);
        if (UseWebAccountManager && !OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Web Account Manager authentication is only supported on Windows. Use -Interactive on this platform.");
        }
        if (UseWebAccountManager && UseSystemBrowser)
        {
            throw new ArgumentException(
                "-UseWebAccountManager and -UseSystemBrowser cannot be used together.");
        }
        if (ParameterSetName == IntegratedWindowsSet && !OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Integrated Windows Authentication is only supported on Windows. Use -Interactive or -DeviceCode on this platform.");
        }
        if (ParameterSetName == UrlSet && OnPremise)
        {
            var onPremisesApiVersion = MyInvocation.BoundParameters.ContainsKey(nameof(ApiVersion))
                ? ApiVersion
                : "v9.1";
            return DataverseConnectionFactory.CreateOnPremises(Name, serviceUrl, onPremisesApiVersion);
        }
        if (ParameterSetName == AccessTokenSet)
        {
            return DataverseConnectionFactory.CreateAccessToken(Name, serviceUrl, ApiVersion, AccessToken, ExpiresOn);
        }
        if (ParameterSetName == TokenProviderSet)
        {
            return await DataverseConnectionFactory.CreateTokenProviderAsync(
                Name, serviceUrl, ApiVersion, TokenProvider, CancellationToken).ConfigureAwait(false);
        }

        var parameters = new AuthenticationParameters
        {
            Resource = serviceUrl.AbsoluteUri,
            Tenant = TenantId,
            ClientId = ClientId,
            RedirectUri = RedirectUri,
            UseDeviceFlow = ParameterSetName == DeviceCodeSet,
            UseCurrentUser = ParameterSetName is UrlSet or InteractiveSet,
            UseIntegratedWindowsAuthentication = ParameterSetName == IntegratedWindowsSet,
            UseBroker = (ParameterSetName is UrlSet or InteractiveSet) && OperatingSystem.IsWindows() && !UseSystemBrowser,
            ForceAuthentication = ParameterSetName == DeviceCodeSet || ForceAuthentication,
            Username = Username,
            ClientSecret = ResolveClientSecret(),
            CertificateThumbprint = CertificateThumbprint,
            CertificateStoreName = CertificateStoreName,
            Scopes = Scopes is { Length: > 0 } ? Scopes : [new Uri(serviceUrl, ".default").AbsoluteUri]
        };
        if (!string.IsNullOrWhiteSpace(TenantId))
        {
            parameters.Authority = $"https://login.microsoftonline.com/{TenantId}";
        }
        var kind = ParameterSetName switch
        {
            DeviceCodeSet => DataverseAuthenticationKind.DeviceCode,
            IntegratedWindowsSet => DataverseAuthenticationKind.IntegratedWindows,
            ClientSecretSet or ClientSecretProviderSet => DataverseAuthenticationKind.ClientSecret,
            CertificateSet => DataverseAuthenticationKind.Certificate,
            _ when parameters.UseBroker => DataverseAuthenticationKind.Wam,
            _ => DataverseAuthenticationKind.Interactive
        };
        return await DataverseConnectionFactory.CreateMsalAsync(
            Name, serviceUrl, ApiVersion, kind, parameters, onMessageForUser, CancellationToken)
            .ConfigureAwait(false);
    }

    internal static DataverseAuthenticationKind ResolveAuthenticationKind(AuthenticationParameters parameters)
    {
        if (parameters.UseIntegratedWindowsAuthentication)
        {
            return DataverseAuthenticationKind.IntegratedWindows;
        }
        if (!string.IsNullOrWhiteSpace(parameters.ClientSecret))
        {
            return DataverseAuthenticationKind.ClientSecret;
        }
        if (!string.IsNullOrWhiteSpace(parameters.CertificateThumbprint))
        {
            return DataverseAuthenticationKind.Certificate;
        }
        if (parameters.UseBroker && OperatingSystem.IsWindows())
        {
            return DataverseAuthenticationKind.Wam;
        }
        return parameters.UseDeviceFlow
            ? DataverseAuthenticationKind.DeviceCode
            : DataverseAuthenticationKind.Interactive;
    }

    private string ResolveClientSecret()
    {
        if (ClientSecret is not null)
        {
            return DataverseConnectionFactory.Unprotect(ClientSecret);
        }
        if (ClientSecretProvider is null)
        {
            return null;
        }
        var output = ClientSecretProvider.Invoke();
        if (output.Count != 1)
        {
            throw new InvalidOperationException("A client-secret provider must return exactly one value.");
        }
        return output[0].BaseObject switch
        {
            SecureString secure => DataverseConnectionFactory.Unprotect(secure),
            string value when !string.IsNullOrWhiteSpace(value) => value,
            PSCredential credential => DataverseConnectionFactory.Unprotect(credential.Password),
            _ => throw new InvalidOperationException(
                "A client-secret provider must return a SecureString, PSCredential, or non-empty string.")
        };
    }

    private static Uri NormalizeServiceUrl(string value)
    {
        var uri = new Uri(value, UriKind.Absolute);
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            throw new ArgumentException("Dataverse URL must use HTTP or HTTPS.", nameof(value));
        }
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }

    private void DisposeLegacyConnection()
    {
        (GetVariableValue(Globals.VariableNameServiceProvider) as IDisposable)?.Dispose();
        SessionState.PSVariable.Remove(Globals.VariableNameServiceProvider);
        SessionState.PSVariable.Remove(Globals.VariableNameAccessToken);
        SessionState.PSVariable.Remove(Globals.VariableNameAccessTokenExpiresOn);
        SessionState.PSVariable.Remove(Globals.VariableNameAuthResult);
        SessionState.PSVariable.Remove(Globals.VariableNameConnectionString);
        SessionState.PSVariable.Remove(Globals.VariableNameIsOnPremise);
    }
}
