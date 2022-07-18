using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace DataverseModule
{
    public class HttpClientFactory : IHttpClientFactory, IDisposable
    {
        HttpClient _HttpClient;
        private readonly object _Lock = new();
        private readonly Uri _baseUrl;

        public HttpClientFactory(Uri baseUrl, string apiVersion)
        {
            _baseUrl = new Uri(baseUrl, $"/api/data/{apiVersion}/");
        }

        public HttpClient GetHttpClientInstance()
        {
            if (_HttpClient == null)
            {
                lock (_Lock)
                {
                    if (_HttpClient == null)
                    {
                        _HttpClient = new HttpClient(CreateHttpClientHandler());
                        SetHttpClientDefaults(_HttpClient);
                    }
                }
            }
            return _HttpClient;
        }

        public void Dispose()
        {
            ((IDisposable)_HttpClient).Dispose();
        }


        /// <summary>
        /// Sets the default values for HttpClient.
        /// </summary>
        /// <remarks>
        /// All uses of HttpClient should never modify the following properties
        /// after creation:
        /// <list type="bullet">
        /// <item>BaseAddress</item>
        /// <item>Timeout</item>
        /// <item>MaxResponseContentBufferSize</item>
        /// </list>
        /// </remarks>
        protected virtual void SetHttpClientDefaults(HttpClient client)
        {
            client.BaseAddress = _baseUrl;
            client.Timeout = GetRequestTimeout();
            client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            client.DefaultRequestHeaders.Add("OData-Version", "4.0");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private TimeSpan GetRequestTimeout()
        {
            string requestTimeout = Environment.GetEnvironmentVariable("Request_Timeout", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrEmpty(requestTimeout) && TimeSpan.TryParse(requestTimeout, out var timeout))
            {
                return timeout;
            }
            return TimeSpan.FromMinutes(10);
        }

        public virtual HttpClientHandler CreateHttpClientHandler() => new()
        {
            UseCookies = false
        };

        public HttpClient CreateClient(string name) => GetHttpClientInstance();
    }
}
