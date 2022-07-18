using System;
using System.Linq;

namespace DataverseModule
{
    public class DataverseConnectionString
    {
        public string Authority { get; set; }
        public string Resource { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string CertificationThumbprint { get; set; }

        public static DataverseConnectionString Parse(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) {throw new ArgumentNullException(nameof(connectionString)); }
            var connectionProperties = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToDictionary(s => s.Substring(0, s.IndexOf('=')), s => s.Substring(s.IndexOf('=') + 1), StringComparer.OrdinalIgnoreCase);
            return new DataverseConnectionString
                {
                    Authority = connectionProperties["authority"],
                    ClientId = connectionProperties["clientid"],
                    Resource = connectionProperties["resource"],
                    ClientSecret = connectionProperties.TryGetValue("clientsecret", out var secret) ? secret : null,
                    CertificationThumbprint = connectionProperties.TryGetValue("thumbprint", out var thumbprint) ? thumbprint : null
                };
        }
    }
}