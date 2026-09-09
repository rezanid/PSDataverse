namespace PSDataverse;

using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

public record AuthenticationParameters
{
    public const string DefaultClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";

    // Power Platform SDK uses "app://58145B91-0C36-4500-8554-080854F2AC97", but according to MSAL docs, localhost is safer
    // Read more: https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/using-web-browsers;
    public const string DefaultRedirectUrl = "http://localhost";

    public string Authority { get; set; }
    public string Resource { get; set; }
    public string ClientId { get; set; } = DefaultClientId;
    public string ClientSecret { get; set; }
    public string CertificateThumbprint { get; set; }
    public StoreName CertificateStoreName { get; set; }
    public string Tenant { get; set; }
    public IEnumerable<string> Scopes { get; set; }
    public bool UseDeviceFlow { get; set; }
    public bool UseCurrentUser { get; set; }
    public bool UseIntegratedWindowsAuthentication { get; set; }
    public bool UseBroker { get; set; }
    internal bool BrokerPreferenceSpecified { get; set; }
    public string RedirectUri { get; set; } = DefaultRedirectUrl;
    public string Username { get; set; }

    public IAccount Account { get; set; }

    public static AuthenticationParameters Parse(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) { throw new ArgumentNullException(nameof(connectionString)); }
        string resource = null;
        Dictionary<string, string> dictionary = null;
        if (connectionString.IndexOf('=') <= 0)
        {
            if (connectionString.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                resource = connectionString;
                if (!resource.EndsWith("/", StringComparison.OrdinalIgnoreCase)) { resource += "/"; }
                dictionary = new Dictionary<string, string>() { ["resource"] = connectionString, ["integrated security"] = true.ToString() };
            }
            else
            {
                throw new InvalidOperationException(
                    "Connection string must be an HTTPS Dataverse URL or contain key=value pairs including Url or Resource.");
            }
        }
        else
        {
            dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var segment in connectionString.Split([';'], StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = segment.IndexOf('=');
                if (separator <= 0)
                {
                    throw new ArgumentException($"Invalid connection-string segment '{segment.Trim()}'.");
                }
                var key = segment[..separator].Trim();
                if (!dictionary.TryAdd(key, segment[(separator + 1)..].Trim()))
                {
                    throw new ArgumentException($"Connection-string key '{key}' was specified more than once.");
                }
            }
            resource =
                dictionary.TryGetValue("resource", out var url)
                ? url
                : dictionary.TryGetValue("url", out url)
                ? url
                : throw new ArgumentException("Connection string should contain either Url or Resource. Both are missing.");
        }
        if (string.IsNullOrEmpty(resource))
        {
            throw new ArgumentException("Either Resource or Url is required.");
        }
        if (!resource.EndsWith("/", StringComparison.OrdinalIgnoreCase)) { resource += "/"; }

        var parameters = new AuthenticationParameters
        {
            Authority = GetValue(dictionary, "authority"),
            ClientId = GetValue(dictionary, "clientid", "applicationid", "appid") ?? DefaultClientId,
            RedirectUri = dictionary.TryGetValue("redirecturi", out var redirecturi) ? redirecturi : DefaultRedirectUrl,
            Resource = resource,
            ClientSecret = GetValue(dictionary, "clientsecret", "secret"),
            CertificateThumbprint = GetValue(dictionary, "thumbprint", "certificatethumbprint"),
            Tenant = GetValue(dictionary, "tenantid", "tenant"),
            Scopes = dictionary.TryGetValue("scopes", out var scopes) ? scopes.Split(',') : [new Uri(new Uri(resource, UriKind.Absolute), ".default").ToString()],
            UseDeviceFlow = GetBoolean(dictionary, "device") || IsAuthType(dictionary, "devicecode"),
            UseCurrentUser = GetBoolean(dictionary, "integrated security") || IsAuthType(dictionary, "oauth", "interactive"),
            UseIntegratedWindowsAuthentication = IsAuthType(dictionary, "ad", "integratedwindows"),
            UseBroker = GetBoolean(dictionary, "usebroker")
        };
        parameters.BrokerPreferenceSpecified = dictionary.ContainsKey("usebroker");
        if (string.IsNullOrEmpty(parameters.Authority) && !string.IsNullOrEmpty(parameters.Tenant))
        {
            parameters.Authority = $"https://login.microsoftonline.com/{parameters.Tenant}/oauth2/authorize";
        }
        parameters.CertificateStoreName = ExtractStoreName(dictionary);
        ValidateAuthenticationType(dictionary, parameters);
        return parameters;
    }

    private static StoreName ExtractStoreName(Dictionary<string, string> parameters)
    {

        if (parameters.TryGetValue("certificatestore", out var certificateStore)
            || parameters.TryGetValue("storename", out certificateStore))
        {
            if (Enum.TryParse(certificateStore, true, out StoreName storeName))
            {
                return storeName;
            }
            else
            {
                //TODO: Log warning.
            }
        }
        return StoreName.My;
    }

    private static string GetValue(Dictionary<string, string> parameters, params string[] names)
    {
        foreach (var name in names)
        {
            if (parameters.TryGetValue(name, out var value))
            {
                return value;
            }
        }
        return null;
    }

    private static bool GetBoolean(Dictionary<string, string> parameters, string name)
        => parameters.TryGetValue(name, out var value) &&
           bool.TryParse(value, out var parsed) &&
           parsed;

    private static bool IsAuthType(Dictionary<string, string> parameters, params string[] expected)
        => parameters.TryGetValue("authtype", out var value) &&
           expected.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static void ValidateAuthenticationType(
        Dictionary<string, string> values,
        AuthenticationParameters parameters)
    {
        if (!values.TryGetValue("authtype", out var authenticationType))
        {
            return;
        }
        var supported = new[]
        {
            "oauth", "interactive", "devicecode", "clientsecret", "certificate", "ad", "integratedwindows"
        };
        if (!supported.Contains(authenticationType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Connection-string AuthType '{authenticationType}' is not supported. Use Interactive, DeviceCode, ClientSecret, Certificate, or IntegratedWindows.");
        }
        if (authenticationType.Equals("clientsecret", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(parameters.ClientSecret))
        {
            throw new ArgumentException("AuthType=ClientSecret requires ClientSecret or Secret.");
        }
        if (authenticationType.Equals("certificate", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(parameters.CertificateThumbprint))
        {
            throw new ArgumentException("AuthType=Certificate requires CertificateThumbprint or Thumbprint.");
        }
    }

    public bool IsValid() => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(Resource) &&
        (!string.IsNullOrWhiteSpace(Authority) || !string.IsNullOrWhiteSpace(Tenant));

    public bool IsUncertainAuthFlow()
        => string.IsNullOrWhiteSpace(ClientSecret) && string.IsNullOrWhiteSpace(CertificateThumbprint) && !UseDeviceFlow && IsValid();
}
