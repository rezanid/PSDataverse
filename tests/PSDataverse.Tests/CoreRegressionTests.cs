namespace PSDataverse.Tests;

using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Registry;
using PSDataverse.Dataverse;
using PSDataverse.Dataverse.Execute;
using PSDataverse.Dataverse.Model;

public class CoreRegressionTests
{
    [Fact]
    public void ChangeSetRemovesOperationByExactContentId()
    {
        var first = new Operation<string> { ContentId = "1", Method = "DELETE", Uri = "accounts(1)" };
        var second = new Operation<string> { ContentId = "10", Method = "DELETE", Uri = "accounts(10)" };
        var changeSet = new ChangeSet<string> { Operations = [first, second] };

        changeSet.RemoveOperation("1");

        changeSet.Operations.Should().ContainSingle().Which.Should().BeSameAs(second);
    }

    [Fact]
    public void ChangeSetRejectsUnknownContentId()
    {
        var changeSet = new ChangeSet<string>
        {
            Operations = [new Operation<string> { ContentId = "1" }]
        };

        var action = () => changeSet.RemoveOperation("missing");

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 1)]
    [InlineData(64, 64)]
    public void MaxDopUsesABoundedDefault(int input, int expected)
        => SendDataverseOperationCmdlet.ResolveMaxDop(input).Should().Be(expected);

    [Fact]
    public void OperationResponsePreservesCreatedResponseBodyAndHeaders()
    {
        using var message = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("{\"accountid\":\"42\"}")
        };
        message.Headers.Add("Content-ID", "7");
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = OperationResponse.From(message);

        response.Should().NotBeNull();
        response!.ContentId.Should().Be("7");
        response.Content.Should().Be("{\"accountid\":\"42\"}");
        response.Headers.Should().ContainKey("Content-Type");
        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task BatchProcessorReportsEmptyServerErrorWithoutNullReference()
    {
        var processor = CreateProcessor(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var batch = CreateBatch();

        var action = () => processor.ExecuteBatchAsync(batch);

        var exception = await action.Should().ThrowAsync<BatchException<string>>();
        exception.Which.Message.Should().Contain("empty response body");
        exception.Which.Batch.Should().BeSameAs(batch);
    }

    [Fact]
    public async Task BatchProcessorPreservesThrottleStatusForNonJsonResponse()
    {
        var processor = CreateProcessor(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("service is busy")
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
            return response;
        });

        var action = () => processor.ExecuteBatchAsync(CreateBatch());

        var exception = await action.Should().ThrowAsync<ThrottlingExceededException>();
        exception.Which.Details.ErrorCode.Should().Be(429);
        exception.Which.Details.RetryAfter.Should().Be(TimeSpan.FromSeconds(5));
        exception.Which.Message.Should().Contain("service is busy");
    }

    [Fact]
    public async Task OperationProcessorPreservesMalformedJsonErrorBody()
    {
        var client = CreateClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("{not-json")
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        });
        var processor = new OperationProcessor(
            NullLogger.Instance,
            new StubHttpClientFactory(client),
            CreatePolicies());

        var action = () => processor.ExecuteAsync(
            new Operation<string> { Method = "GET", Uri = "accounts" });

        var exception = await action.Should().ThrowAsync<OperationException<string>>();
        exception.Which.Error.Code.Should().Be("502");
        exception.Which.Error.Message.Should().Be("{not-json");
    }

    [Fact]
    public void BatchResponseRejectsAResponseWithoutOperations()
    {
        const string response = "--batchresponse_1\r\n" +
                                "Content-Type: multipart/mixed; boundary=changesetresponse_2\r\n\r\n" +
                                "--batchresponse_1--\r\n";

        var action = () => BatchResponse.Parse(response);

        action.Should().Throw<ParseException>().WithMessage("*did not contain any operation responses*");
    }

    private static Batch<string> CreateBatch()
        => new([new Operation<string> { ContentId = "1", Method = "DELETE", Uri = "accounts(1)" }]);

    private static BatchProcessor CreateProcessor(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var client = CreateClient(responder);
        return new BatchProcessor(NullLogger.Instance, new StubHttpClientFactory(client), CreatePolicies());
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new(new StubHandler(responder))
        {
            BaseAddress = new Uri("https://example.crm.dynamics.com/api/data/v9.2/")
        };

    private static PolicyRegistry CreatePolicies()
        => new()
        {
            { Globals.PolicyNameHttp, Policy.NoOpAsync<HttpResponseMessage>() }
        };

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
