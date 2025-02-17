namespace PSDataverse;

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Registry;
using Polly.Timeout;
using PSDataverse.Auth;
using PSDataverse.Dataverse.Execute;

internal sealed class Startup(Uri baseUrl, string apiVersion = "v9.2")
{
    public IServiceCollection ConfigureServices(IServiceCollection services)
    {
        _ = services
        .AddSingleton<ILogger>(NullLogger.Instance)
        .AddSingleton<IReadOnlyPolicyRegistry<string>>((s) => SetupRetryPolicies())
        .AddSingleton<OperationProcessor>()
        .AddSingleton<BatchProcessor>()
        .AddSingleton<IAuthenticator, DelegatingAuthenticator>((provider) => new ClientAppAuthenticator
        {
            NextAuthenticator = new DeviceCodeAuthenticator
            {
                NextAuthenticator = new IntegratedAuthenticator()
            }
        })
        .AddSingleton<AuthenticationService>()
        .AddHttpClient(Globals.DataverseHttpClientName, (provider, client) =>
        {
            client.BaseAddress = new(
                baseUrl.AbsoluteUri.EndsWith("/", StringComparison.OrdinalIgnoreCase)
                ? baseUrl.AbsoluteUri + $"api/data/{apiVersion}/"
                : baseUrl.AbsoluteUri + $"/api/data/{apiVersion}/"
            );
            client.Timeout = GetDefaultRequestTimeout();
            client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            client.DefaultRequestHeaders.Add("OData-Version", "4.0");
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        })
        .ConfigureHttpMessageHandlerBuilder(builder => builder.PrimaryHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseDefaultCredentials = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });
        return services;
    }

    public PolicyRegistry SetupRetryPolicies()
    {
        HttpStatusCode[] httpStatusCodesWorthRetrying = [
            HttpStatusCode.RequestTimeout,       // 408
            HttpStatusCode.InternalServerError,  // 500
            HttpStatusCode.BadGateway,           // 502
            HttpStatusCode.ServiceUnavailable,   // 503
            HttpStatusCode.GatewayTimeout,       // 504
            (HttpStatusCode)429      // Too Many Requests
        ];

        var registry = new PolicyRegistry();

        var httpPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(5, WaitTimeProvider, OnRetryAsync);

        registry.Add(Globals.PolicyNameHttp, httpPolicy);
        return registry;
    }

    private TimeSpan WaitTimeProvider(int retryAttempt, DelegateResult<HttpResponseMessage> response, Context context)
    {
        var retryAfter = response.Result.Headers.RetryAfter;
        if (retryAfter != null)
        {
            return retryAfter.Delta.Value;
        }
        return TimeSpan.FromSeconds(3 * Math.Pow(2, retryAttempt));
    }

    private Task OnRetryAsync(DelegateResult<HttpResponseMessage> response, TimeSpan wait, int retryAttempt, Context context)
    {
        Debug.WriteLine($"Retry delegate invoked. Attempt {retryAttempt}");
        return Task.CompletedTask;
    }

    private static TimeSpan GetDefaultRequestTimeout()
    {
        var requestTimeout = Environment.GetEnvironmentVariable("Request_Timeout", EnvironmentVariableTarget.Process);
        if (!string.IsNullOrEmpty(requestTimeout) && TimeSpan.TryParse(requestTimeout, out var timeout))
        {
            return timeout;
        }
        return TimeSpan.FromMinutes(10);
    }
}
