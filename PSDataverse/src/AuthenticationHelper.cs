using System;
using Microsoft.Identity.Client;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace DataverseModule
{
    internal class AuthenticationHelper
    {
        public static bool TestAuth()
        {
            string authority = "https://login.microsoftonline.com/c89214f1-7515-48ee-9cd0-9b859ed3e4c4/oauth2/authorize";
            string clientId = "b84d3f12-8ec4-4107-9812-dbfeceb8f17f";
            string thumbprint = "320DAC96A53579C484248523ABD090FA5C223FE1";
            var certificate = FindCertificate(thumbprint, StoreName.My);
            var result = Authenticate(authority, clientId, resource: "https://crm4kbcgen-dev-202205.crm4.dynamics.com/", certificate);
            return result != null && !string.IsNullOrEmpty(result.AccessToken);
        }

        public static AuthenticationResult Authenticate(string authority, string clientId, string resource, X509Certificate2 clientCert)
        {
            var confidentialClient = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithCertificate(clientCert)
                .WithAuthority(authority)
                .Build();
            return confidentialClient.AcquireTokenForClient(new string[] { $"{resource}/.default" }).ExecuteAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static X509Certificate2 FindCertificate(
          string certificateThumbprint,
          StoreName storeName)
        {
            //logSink.Log(string.Format("Looking for certificate with thumbprint: {0}..", (object)certificateThumbprint));
            var source = new StoreLocation[2] { StoreLocation.CurrentUser, StoreLocation.LocalMachine };
            try
            {
                X509Certificate2Collection certificates = null;
                if (((IEnumerable<StoreLocation>)source).Any(storeLocation => TryFindCertificatesInStore(certificateThumbprint, storeLocation, storeName, out certificates)))
                {
                    //logSink.Log(string.Format("Found certificate with thumbprint: {0}!", (object)certificateThumbprint));
                    return certificates[0];
                }
            }
            catch (Exception)
            {
                //logSink.Log(string.Format("Failed to find certificate with thumbprint: {0}.", (object)certificateThumbprint), TraceEventType.Error, ex);
                return null;
            }
            //logSink.Log(string.Format("Failed to find certificate with thumbprint: {0}.", (object)certificateThumbprint), TraceEventType.Error);
            return null;
        }

        private static bool TryFindCertificatesInStore(
          string certificateThumbprint,
          StoreLocation location,
          StoreName certReproName,
          out X509Certificate2Collection certificates)
        {
            var x509Store = new X509Store(certReproName, location);
            x509Store.Open(OpenFlags.ReadOnly);
            certificates = x509Store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);
            x509Store.Close();
            return certificates.Count > 0;
        }
    }
}