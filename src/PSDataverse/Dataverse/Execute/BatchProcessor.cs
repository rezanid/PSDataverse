namespace PSDataverse.Dataverse.Execute;

using System;
using System.Collections.Generic;
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
    private readonly ILogger log;
    private readonly HttpClient httpClient;
    private readonly IAsyncPolicy<HttpResponseMessage> retry;

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
        retry = policyRegistry.Get<IAsyncPolicy<HttpResponseMessage>>(Globals.PolicyNameHttp);
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

        using var response = await retry.ExecuteAsync(
            ct => httpClient.SendAsync(HttpMethod.Post, "$batch", batch, ct),
            cancellationToken).ConfigureAwait(false);

        log.LogDebug("Dataverse batch {BatchId} returned {StatusCode} {ReasonPhrase}.",
            batch.Id, (int)response.StatusCode, response.ReasonPhrase);

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
            if (!batchResponse.IsSuccessful)
            {
                var failedResponse = batchResponse.Operations.FirstOrDefault();
                var failedOperation = batch.ChangeSet?.Operations?.FirstOrDefault(
                    operation => operation.ContentId == failedResponse?.ContentId);
                if (failedOperation is not null)
                {
                    failedOperation.RunCount++;
                    log.LogWarning("Dataverse batch {BatchId} failed at operation {Operation}.", batch.Id, failedOperation);
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

    private static string Limit(string value)
        => string.IsNullOrEmpty(value)
            ? "<empty>"
            : value.Length <= MaxErrorBodyLength ? value : value[..MaxErrorBodyLength] + "…";
}
