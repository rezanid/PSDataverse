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
        private ConcurrentBag<Operation<JObject>> operations;
        private ConcurrentBag<BatchPointer<JObject>> batchPointers;
        private List<Task<Batch<JObject>>> tasks;
        private Stopwatch stopwatch;
        private SemaphoreSlim taskThrottler;
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
                batchPointers = new();
                tasks = new();
                taskThrottler = new SemaphoreSlim(MaxDop <= 0 ? 20 : MaxDop);
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
                //TODO: Temporary
                //SendBatch();
                MakeAndSendBatchThenOutput(waitForAll: false);
            }
        }

        protected override void EndProcessing()
        {
            base.EndProcessing();

            if (tasks?.Count > 0 || (operations?.Any() ?? false))
            {
                MakeAndSendBatchThenOutput(waitForAll: true);
            }
            if (BatchCapacity == 0)
            {
                stopwatch.Stop();
                WriteInformation($"Send-Dataverse completed - Elapsed: {stopwatch.Elapsed}, Batches: {batchCounter}, Operations: {operationCounter}.", new string[] { "Dataverse" });
                return;
            }
            stopwatch.Stop();
            taskThrottler.Release();
            taskThrottler.Dispose();
            WriteInformation($"Send-Dataverse completed - Elapsed: {stopwatch.Elapsed}, Batches: {batchCounter}, Operations: {operationCounter}.", new string[] { "Dataverse" });
        }

        //TODO:Temporary
        protected void OldEndProcessing()
        {
            base.EndProcessing();

            if (operations?.Any() ?? false)
            {
                SendBatch();
            }
            if (batchPointers?.Any() ?? false)
            {
                try
                {
                    Task.WaitAll(batchPointers.Select(b => b.Task).ToArray());
                }
                catch (AggregateException ex)
                {
                    WriteWarning($"Batches were executed with failures. Error count: {ex.InnerExceptions.Count}.");
                }
            }
            if (batchPointers == null) { 
                stopwatch.Stop();
                WriteInformation($"Send-Dataverse completed - Elapsed: {stopwatch.Elapsed}, Batches: {batchCounter}, Operations: {operationCounter}.", new string[] { "Dataverse" });
                return; 
            }
            foreach (var item in batchPointers)
            {
                OutputBatchPointer(item);
            }
            stopwatch.Stop();
            taskThrottler.Release();
            taskThrottler.Dispose();
            WriteInformation($"Send-Dataverse completed - Elapsed: {stopwatch.Elapsed}, Batches: {batchCounter}, Operations: {operationCounter}.", new string[] { "Dataverse" });
        }

        private void OutputBatchPointer(BatchPointer<JObject> batchPointer)
        {
            if (batchPointer.Task.IsFaulted)
            {
                var exception = new BatchException<JObject>("Batch has been faild due to an error", batchPointer.Task.Exception.InnerException)
                {
                    Batch = batchPointer.Batch
                };
                WriteError(new ErrorRecord(exception, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
            }
            else if (batchPointer.Task.IsCanceled)
            {
                var exception = new BatchException<JObject>("Batch has been canceled")
                {
                    Batch = batchPointer.Batch
                };
                WriteError(new ErrorRecord(exception, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
            }
            else
            {
                if (!OutputTable)
                {
                    WriteObject(batchPointer.Task.Result.Response);
                }
                else
                {
                    var tbl = new System.Data.DataTable();
                    tbl.Columns.Add("Id", typeof(string));
                    tbl.Columns.Add("BatchId", typeof(Guid));
                    tbl.Columns.Add("Response", typeof(string));
                    tbl.Columns.Add("Succeeded", typeof(bool));
                    foreach (var response in batchPointer.Task.Result.Response.Operations)
                    {
                        tbl.Rows.Add(
                            response.ContentId,
                            batchPointer.Batch.Id,
                            response.Headers != null && response.Headers.TryGetValue("OData-EntityId", out var id) ? id : null,
                            true);
                    }
                    WriteObject(tbl);
                }
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

        private bool IsNewBatchNeeded() => BatchCapacity > 0 && operationCounter == 0 || operationCounter % BatchCapacity == 0;

        private void SendBatch() => batchPointers.Add(MakeAndSendBatch());

        private BatchPointer<JObject> MakeAndSendBatch() 
        {
            var batch = new Batch<JObject>(operations);
            operations.Clear();
            var task = SendBatchAsync(batch);
            return new BatchPointer<JObject>(batch, task);
        }

        private async Task<BatchResult> SendBatchAsync(Batch<JObject> batch)
        {
            var batchStopWatch = Stopwatch.StartNew();

            WriteInformation($"Batch-{batch.Id}[total:{batch.ChangeSet.Operations.Count()}, starting: {batch.ChangeSet.Operations.First().ContentId}] being sent...", new string[] { "dataverse" });
            BatchResponse response = null;
            try 
            {
                await taskThrottler.WaitAsync();
                response = await batchProcessor.ExecuteBatchAsync(batch);
            }
            finally
            {
                taskThrottler.Release();
            }

            WriteInformation($"Batch-{batch.Id} completed.", new string[] { "dataverse" });
            
            Interlocked.Increment(ref batchCounter);
            batchStopWatch.Stop();
            return new BatchResult(batchStopWatch.Elapsed, response); 
        }

        private void MakeAndSendBatchThenOutput(bool waitForAll)
        {
            if (operations?.Count > 0)
            {
                var batch = new Batch<JObject>(operations);
                operations.Clear();
                var task = SendBatchThenOutputAsync(batch);
                tasks.Add(task);
            }
            if (waitForAll)
            {
                var all = Task.WhenAll(tasks);
                try
                {
                    var responses = all.Result;
                    foreach (var response in responses)
                    {
                        WriteOutput(response);
                    }
                }
                catch (AggregateException ex)
                {
                    foreach (var exception in ex.InnerExceptions)
                    {
                        WriteError(new ErrorRecord(exception, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
                    }
                }
                tasks.Clear();
            }
            else
            {
                while (tasks.Count >= MaxDop) 
                {
                    Thread.Sleep(200); 
                    var completedTasks = tasks.Where(t => t.IsCompleted).ToArray();
                    foreach (var completedTask in completedTasks)
                    {
                        try
                        {
                            WriteOutput(completedTask.Result);
                        }
                        catch (AggregateException ex)
                        {
                            foreach (var exception in ex.InnerExceptions)
                            {
                                WriteError(new ErrorRecord(exception, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
                            }
                        }
                    }
                    tasks.RemoveAll(t => completedTasks.Contains(t));
                }
            }
        }

        private void WriteOutput(BatchResponse batchResponse)
        {
            if (!OutputTable)
            {
                WriteObject(batchResponse);
            }
            else
            {
                var tbl = new System.Data.DataTable();
                tbl.Columns.Add("Id", typeof(string));
                tbl.Columns.Add("BatchId", typeof(Guid));
                tbl.Columns.Add("Response", typeof(string));
                tbl.Columns.Add("Succeeded", typeof(bool));
                foreach (var response in batchResponse.Operations)
                {
                    tbl.Rows.Add(
                        response.ContentId,
                        batchResponse.Id,
                        response.Headers != null && response.Headers.TryGetValue("OData-EntityId", out var id) ? id : null,
                        true);
                }
                WriteObject(tbl);
            }
        }
        private void WriteOutput(Batch<JObject> batch)
        {
            if (!OutputTable)
            {
                WriteObject(batch);
            }
            else
            {
                var tbl = new System.Data.DataTable();
                tbl.Columns.Add("Id", typeof(string));
                tbl.Columns.Add("BatchId", typeof(Guid));
                tbl.Columns.Add("Response", typeof(string));
                tbl.Columns.Add("Succeeded", typeof(bool));
                if (batch.Response.IsSuccessful)
                {
                    foreach (var response in batch.Response.Operations)
                    {
                        var msg = response.Headers != null && response.Headers.TryGetValue("OData-EntityId", out var id) ? id : null;
                        tbl.Rows.Add(response.ContentId, batch.Id, msg, true);
                    }
                }
                else
                {
                    var failedOperationIds = new string[batch.Response.Operations.Count];
                    var i = 0;
                    foreach (var response in batch.Response.Operations) // There will only be one op, when batch fails.
                    {
                        tbl.Rows.Add(response.ContentId, batch.Id, response.Error.ToString(), false);
                        failedOperationIds[i] = response.ContentId;
                        ++i;
                    }
                    foreach (var op in batch.ChangeSet.Operations.Where(op => !failedOperationIds.Contains(op.ContentId)))
                    {
                        tbl.Rows.Add(op.ContentId, batch.Id, null, false);
                    }
                }
                WriteObject(tbl);
            }
        }

        private async Task<Batch<JObject>> SendBatchThenOutputAsync(Batch<JObject> batch)
        {
            WriteInformation($"Batch-{batch.Id}[total:{batch.ChangeSet.Operations.Count()}, starting: {batch.ChangeSet.Operations.First().ContentId}] being sent...", new string[] { "dataverse" });
            BatchResponse response = null;
            try
            {
                await taskThrottler.WaitAsync();
                response = await batchProcessor.ExecuteBatchAsync(batch);
                WriteInformation($"Batch-{batch.Id} completed.", new string[] { "dataverse" });
            }
            catch (Exception ex)
            { 
                throw new BatchException<JObject>("Batch has been faild due to an error", ex) { Batch = batch };
                //WriteError(new ErrorRecord(exception, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
            }
            finally
            {
                taskThrottler.Release();
                Interlocked.Increment(ref batchCounter);
            }
            batch.Response = response;
            return batch;
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