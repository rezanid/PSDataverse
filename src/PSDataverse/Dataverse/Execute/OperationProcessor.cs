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
            yield return await ExecuteAsync(operation);
        }
    }

    public async Task<HttpResponseMessage> ExecuteAsync(Operation<JObject> operation)
    {
        if (operation is null)
        { throw new ArgumentNullException(nameof(operation)); }

        if (!operation.Uri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            operation.Uri = new Uri(httpClient.BaseAddress, operation.Uri).ToString();
        }

        log.LogDebug($"Executing operation {operation.Method} {operation.Uri}...");
        var response = await policy.ExecuteAsync(() => httpClient.SendAsync(operation, CancellationToken.None));
        log.LogDebug($"Dataverse: {(int)response.StatusCode} {response.ReasonPhrase}");

        if (response.IsSuccessStatusCode)
        { return response; }

        await ThrowOperationExceptionAsync(operation, response);
        return null;
    }

    public async Task<HttpResponseMessage> ExecuteAsync(Operation<string> operation)
    {
        if (operation is null)
        { throw new ArgumentNullException(nameof(operation)); }

        if (!operation.Uri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            operation.Uri = new Uri(httpClient.BaseAddress, operation.Uri).ToString();
        }

        log.LogDebug($"Executing operation {operation.Method} {operation.Uri}...");
        var response = await policy.ExecuteAsync(() => httpClient.SendAsync(operation, CancellationToken.None));
        log.LogDebug($"Dataverse: {(int)response.StatusCode} {response.ReasonPhrase}");

        if (response.IsSuccessStatusCode)
        { return response; }

        await ThrowOperationExceptionAsync(operation, response);
        return null;
    }

    private async Task ThrowOperationExceptionAsync(Operation<JObject> operation, HttpResponseMessage response)
    {
        operation.RunCount++;
        var error = await ExtractError(response);
        throw CreateOperationException(
            "operationerror",
            operation,
            new OperationResponse(
                response.StatusCode,
                string.IsNullOrEmpty(operation.ContentId) ? Guid.Empty.ToString() : operation.ContentId,
                error));
    }

    private async Task ThrowOperationExceptionAsync(Operation<string> operation, HttpResponseMessage response)
    {
        operation.RunCount++;
        var error = await ExtractError(response);
        throw CreateOperationException(
            "operationerror",
            operation,
            new OperationResponse(
                response.StatusCode,
                string.IsNullOrEmpty(operation.ContentId) ? Guid.Empty.ToString() : operation.ContentId,
                error));
    }

    private async Task<OperationError> ExtractError(HttpResponseMessage response)
    {
        if (response.Content == null)
        {
            log.LogWarning("Dynamics 365 returned non-success without conntent!");
            return null;
        }
        var responseContent = await response.Content.ReadAsStringAsync();
        response.Content.Dispose();
        if (!string.IsNullOrEmpty(responseContent) && response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            var responseJson = JObject.Parse(responseContent);
            var errorJson = responseJson.SelectToken("error");
            if (errorJson == null)
            {
                return new OperationError
                {
                    Code = responseJson["ErrorCode"].ToString(),
                    // Ignore ErrorMessage because it is always the same as Message.
                    Message = responseJson["Message"].ToString(),
                    Type = responseJson["ExceptionType"].ToString(),
                    StackTrace = responseJson["StackTrace"].ToString()
                };
            }
            return errorJson.ToObject<OperationError>();
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
