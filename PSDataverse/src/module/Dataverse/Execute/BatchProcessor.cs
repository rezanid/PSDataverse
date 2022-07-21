using Microsoft.Extensions.Logging;
using PSDataverse.Dataverse.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Threading;
using System.Globalization;

namespace PSDataverse.Dataverse.Execute
{
    public class BatchProcessor : Processor<JObject>, IBatchProcessor<JObject>
    {
        readonly ILogger Log;
        readonly HttpClient HttpClient;
        readonly IAsyncPolicy<HttpResponseMessage> Retry;
        readonly bool canThrowOperationException;
        public string AuthenticationToken
        {
            set
            {
                HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", value);
            }
        }
        public BatchProcessor(
            ILogger log,
            IHttpClientFactory httpClientFactory,
            IReadOnlyPolicyRegistry<string> policyRegistry,
            string authenticationToken) : this(log, httpClientFactory, policyRegistry)
        {
            canThrowOperationException = false;
            AuthenticationToken = authenticationToken;
        }

        public BatchProcessor(
            ILogger log,
            IHttpClientFactory httpClientFactory,
            IReadOnlyPolicyRegistry<string> policyRegistry)
        {
            Log = log;
            HttpClient = httpClientFactory.CreateClient("Dataverse");
            Retry = policyRegistry.Get<IAsyncPolicy<HttpResponseMessage>>(Globals.PolicyNameHttp);
        }

        // public async IAsyncEnumerable<HttpResponseMessage> ProcessAsync(Batch<JObject> batch)
        public async IAsyncEnumerable<BatchResponse> ProcessAsync(Batch<JObject> batch)
        {
            //foreach (var operation in batch.ChangeSet.Operations)
            //{
            //    operation.Uri = new Uri(ServiceUrl, operation.Uri).ToString();
            //    if (operation.Uri.EndsWith("$ref", StringComparison.OrdinalIgnoreCase))
            //    {
            //        if (operation.Value["@odata.id"] != null)
            //        {
            //            operation.Value["@odata.id"] = new Uri(ServiceUrl, operation.Value["@odata.id"].ToString());
            //        }
            //    }
            //}
            //TODO: Parse all messages in the response and yield-return them separately.
            yield return await ExecuteBatchAsync(batch);
        }

        public Task<BatchResponse> ExecuteBatchAsync(Batch<JObject> batch) => ExecuteBatchAsync(batch, CancellationToken.None);
        public async Task<BatchResponse> ExecuteBatchAsync(Batch<JObject> batch, CancellationToken cancellationToken)
        {
            // Make the request
            var response = await SendBatchAsync(batch, cancellationToken);
            Log.LogDebug($"Dynamics 365: {(int)response.StatusCode} {response.ReasonPhrase}");

            // Extract the response content
            string responseContent = null;
            if (response.Content != null)
            {
                responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                response.Content.Dispose();
            }

            // Catch throtelling exceptions
            WebApiFault details = null;
            if (!response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == MediaTypeNames.Application.Json)
            {
                details = JsonConvert.DeserializeObject<WebApiFault>(responseContent);
            }
            if (response.StatusCode == (HttpStatusCode)429)
            {
                details.RetryAfter = response.Headers.RetryAfter?.Delta;
                throw new ThrottlingExceededException(details);
            }
            if (response.Headers.RetryAfter != null)
            {
                details = new WebApiFault
                {
                    Message = $"Response status code does not indicate success: " +
                        $"{response.StatusCode} ({response.ReasonPhrase}) content: {responseContent}.",
                    ErrorCode = (int)response.StatusCode,
                    RetryAfter = response.Headers.RetryAfter.Delta
                };
                throw new ThrottlingExceededException(details);
            }

            if (response.Content.Headers.ContentType != null && !string.Equals("multipart/mixed", response.Content.Headers.ContentType.MediaType, StringComparison.OrdinalIgnoreCase))
            {
                throw new ParseException($"Unsupported response media type received from Dataverse. Expected: multipart/mixed, Actual: " + response.Content.Headers.ContentType.MediaType);
            }

            if (!response.IsSuccessStatusCode && response.Content.Headers.ContentLength == 0)
            {
                throw new BatchException<JObject>($"{(int)response.StatusCode} {response.ReasonPhrase}")
                {
                    Batch = batch
                };
            }

            BatchResponse batchResponse = null;
            try
            {
                // Try to parse the response content like a batch response
                batchResponse = BatchResponse.Parse(responseContent);
            }
            catch (Exception)
            {
                Log.LogWarning("It is not possible to parse the CRM response!\r\n" + responseContent);
            }

            if (batchResponse.IsSuccessful)
            {
                return batchResponse;
            }

            Log.LogDebug("Dynamics 365 response: " + responseContent);

            var failedOperationResponse = batchResponse.Operations.First();
            var failedOperation = batch.ChangeSet.Operations.FirstOrDefault(
                o => o.ContentId == failedOperationResponse.ContentId);
            Log.LogWarning($"Failed operation: {failedOperation}.");
            failedOperation.RunCount++;

            if (canThrowOperationException)
            {
                throw CreateOperationException(batch.Id, failedOperation, failedOperationResponse);
            }
            else
            {
                return batchResponse;
            }
        }

