namespace PSDataverse;
using System;
using System.Net.Http;
using System.Net.Http.Headers;

public class HttpClientFactory(Uri baseUrl, string apiVersion) : IHttpClientFactory, IDisposable
{
    private HttpClient httpClient;
    private readonly object @lock = new();
    private readonly Uri baseUrl = new(
            baseUrl.AbsoluteUri.EndsWith("/", StringComparison.OrdinalIgnoreCase)
            ? baseUrl.AbsoluteUri + $"api/data/{apiVersion}/"
            : baseUrl.AbsoluteUri + $"/api/data/{apiVersion}/");

    public HttpClient GetHttpClientInstance()
    {
        if (httpClient == null)
        {
            lock (@lock)
            {
                if (httpClient == null)
                {
                    httpClient = new HttpClient(CreateHttpClientHandler());
                    SetHttpClientDefaults(httpClient);
                }
            }
        }
        return httpClient;
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
            ((IDisposable)httpClient).Dispose();
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
        client.BaseAddress = baseUrl;
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
        var requestTimeout = Environment.GetEnvironmentVariable("Request_Timeout", EnvironmentVariableTarget.Process);
        if (!string.IsNullOrEmpty(requestTimeout) && TimeSpan.TryParse(requestTimeout, out var timeout))
        {
            return timeout;
        }
        return TimeSpan.FromMinutes(10);
    }

    public virtual HttpClientHandler CreateHttpClientHandler() => new()
    {
        UseCookies = false,
        UseDefaultCredentials = true
    };

    public HttpClient CreateClient(string name) => GetHttpClientInstance();
}
