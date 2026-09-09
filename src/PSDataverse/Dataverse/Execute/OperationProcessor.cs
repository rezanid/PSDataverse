namespace PSDataverse.Dataverse.Execute;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Registry;
using PSDataverse.Dataverse.Model;

public class OperationProcessor : Processor<JObject>//, IBatchProcessor<JObject>
{
    private readonly ILogger log;
    private readonly HttpClient httpClient;
    private readonly IAsyncPolicy<HttpResponseMessage> policy;
    public string AuthenticationToken
    {
        set => httpClient.DefaultRequestHeaders.Authorization =
            string.IsNullOrEmpty(value) ? null : new AuthenticationHeaderValue("Bearer", value);
    }

    public OperationProcessor(
        ILogger log,
        IHttpClientFactory httpClientFactory,
        IReadOnlyPolicyRegistry<string> policyRegistry,
        string authenticationToken) : this(log, httpClientFactory, policyRegistry) => AuthenticationToken = authenticationToken;

    public OperationProcessor(
        ILogger log,
        IHttpClientFactory httpClientFactory,
        IReadOnlyPolicyRegistry<string> policyRegistry)
    {
        this.log = log;
        httpClient = httpClientFactory.CreateClient("Dataverse");
        policy = policyRegistry.Get<IAsyncPolicy<HttpResponseMessage>>(Globals.PolicyNameHttp);
    }

    public async IAsyncEnumerable<HttpResponseMessage> ProcessAsync(Batch<JObject> batch)
    {
        foreach (var operation in batch.ChangeSet.Operations)
        {
            //operation.Uri = (new Uri(ServiceUrl, operation.Uri)).ToString();
            //if (operation.Uri.EndsWith("$ref", StringComparison.OrdinalIgnoreCase))
            //{
            //    if (operation.Value["@odata.id"] != null)
            //    {
            //        operation.Value["@odata.id"] = new Uri(ServiceUrl, operation.Value["@odata.id"].ToString());
            //    }
            //}
            yield return await ExecuteAsync(operation, CancellationToken.None);
        }
    }

    public Task<HttpResponseMessage> ExecuteAsync(Operation<JObject> operation)
        => ExecuteAsync(operation, CancellationToken.None);

    public async Task<HttpResponseMessage> ExecuteAsync(Operation<JObject> operation, CancellationToken cancellationToken)
    {
        if (operation is null)
        { throw new ArgumentNullException(nameof(operation)); }

        if (!operation.Uri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            operation.Uri = new Uri(httpClient.BaseAddress, operation.Uri).ToString();
        }

        log.LogDebug($"Executing operation {operation.Method} {operation.Uri}...");
        var response = await policy.ExecuteAsync(ct => httpClient.SendAsync(operation, ct), cancellationToken);
        log.LogDebug($"Dataverse: {(int)response.StatusCode} {response.ReasonPhrase}");

        if (response.IsSuccessStatusCode)
        { return response; }

        using (response)
        {
            await ThrowOperationExceptionAsync(operation, response, cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    public Task<HttpResponseMessage> ExecuteAsync(Operation<string> operation)
        => ExecuteAsync(operation, CancellationToken.None);

    public async Task<HttpResponseMessage> ExecuteAsync(Operation<string> operation, CancellationToken cancellationToken)
    {
        if (operation is null)
        { throw new ArgumentNullException(nameof(operation)); }

        if (!operation.Uri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            operation.Uri = new Uri(httpClient.BaseAddress, operation.Uri).ToString();
        }

        log.LogDebug($"Executing operation {operation.Method} {operation.Uri}...");
        var response = await policy.ExecuteAsync(ct => httpClient.SendAsync(operation, ct), cancellationToken);
        log.LogDebug($"Dataverse: {(int)response.StatusCode} {response.ReasonPhrase}");

        if (response.IsSuccessStatusCode)
        { return response; }

        using (response)
        {
            await ThrowOperationExceptionAsync(operation, response, cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    private async Task ThrowOperationExceptionAsync(
        Operation<JObject> operation,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        operation.RunCount++;
        var error = await ExtractError(response, cancellationToken).ConfigureAwait(false);
        throw CreateOperationException(
            "operationerror",
            operation,
            new OperationResponse(
                response.StatusCode,
                string.IsNullOrEmpty(operation.ContentId) ? Guid.Empty.ToString() : operation.ContentId,
                error));
    }

    private async Task ThrowOperationExceptionAsync(
        Operation<string> operation,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        operation.RunCount++;
        var error = await ExtractError(response, cancellationToken).ConfigureAwait(false);
        throw CreateOperationException(
            "operationerror",
            operation,
            new OperationResponse(
                response.StatusCode,
                string.IsNullOrEmpty(operation.ContentId) ? Guid.Empty.ToString() : operation.ContentId,
                error));
    }

    private async Task<OperationError> ExtractError(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content == null)
        {
            log.LogWarning("Dynamics 365 returned non-success without conntent!");
            return null;
        }
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(responseContent) &&
            response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            try
            {
                var responseJson = JObject.Parse(responseContent);
                var errorJson = responseJson.SelectToken("error");
                if (errorJson is not null)
                {
                    return errorJson.ToObject<OperationError>();
                }

                if (responseJson["ErrorCode"] is not null || responseJson["Message"] is not null)
                {
                    return new OperationError
                    {
                        Code = responseJson["ErrorCode"]?.ToString(),
                        Message = responseJson["Message"]?.ToString(),
                        Type = responseJson["ExceptionType"]?.ToString(),
                        StackTrace = responseJson["StackTrace"]?.ToString()
                    };
                }
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // Fall through and preserve the status and unparsed response body.
            }
        }
        return new OperationError
        {
            Code = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
            Message = string.IsNullOrWhiteSpace(responseContent) ? response.ReasonPhrase : responseContent
        };
    }

    private OperationException<JObject> CreateOperationException(
        string batchId,
        Operation<JObject> operation,
        OperationResponse response)
    {
        var entityName = ExtractEntityName(operation);
        var errorMessage = $"{response.Error?.Code} {response.Error?.Message}";
        return new OperationException<JObject>(errorMessage)
        {
            BatchId = batchId,
            Operation = operation,
            Error = response?.Error,
            EntityName = entityName,
        };
    }

    private OperationException<string> CreateOperationException(
        string batchId,
        Operation<string> operation,
        OperationResponse response)
    {
        var entityName = ExtractEntityName(operation);
        var errorMessage = $"{response.Error?.Code} {response.Error?.Message}";
        return new OperationException<string>(errorMessage)
        {
            BatchId = batchId,
            Operation = operation,
            Error = response?.Error,
            EntityName = entityName,
        };
    }

}
