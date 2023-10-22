namespace PSDataverse;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Identity.Client;

public class AuthenticationParameters
{
    public string Authority { get; set; }
    public string Resource { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string CertificateThumbprint { get; set; }
    public string TenantId { get; set; }
    public IEnumerable<string> Scopes { get; set; }
    public bool UseDeviceFlow { get; set; }

    public IAccount Account { get; set; }

    public static AuthenticationParameters Parse(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        { throw new ArgumentNullException(nameof(connectionString)); }
        var connectionProperties = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToDictionary(s => s[..s.IndexOf('=')], s => s[(s.IndexOf('=') + 1)..], StringComparer.OrdinalIgnoreCase);
        var resource = connectionProperties["resource"];
        return new AuthenticationParameters
        {
            Authority = connectionProperties["authority"],
            ClientId = connectionProperties["clientid"],
            Resource = resource,
            ClientSecret = connectionProperties.TryGetValue("clientsecret", out var secret) ? secret : null,
            CertificateThumbprint = connectionProperties.TryGetValue("thumbprint", out var thumbprint) ? thumbprint : null,
            TenantId = connectionProperties.TryGetValue("tenantid", out var tenantid) ? tenantid : null,
            Scopes = connectionProperties.TryGetValue("scopes", out var scopes) ? scopes.Split(',') : new string[] { new Uri(new Uri(resource, UriKind.Absolute), ".default").ToString() },
            UseDeviceFlow = connectionProperties.TryGetValue("device", out var device) && bool.Parse(device)
        };
    }
}
