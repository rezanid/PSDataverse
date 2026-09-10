namespace PSDataverse.Dataverse.Execute;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Registry;
using PSDataverse.Dataverse.Model;

public class BatchProcessor : Processor<JObject>, IBatchProcessor<JObject>
{
    private const int MaxErrorBodyLength = 2048;
    private static readonly Action<ILogger, string, int, string, Exception> LogBatchResponse =
        LoggerMessage.Define<string, int, string>(
            LogLevel.Debug,
            new EventId(1, nameof(LogBatchResponse)),
            "Dataverse batch {BatchId} returned {StatusCode} {ReasonPhrase}.");
    private static readonly Action<ILogger, string, object, Exception> LogBatchOperationFailure =
        LoggerMessage.Define<string, object>(
            LogLevel.Warning,
            new EventId(2, nameof(LogBatchOperationFailure)),
            "Dataverse batch {BatchId} failed at operation {Operation}.");
    private readonly ILogger log;
    private readonly HttpClient httpClient;
    private readonly IAsyncPolicy<HttpResponseMessage> retryPolicy;
    private readonly IAsyncPolicy<HttpResponseMessage> noRetryPolicy;

    public string AuthenticationToken
    {
        set => httpClient.DefaultRequestHeaders.Authorization =
            string.IsNullOrEmpty(value) ? null : new AuthenticationHeaderValue("Bearer", value);
    }

    public BatchProcessor(
        ILogger log,
        IHttpClientFactory httpClientFactory,
        IReadOnlyPolicyRegistry<string> policyRegistry,
        string authenticationToken) : this(log, httpClientFactory, policyRegistry)
        => AuthenticationToken = authenticationToken;

    public BatchProcessor(
        ILogger log,
        IHttpClientFactory httpClientFactory,
        IReadOnlyPolicyRegistry<string> policyRegistry)
    {
        this.log = log;
        httpClient = httpClientFactory.CreateClient(Globals.DataverseHttpClientName);
        retryPolicy = policyRegistry.Get<IAsyncPolicy<HttpResponseMessage>>(Globals.PolicyNameHttp);
        noRetryPolicy = policyRegistry.Get<IAsyncPolicy<HttpResponseMessage>>(Globals.PolicyNameNoRetry);
    }

    public async IAsyncEnumerable<BatchResponse> ProcessAsync(Batch<JObject> batch)
    {
        yield return await ExecuteBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
    }

    public Task<BatchResponse> ExecuteBatchAsync(Batch<JObject> batch)
        => ExecuteBatchAsync(batch, CancellationToken.None);

    public Task<BatchResponse> ExecuteBatchAsync(Batch<string> batch)
        => ExecuteBatchAsync(batch, CancellationToken.None);

    public Task<BatchResponse> ExecuteBatchAsync(Batch<JObject> batch, CancellationToken cancellationToken)
        => ExecuteBatchCoreAsync(batch, cancellationToken);

    public Task<BatchResponse> ExecuteBatchAsync(Batch<string> batch, CancellationToken cancellationToken)
        => ExecuteBatchCoreAsync(batch, cancellationToken);

    private async Task<BatchResponse> ExecuteBatchCoreAsync<T>(Batch<T> batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (string.IsNullOrWhiteSpace(batch.Id))
        {
            throw new ArgumentException("Batch.Id cannot be null or empty.", nameof(batch));
        }

        var policy = batch.ChangeSet?.Operations?.All(operation => HttpReplaySafety.IsReplaySafe(operation.Method)) == true
            ? retryPolicy
            : noRetryPolicy;
        using var response = await policy.ExecuteAsync(
            ct => httpClient.SendAsync(HttpMethod.Post, "$batch", batch, ct),
            cancellationToken).ConfigureAwait(false);

        LogBatchResponse(log, batch.Id, (int)response.StatusCode, response.ReasonPhrase, null);

        var mediaType = response.Content?.Headers.ContentType?.MediaType;
        var responseContent = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            WebApiFault details = null;
            if (string.Equals(mediaType, MediaTypeNames.Application.Json, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(responseContent))
            {
                try
                {
                    details = JsonConvert.DeserializeObject<WebApiFault>(responseContent);
                }
                catch (JsonException)
                {
                    // Preserve the original status and body in the fallback fault below.
                }
            }

            details ??= CreateFault(response, responseContent);
            details.RetryAfter = response.Headers.RetryAfter?.Delta;
            throw new ThrottlingExceededException(details);
        }

        if (!response.IsSuccessStatusCode)
        {
            if (response.Headers.RetryAfter is not null)
            {
                throw new ThrottlingExceededException(CreateFault(response, responseContent));
            }

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                throw new BatchException<T>($"Dataverse returned {(int)response.StatusCode} {response.ReasonPhrase} with an empty response body.")
                {
                    Batch = batch
                };
            }
        }

        if (!string.Equals(mediaType, "multipart/mixed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ParseException(
                $"Unsupported Dataverse batch response media type '{mediaType ?? "<none>"}'. " +
                $"Expected 'multipart/mixed'. Body: {Limit(responseContent)}");
        }

        try
        {
            var batchResponse = BatchResponse.Parse(responseContent);
            batchResponse.RecommendedDegreeOfParallelism = GetRecommendedDegreeOfParallelism(response);
            if (!batchResponse.IsSuccessful)
            {
                var failedResponse = batchResponse.Operations.FirstOrDefault();
                var failedOperation = batch.ChangeSet?.Operations?.FirstOrDefault(
                    operation => operation.ContentId == failedResponse?.ContentId);
                if (failedOperation is not null)
                {
                    failedOperation.RunCount++;
                    LogBatchOperationFailure(log, batch.Id, failedOperation, null);
                }
            }
            return batchResponse;
        }
        catch (ParseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ParseException(
                $"Unable to parse the Dataverse batch response. Body: {Limit(responseContent)}",
                ex);
        }
    }

    private static WebApiFault CreateFault(HttpResponseMessage response, string responseContent)
        => new()
        {
            Message = $"Dataverse returned {(int)response.StatusCode} {response.ReasonPhrase}. Body: {Limit(responseContent)}",
            ErrorCode = (int)response.StatusCode,
            RetryAfter = response.Headers.RetryAfter?.Delta
        };

    private static int? GetRecommendedDegreeOfParallelism(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("x-ms-dop-hint", out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault();
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hint) && hint > 0
            ? hint
            : null;
    }

    private static string Limit(string value)
        => string.IsNullOrEmpty(value)
            ? "<empty>"
            : value.Length <= MaxErrorBodyLength ? value : value[..MaxErrorBodyLength] + "…";
}
