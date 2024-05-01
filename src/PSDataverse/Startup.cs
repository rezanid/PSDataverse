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
using System.Collections.Generic;
using System.Text;
using PSDataverse.Auth;
using PSDataverse.Dataverse.Execute;

internal class Startup(Uri baseUrl)
{
    public IServiceCollection ConfigureServices(IServiceCollection services) => services
        .AddSingleton<ILogger>(NullLogger.Instance)
        .AddSingleton<IHttpClientFactory, HttpClientFactory>((provider) => new HttpClientFactory(baseUrl, "v9.2"))
        .AddSingleton<IReadOnlyPolicyRegistry<string>>((s) => SetupRetryPolicies())
        .AddSingleton<OperationProcessor>()
        .AddSingleton<BatchProcessor>()
        .AddSingleton<IAuthenticator, DelegatingAuthenticator>((provider) => new ClientAppAuthenticator
        {
            NextAuthenticator = new DeviceCodeAuthenticator()
        })
        .AddSingleton<AuthenticationService>();

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
}
