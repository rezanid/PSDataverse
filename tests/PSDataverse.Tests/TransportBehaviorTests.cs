namespace PSDataverse.Tests;

using System.Management.Automation;
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using PSDataverse.Dataverse.Execute;
using PSDataverse.Dataverse.Model;

public class TransportBehaviorTests
{
    [Fact]
    public async Task CancellationReachesTheHttpHandler()
    {
        var handler = new TestHttpMessageHandler().Enqueue(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var processor = CreateProcessor(handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var action = () => processor.ExecuteAsync(
            new Operation<string> { Method = "GET", Uri = "accounts" },
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task AuthenticationTokenIsSentAsBearerHeader()
    {
        var handler = new TestHttpMessageHandler()
            .Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = TestHttpInfrastructure.CreateClient(handler);
        var processor = new OperationProcessor(
            NullLogger.Instance,
            new TestHttpClientFactory(client),
            TestHttpInfrastructure.CreateNoOpPolicies(),
            "test-access-token");

        using var response = await processor.ExecuteAsync(
            new Operation<string> { Method = "GET", Uri = "accounts" });

        handler.Requests.Single().Headers["Authorization"]
            .Should().Be("Bearer test-access-token");
    }

    [Fact]
    public async Task RetryPolicyRepeatsTransientResponseUsingRetryAfter()
    {
        var transient = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        transient.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        var handler = new TestHttpMessageHandler()
            .Enqueue(transient)
            .Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":[]}")
            });
        using var client = TestHttpInfrastructure.CreateClient(handler);
        var processor = new OperationProcessor(
            NullLogger.Instance,
            new TestHttpClientFactory(client),
            Startup.SetupRetryPolicies());

        using var response = await processor.ExecuteAsync(
            new Operation<string> { Method = "GET", Uri = "accounts" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task RetryPolicyRepeatsTooManyRequestsUsingRetryAfter()
    {
        var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        throttled.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        var handler = new TestHttpMessageHandler()
            .Enqueue(throttled)
            .Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":[]}")
            });
        using var client = TestHttpInfrastructure.CreateClient(handler);
        var processor = new OperationProcessor(
            NullLogger.Instance,
            new TestHttpClientFactory(client),
            Startup.SetupRetryPolicies());

        using var response = await processor.ExecuteAsync(
            new Operation<string> { Method = "GET", Uri = "accounts" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task RetryPolicyDoesNotReplayPostAfterTransientResponse()
    {
        var handler = new TestHttpMessageHandler()
            .Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = TestHttpInfrastructure.CreateClient(handler);
        var processor = new OperationProcessor(
            NullLogger.Instance,
            new TestHttpClientFactory(client),
            Startup.SetupRetryPolicies());

        var action = () => processor.ExecuteAsync(new Operation<string>
        {
            Method = "POST",
            Uri = "accounts",
            Value = "{\"name\":\"example\"}"
        });

        await action.Should().ThrowAsync<OperationException<string>>();
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task BatchResponseCapturesServerDegreeOfParallelismHint()
    {
        var body = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "samples",
            "BatchResponse-Success.http"));
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        };
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(
            "multipart/mixed; boundary=batchresponse_batch-1");
        response.Headers.Add("x-ms-dop-hint", "7");
        var handler = new TestHttpMessageHandler().Enqueue(response);
        using var client = TestHttpInfrastructure.CreateClient(handler);
        var processor = new BatchProcessor(
            NullLogger.Instance,
            new TestHttpClientFactory(client),
            TestHttpInfrastructure.CreateNoOpPolicies());

        var result = await processor.ExecuteBatchAsync(new Batch<string>(
            [new Operation<string> { ContentId = "1", Method = "DELETE", Uri = "accounts(1)" }]));

        result.RecommendedDegreeOfParallelism.Should().Be(7);
    }

    [Fact]
    public void RetryDelaySupportsDeltaDateAndExceptionResults()
    {
        var now = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        using var deltaResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        deltaResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
        using var dateResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        dateResponse.Headers.RetryAfter = new RetryConditionHeaderValue(now.AddSeconds(11));

        Startup.GetRetryDelay(1, new DelegateResult<HttpResponseMessage>(deltaResponse), now)
            .Should().Be(TimeSpan.FromSeconds(7));
        Startup.GetRetryDelay(1, new DelegateResult<HttpResponseMessage>(dateResponse), now)
            .Should().Be(TimeSpan.FromSeconds(11));
        Startup.GetRetryDelay(2, new DelegateResult<HttpResponseMessage>(new HttpRequestException()), now)
            .Should().Be(TimeSpan.FromSeconds(12));
    }

    [Fact]
    public void ServiceProvidersKeepEnvironmentBaseAddressesIsolated()
    {
        using var firstProvider = new Startup(new Uri("https://first.crm.dynamics.com"))
            .ConfigureServices(new ServiceCollection())
            .BuildServiceProvider();
        using var secondProvider = new Startup(new Uri("https://second.crm.dynamics.com"))
            .ConfigureServices(new ServiceCollection())
            .BuildServiceProvider();

        var firstClient = firstProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(Globals.DataverseHttpClientName);
        var secondClient = secondProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(Globals.DataverseHttpClientName);

        firstClient.BaseAddress.Should().Be("https://first.crm.dynamics.com/api/data/v9.2/");
        secondClient.BaseAddress.Should().Be("https://second.crm.dynamics.com/api/data/v9.2/");
    }

    [Fact]
    public void AutoPaginationRequestsEveryNextLinkAndStreamsEachPage()
    {
        const string nextLink = "https://example.crm.dynamics.com/api/data/v9.2/accounts?$skiptoken=page2";
        var handler = new TestHttpMessageHandler()
            .Enqueue(JsonResponse($"{{\"value\":[{{\"name\":\"one\"}}],\"@odata.nextLink\":\"{nextLink}\"}}"))
            .Enqueue(JsonResponse("{\"value\":[{\"name\":\"two\"}]}"));
        var reporter = new RecordingOperationReporter();
        var operationHandler = new OperationHandler(CreateProcessor(handler), reporter);

        operationHandler.ExecuteSingleOperation(
            new Operation<string> { Method = "GET", Uri = "accounts" },
            "token",
            autoPagination: true,
            CancellationToken.None);

        handler.Requests.Select(request => request.Uri!.AbsoluteUri).Should().Equal(
            "https://example.crm.dynamics.com/api/data/v9.2/accounts",
            nextLink);
        reporter.Objects.Should().HaveCount(2);
        reporter.Errors.Should().BeEmpty();
    }

    private static OperationProcessor CreateProcessor(TestHttpMessageHandler handler)
    {
        var client = TestHttpInfrastructure.CreateClient(handler);
        return new OperationProcessor(
            NullLogger.Instance,
            new TestHttpClientFactory(client),
            TestHttpInfrastructure.CreateNoOpPolicies());
    }

    private static HttpResponseMessage JsonResponse(string content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return response;
    }

    private sealed class RecordingOperationReporter : IOperationReporter
    {
        public List<ErrorRecord> Errors { get; } = [];
        public List<object> Objects { get; } = [];
        public List<string> Information { get; } = [];

        public void WriteError(ErrorRecord errorRecord) => Errors.Add(errorRecord);
        public void WriteInformation(string messageData, string[] tags) => Information.Add(messageData);
        public void WriteObject(object obj) => Objects.Add(obj);
    }
}
