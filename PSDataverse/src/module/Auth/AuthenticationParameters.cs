namespace DataverseModule;

using System;
using System.Collections.Generic;
using System.Linq;

public class AuthenticationParameters
{
    public string Authority { get; set; }
    public string Resource { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string CertificateThumbprint { get; set; }
    public string ServicePrincipalSecret { get; set; }
    public string TenantId { get; set; }
    public IEnumerable<string> Scopes { get; set; }
    public bool UseDeviceFlow { get; set; }

    public static AuthenticationParameters Parse(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        { throw new ArgumentNullException(nameof(connectionString)); }
        var connectionProperties = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToDictionary(s => s.Substring(0, s.IndexOf('=')), s => s.Substring(s.IndexOf('=') + 1), StringComparer.OrdinalIgnoreCase);
        var resource = connectionProperties["resource"];
        return new AuthenticationParameters
        {
            Authority = connectionProperties["authority"],
            ClientId = connectionProperties["clientid"],
            Resource = resource,
            ClientSecret = connectionProperties.TryGetValue("clientsecret", out var secret) ? secret : null,
            CertificateThumbprint = connectionProperties.TryGetValue("thumbprint", out var thumbprint) ? thumbprint : null,
            TenantId = connectionProperties.TryGetValue("tenantid", out var tenantid) ? tenantid : null,
            ServicePrincipalSecret = connectionProperties.TryGetValue("serviceprincipalsecret", out var principalsecret) ? principalsecret : null,
            Scopes = connectionProperties.TryGetValue("scopes", out var scopes) ? scopes.Split(',') : new string[] { $"{resource}/.default" },
            UseDeviceFlow = connectionProperties.TryGetValue("device", out var device) ? bool.Parse(device) : false
        };
    }
}