        private Task<HttpResponseMessage> SendBatchAsync(Batch<JObject> batch) => SendBatchAsync(batch, CancellationToken.None);

        private async Task<HttpResponseMessage> SendBatchAsync(Batch<JObject> batch, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            if (batch is null) { throw new ArgumentNullException(nameof(batch)); }
            if (batch.Id == null) { throw new ArgumentException("Batch.Id cannot be null."); }

            Log.LogDebug($"Executing batch {batch.Id}...");
            response = await Retry.ExecuteAsync(() => HttpClient.SendAsync(HttpMethod.Post, "$batch", batch, cancellationToken));
            if (response.IsSuccessStatusCode)
            {
                Log.LogDebug($"Batch {batch.Id} succeeded.");
                return response;
            }
            if (response != null)
            {
                return response;
            }
            throw new HttpRequestException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Response status code does not indicate success: {0} ({1}) and the response contains no content.",
                    response.StatusCode,
                    response.ReasonPhrase));
        }

        private OperationException CreateOperationException(
            string batchId,
            Operation<JObject> operation,
            OperationResponse response)
        {
            var entityName = ExtractEntityName(operation);
            var errorMessage = response.Error.Message;
            //if (operationExceptions.TryGetValue(entityName, out OperationException exception))
            //{
            //    exception.Message = errorMessage;
            //    exception.BatchId = batchId;
            //    exception.Operation = operation;
            //    exception.Error = response.Error;
            //    return exception;
            //}
            return new OperationException(errorMessage)
            {
                BatchId = batchId,
                Operation = operation,
                Error = response.Error,
                EntityName = entityName,
            };
        }

        private static void ThrowGeneralException(HttpResponseMessage response)
        {
            string responseContent = null;
            if (response.Content != null)
            {
                responseContent = response.Content.ReadAsStringAsync().Result;
                response.Content.Dispose();
            }

            WebApiFault details;
            if (response.StatusCode == (HttpStatusCode)429)
            {
                details = JsonConvert.DeserializeObject<WebApiFault>(responseContent);
                details.RetryAfter = response.Headers.RetryAfter?.Delta;
                throw new ThrottlingExceededException(details);
            }
            if (response.Headers.RetryAfter != null)
            {
                details = new WebApiFault
                {
                    Message = $"Response status code does not indicate success: " +
                        $"{response.StatusCode} ({response.ReasonPhrase}) content: {responseContent}.",
                    ErrorCode = (int)response.StatusCode,
                    RetryAfter = response.Headers.RetryAfter.Delta
                };
                throw new ThrottlingExceededException(details);
            }

            throw new HttpRequestException(
                $"Response status code does not indicate success: " +
                $"{(int)response.StatusCode} ({response.ReasonPhrase}) content: {responseContent}.");
        }
    }
}
