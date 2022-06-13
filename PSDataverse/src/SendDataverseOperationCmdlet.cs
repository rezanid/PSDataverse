using Microsoft.Extensions.DependencyInjection;
using DataverseModule.Dataverse;
using DataverseModule.Dataverse.Execute;
using DataverseModule.Dataverse.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace DataverseModule
{
    [Cmdlet(VerbsCommunications.Send, "DataverseOperation")]
    public class SendDataverseOperationCmdlet : PSCmdlet
    {
        [Parameter(Position=0, Mandatory = true, ParameterSetName = "Object")]
        public Operation<JObject> Operation { get; set; }

        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Json", ValueFromPipeline = true)]
        public string OperationJson { get; set; }

        [Parameter(Position = 1, Mandatory = false)]
        public int BatchCapacity { get; set; } = 0;

        [Parameter(Position = 2, Mandatory = false)]
        public int MaxDop { get; set; } = 0;

        [Parameter(Position=3, Mandatory = false)]
        public int Retry { get; set; }

        [Parameter(Position = 4, Mandatory = false)]
        public bool OutputTable { get; set; }

        private bool isValidationFailed;
        private string accessToken;
        private DataverseConnectionString dataverseCnnStr;
        private DateTimeOffset? authExpiresOn;
        private OperationProcessor operationProcessor;
        private BatchProcessor batchProcessor;
        private int operationCounter = 0;
        private int batchCounter = 0;
        private ConcurrentBag<Operation<JObject>> operations;// List<Operation<JObject>> operations;
        //private ConcurrentBag<Task<(TimeSpan, BatchResponse)>> batches;
        private ConcurrentBag<BatchPointer<JObject>> batchPointers;
        private Stopwatch stopwatch;
        private SemaphoreSlim throttler;
        private static readonly string[] validMethodsWithoutPayload = {"GET","DELETE"};

        protected override void BeginProcessing()
        {
            base.BeginProcessing();
            
            stopwatch = Stopwatch.StartNew();

            if (!VerifyConnection()) 
            {
                isValidationFailed = true;
                return; 
            }

            var serviceProvider = (IServiceProvider)GetVariableValue(Globals.VariableNameServiceProvider);
            operationProcessor = serviceProvider.GetService<OperationProcessor>();
            batchProcessor = serviceProvider.GetService<BatchProcessor>();
            if (BatchCapacity > 0)
            {
                operations = new(new List<Operation<JObject>>(BatchCapacity));
                // batches = new();
                batchPointers = new();
                throttler = new SemaphoreSlim(MaxDop <= 0 ? 20 : MaxDop);
            }
            operationCounter = 0;
        }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            if (isValidationFailed) { return; }
            if (!VerifyConnection()) { return; }

            var op = Operation;
            if (op == null && string.IsNullOrEmpty(OperationJson))
            {
                var errMessage = "No operation has been given. Please provide an operation using either of -Operation or -OperationJson arguments.";
                WriteError(new ErrorRecord(new Exception(errMessage), Globals.ErrorIdMissingOperation, ErrorCategory.ConnectionError, null));
                WriteObject(errMessage);
                return;
            }
            if (op == null)
            {
                WriteDebug("JSON: " + OperationJson);
                op = JsonConvert.DeserializeObject<Operation<JObject>>(OperationJson);
            }
            if (op == null || string.IsNullOrEmpty(op.Method)) 
            {
                throw new InvalidOperationException("Operation parameter is not provided or it has no 'Method'.");
            }
            if (!validMethodsWithoutPayload.Contains(op.Method, StringComparer.OrdinalIgnoreCase) && op.Value == null)
            {
                throw new InvalidOperationException($"Operation does not have a 'Value' but it has a {op.Method} method. All operations with non-GET method should have a value.");
            }

            Interlocked.Increment(ref operationCounter);

            if (BatchCapacity <= 0)
            {
                operationProcessor.AuthenticationToken = accessToken;
                try
                {
                    //var enumerator = operationProcessor.ProcessAsync(new Batch<JObject>(new List<Operation<JObject>>() { op })).ConfigureAwait(false).GetAsyncEnumerator();
                    var response = operationProcessor.ExecuteAsync(op).Result;
                    var opResponse = OperationResponse.From(response);
                    if (string.IsNullOrEmpty(opResponse.ContentId)) { opResponse.ContentId = op.ContentId; }
                    WriteObject(opResponse);
                }
                catch (OperationException ex)
                {
                    WriteError(new ErrorRecord(ex, Globals.ErrorIdOperationException, ErrorCategory.WriteError, null));
                }
                catch (AggregateException ex) when (ex.InnerException is OperationException)
                {
                    WriteError(new ErrorRecord(ex.InnerException, Globals.ErrorIdOperationException, ErrorCategory.WriteError, null));
                }
                WriteInformation("Dataverse operation successfull.", new string[] { "dataverse" });
                return;
            }

            operations.Add(op);

            if (IsNewBatchNeeded())
            {
                batchProcessor.AuthenticationToken = accessToken;
                SendBatch();
            }
        }

        private bool VerifyConnection()
        {
            accessToken = (string)GetVariableValue(Globals.VariableNameAccessToken);
            authExpiresOn = (DateTimeOffset?)GetVariableValue(Globals.VariableNameAccessTokenExpiresOn);
            dataverseCnnStr = (DataverseConnectionString)GetVariableValue(Globals.VariableNameConnectionString);
            if (string.IsNullOrEmpty(accessToken))
            {
                var errMessage = "No active connection detect. Please first authenticate using Connect-Dataverse cmdlet.";
                WriteError(new ErrorRecord(new Exception(errMessage), Globals.ErrorIdNotConnected, ErrorCategory.ConnectionError, null));
                return false;
            }
            if (authExpiresOn <= DateTimeOffset.Now && dataverseCnnStr == null)
            {
                var errMessage = "Active connection has expired. Please authenticate again using Connect-Dataverse cmdlet.";
                WriteError(new ErrorRecord(new Exception(errMessage), Globals.ErrorIdConnectionExpired, ErrorCategory.ConnectionError, null));
                return false;
            }
            if (dataverseCnnStr != null && authExpiresOn <= DateTimeOffset.Now) {
                var certificate = AuthenticationHelper.FindCertificate(dataverseCnnStr.CertificationThumbprint, StoreName.My);
                var authResult = AuthenticationHelper.Authenticate(dataverseCnnStr.Authority, dataverseCnnStr.ClientId, dataverseCnnStr.Resource, certificate);
                SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessToken, authResult.AccessToken, ScopedItemOptions.AllScope));
                SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessTokenExpiresOn, authResult.ExpiresOn, ScopedItemOptions.AllScope));
            }
            return true;
        }

        protected override void EndProcessing()
        {
            base.EndProcessing();

            if (!operations?.IsEmpty ?? false)
            {
                SendBatch();
            }
            // if (batches?.Any() ?? false)
            if (batchPointers?.Any() ?? false)
            {
                try
                {
                    // Task.WaitAll(batches.ToArray());
                    Task.WaitAll(batchPointers.Select(b => b.Task).ToArray());
                }
                catch (AggregateException ex)
                {
                    WriteWarning($"Batches were executed with failures. Error count: {ex.InnerExceptions.Count}.");
                    //foreach (var exception in ex.InnerExceptions)
                    //{
                    //    WriteError(new ErrorRecord(exception, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
                    //}
                }
            }
            // if (batches == null) { 
            if (batchPointers == null) { 
                stopwatch.Stop();
                WriteInformation($"Send-Dataverse completed - Elapsed: {stopwatch.Elapsed}, Batches: {batchCounter}, Operations: {operationCounter}.", new string[] { "Dataverse" });
                return; 
            }
            // foreach (var item in batches)
            foreach (var item in batchPointers)
            {
                // item.Key   : Task<TimeSpan, BatchResult>
                // item.Value : Batch

                // if (item.IsFaulted)
                if (item.Task.IsFaulted)
                {
                    var exception = new BatchException<JObject>("Batch has been faild due to an error", item.Task.Exception.InnerException)
                    {
                        Batch = item.Batch
                    };
                    // WriteError(new ErrorRecord(item.Exception.InnerException, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
                    WriteError(new ErrorRecord(exception, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
                }
                else if (item.Task.IsCanceled)
                {
                    var exception = new BatchException<JObject>("Batch has been canceled") 
                    {
                        Batch = item.Batch
                    };
                    WriteError(new ErrorRecord(exception, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
                }
                else
                {
                    if (!OutputTable)
                    {
                        WriteObject(item.Task.Result.Response);
                    }
                    else
                    {
                        var tbl = new System.Data.DataTable();
                        tbl.Columns.Add("Id", typeof(string));
                        tbl.Columns.Add("Response", typeof(string));
                        tbl.Columns.Add("Succeeded", typeof(bool));
                        foreach (var response in item.Task.Result.Response.Operations)
                        {
                            tbl.Rows.Add(
                                response.ContentId,
                                response.Headers != null && response.Headers.TryGetValue("OData-EntityId", out var id) ? id : null, 
                                true);
                        }
                        WriteObject(tbl);
                    }
                }
            }
            stopwatch.Stop();
            WriteInformation($"Send-Dataverse completed - Elapsed: {stopwatch.Elapsed}, Batches: {batchCounter}, Operations: {operationCounter}.", new string[] { "Dataverse" });
        }

        private bool IsNewBatchNeeded() => BatchCapacity > 0 && operationCounter == 0 || operationCounter % BatchCapacity == 0;

        private void SendBatch()
        {
            // batches.Add(SendBatchAsync());
            batchPointers.Add(MakeAndSendBatch());
        }

        private BatchPointer<JObject> MakeAndSendBatch() 
        {
            var batch = new Batch<JObject>(operations);
            operations.Clear();
            var task = SendBatchAsync(batch);
            return new BatchPointer<JObject>(batch, task);
        }

        // private async Task<(TimeSpan, Batch<JObject>)> SendBatchAsync()
        // private async Task<(TimeSpan, BatchResponse)> SendBatchAsync()
        private async Task<BatchResult> SendBatchAsync(Batch<JObject> batch)
        {
            var batchStopWatch = Stopwatch.StartNew();
            // var batch = new Batch<JObject>(operations);
            // operations.Clear();

            WriteInformation($"Batch-{batch.Id}[total:{batch.ChangeSet.Operations.Count()}, starting: {batch.ChangeSet.Operations.First().ContentId}] being sent...", new string[] { "dataverse" });
            BatchResponse response = null;
            try 
            {
                await throttler.WaitAsync();
                response = await batchProcessor.ExecuteBatchAsync(batch);
            }
            finally
            {
                throttler.Release();
            }

            WriteInformation($"Batch-{batch.Id} completed.", new string[] { "dataverse" });
            
            Interlocked.Increment(ref batchCounter);
            batchStopWatch.Stop();
            //return (batchStopWatch.Elapsed, response);
            return new BatchResult(batchStopWatch.Elapsed, response); 
        }

        private void CleanupTasks() 
        {
            foreach (var pointer in batchPointers.Where(p => p.Task.Status == TaskStatus.RanToCompletion))
            {
                
            }
        }
    }

    public class BatchPointer<T>
    {
        public Batch<T> Batch { get; set; }
        public Task<BatchResult> Task { get; set; }

        public BatchPointer(Batch<T> batch, Task<BatchResult> task)
        {
            Batch = batch;
            Task = task;
        }
    }

    public class BatchResult
    {
        public TimeSpan Elapsed { get; set; }
        public BatchResponse Response { get; set; }
        public BatchResult(TimeSpan elapsed, BatchResponse response)
        {
            Elapsed = elapsed;
            Response = response;
        }
    }
}