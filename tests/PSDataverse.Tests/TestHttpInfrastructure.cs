namespace PSDataverse.Tests;

using System.Collections.Concurrent;
using Polly;
using Polly.Registry;

internal sealed record RecordedHttpRequest(
    HttpMethod Method,
    Uri? Uri,
    IReadOnlyDictionary<string, string> Headers,
    string? Content);

internal sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> responders = new();
    private readonly ConcurrentQueue<RecordedHttpRequest> requests = new();

    public IReadOnlyCollection<RecordedHttpRequest> Requests => requests.ToArray();
    public int RequestCount => requests.Count;

    public TestHttpMessageHandler Enqueue(HttpResponseMessage response)
        => Enqueue((_, _) => Task.FromResult(response));

    public TestHttpMessageHandler Enqueue(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        responders.Enqueue(responder);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var contentHeaders = request.Content is null
            ? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()
            : request.Content.Headers;
        var headers = request.Headers
            .Concat(contentHeaders)
            .ToDictionary(
                header => header.Key,
                header => string.Join(",", header.Value),
                StringComparer.OrdinalIgnoreCase);
        var content = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        requests.Enqueue(new RecordedHttpRequest(request.Method, request.RequestUri, headers, content));

        if (!responders.TryDequeue(out var responder))
        {
            throw new InvalidOperationException(
                $"No HTTP response was configured for request #{RequestCount}: {request.Method} {request.RequestUri}.");
        }

        return await responder(request, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

internal static class TestHttpInfrastructure
{
    public static HttpClient CreateClient(TestHttpMessageHandler handler)
        => new(handler)
        {
            BaseAddress = new Uri("https://example.crm.dynamics.com/api/data/v9.2/")
        };

    public static PolicyRegistry CreateNoOpPolicies()
        => new()
        {
            { Globals.PolicyNameHttp, Policy.NoOpAsync<HttpResponseMessage>() },
            { Globals.PolicyNameNoRetry, Policy.NoOpAsync<HttpResponseMessage>() }
        };
}
