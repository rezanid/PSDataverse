using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DataverseModule.Dataverse.Execute;
using Polly;
using Polly.Registry;
using Polly.Timeout;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DataverseModule
{
    class Startup
    {
        private readonly Uri _baseUri;

        public Startup(Uri baseUrl)
        {
            _baseUri = baseUrl;
        }

        public IServiceCollection ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ILogger>(NullLogger.Instance);
            services.AddSingleton<IHttpClientFactory, HttpClientFactory>(
                (provider)=> new HttpClientFactory(_baseUri, "v9.2"));
            services.AddSingleton<IReadOnlyPolicyRegistry<string>>((s) => SetupRetryPolicies());
            services.AddSingleton<OperationProcessor>();
            services.AddSingleton<BatchProcessor>();
            return services;
        }

        public PolicyRegistry SetupRetryPolicies()
        {
            HttpStatusCode[] httpStatusCodesWorthRetrying = {
               HttpStatusCode.RequestTimeout,       // 408
               HttpStatusCode.InternalServerError,  // 500
               HttpStatusCode.BadGateway,           // 502
               HttpStatusCode.ServiceUnavailable,   // 503
               HttpStatusCode.GatewayTimeout,       // 504
               (HttpStatusCode)429      // Too Many Requests
            };

            var registry = new PolicyRegistry();

            var httpPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(5, WaitTimeProvider, OnRetryAsync);

            registry.Add(Globals.PolicyNameHttp , httpPolicy);
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
}
