using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PSDataverse;
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

    #region Dispose Pattern
    /// <summary>
    /// IDisposable implementation, dispose of any disposable resources created by the cmdlet.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Implementation of IDisposable for both manual Dispose() and finalizer-called disposal of resources.
    /// </summary>
    /// <param name="disposing">
    /// Specified as true when Dispose() was called, false if this is called from the finalizer.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            ((IDisposable)_HttpClient).Dispose();
        }
    }
    #endregion


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
                client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/xml"));
    }

    private static TimeSpan GetRequestTimeout()
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
