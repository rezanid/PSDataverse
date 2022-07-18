using DataverseModule.Dataverse;
using DataverseModule.Dataverse.Execute;
using DataverseModule.Dataverse.Model;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace DataverseModule
{
    [Cmdlet(VerbsCommunications.Send, "DataverseOperation", DefaultParameterSetName = "Object")]
    public class SendDataverseOperationCmdlet : PSCmdlet, IDisposable
    {
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Operation", ValueFromPipeline = true)]
        public Operation<JObject> InputOperation { get; set; }

        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Json", ValueFromPipeline = true)]
        public string InputJson { get; set; }

        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Object", ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public PSObject InputObject { get; set; }

        [Parameter(Position = 1, Mandatory = false)]
        [Alias("BatchCapacity")]
        [ValidateRange(0, 1000)]
        public int BatchSize { get; set; } = 0;

        [Parameter(Position = 2, Mandatory = false)]
        [Alias("ThrottleLimit")]
        [ValidateRange(0, int.MaxValue)]
        public int MaxDop { get; set; } = 0;

        [Parameter(Position = 3, Mandatory = false)]
        [ValidateRange(0, 50)]
        public int Retry { get; set; }

        [Parameter(Position = 4, Mandatory = false)]
        public SwitchParameter OutputTable { get; set; }

        private bool isValidationFailed;
        private string accessToken;
        private DataverseConnectionString dataverseCnnStr;
        private DateTimeOffset? authExpiresOn;
        private OperationProcessor operationProcessor;
        private BatchProcessor batchProcessor;
        private int operationCounter = 0;
        private int batchCounter = 0;
        private ConcurrentBag<Operation<JObject>> operations;
        private List<Task<Batch<JObject>>> tasks;
        private Stopwatch stopwatch;
        private SemaphoreSlim taskThrottler;
        private readonly CancellationTokenSource _cancellationSource = new();
        private static readonly string[] validMethodsWithoutPayload = { "GET", "DELETE" };

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
            if (BatchSize > 0)
            {
                operations = new(new List<Operation<JObject>>(BatchSize));
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

            if (!TryGetInputOperation(out var op))
            {
                var errMessage = "No operation has been given. Please provide an operation using either of -InputOperation or -InputJson or -InputObject arguments.";
                WriteError(new ErrorRecord(new Exception(errMessage), Globals.ErrorIdMissingOperation, ErrorCategory.ConnectionError, null));
                return;
            }

            if (op.Value is null && op.Method is null) { op.Method = "GET"; }
            else if (op.Method is null) { op.Method = "POST"; }
            else { /* no other possibility */ }

            ValidateOperation(op);

            Interlocked.Increment(ref operationCounter);

            if (BatchSize <= 0)
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
            if (BatchSize == 0)
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

        /// <summary>
        /// Process the Ctrl+C signal.
        /// </summary>
        protected override void StopProcessing() => _cancellationSource.Cancel();

        private bool TryGetInputOperation(out Operation<JObject> operation)
        {
            if (InputOperation is not null)
            {
                operation = InputOperation;
                return true;
            }
            if (InputJson is not null)
            {
                operation = JsonConvert.DeserializeObject<Operation<JObject>>(InputJson);
                return true;
            }
            if (InputObject is not null)
            {
                if (InputObject.BaseObject is IDictionary dictionary)
                {
                    operation = new Operation<JObject>
                    {
                        ContentId = InputObject.TryGetPropertyValue("ContentId"),
                        Method = InputObject.TryGetPropertyValue("Method"),
                        Uri = InputObject.TryGetPropertyValue("Uri"),
                        Headers = dictionary.Contains("Headers") ?
                            (dictionary["Headers"] as IDictionary).Cast<DictionaryEntry>().ToDictionary(e => e.Key.ToString(), e => e.Value.ToString())
                            : null,
                        Value = dictionary.Contains("Value") ? JObject.FromObject(dictionary["Value"]) : null
                    };
                    return true;
                }
            }
            operation = null;
            return false;
        }



        private static void ValidateOperation(Operation<JObject> operation)
        {
            if (operation is null)
            {
                throw new InvalidOperationException("Operation parameter is not provided.");
            }
            if (!validMethodsWithoutPayload.Contains(operation.Method, StringComparer.OrdinalIgnoreCase) && operation.Value == null)
            {
                throw new InvalidOperationException(
                    $"Operation does not have a 'Value' but it has a {operation.Method} method. All operations with non-GET method should have a value.");
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
            if (dataverseCnnStr != null && authExpiresOn <= DateTimeOffset.Now)
            {
                var authResult = AuthenticationHelper.Authenticate(dataverseCnnStr);
                SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessToken, authResult.AccessToken, ScopedItemOptions.AllScope));
                SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessTokenExpiresOn, authResult.ExpiresOn, ScopedItemOptions.AllScope));
            }
            return true;
        }

        private bool IsNewBatchNeeded() => BatchSize > 0 && operationCounter == 0 || operationCounter % BatchSize == 0;

        private void MakeAndSendBatchThenOutput(bool waitForAll)
        {
            if (operations?.Count > 0)
            {
                var batch = new Batch<JObject>(operations);
                operations.Clear();
                var task = SendBatchAsync(batch);
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
                while (tasks.Count != 0 && tasks.Count >= MaxDop)
                {
                    Thread.Sleep(100);
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

        private async Task<Batch<JObject>> SendBatchAsync(Batch<JObject> batch)
        {
            WriteInformation($"Batch-{batch.Id}[total:{batch.ChangeSet.Operations.Count()}, starting: {batch.ChangeSet.Operations.First().ContentId}] being sent...", new string[] { "dataverse" });
            BatchResponse response = null;
            try
            {
                await taskThrottler.WaitAsync();
                response = await batchProcessor.ExecuteBatchAsync(batch, _cancellationSource.Token);
                if (response is not null)
                {
                    WriteInformation($"Batch-{batch.Id} completed.", new string[] { "dataverse" });
                }
                else
                {
                    WriteWarning($"Batch-{batch.Id} has been cancelled.");
                }
            }
            catch (Exception ex)
            {
                throw new BatchException<JObject>($"Batch has been faild due to: {ex.Message}", ex) { Batch = batch };
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

        #region Dispose Pattern
        /// <summary>
        /// IDisposable implementation, dispose of any disposable resources created by the cmdlet.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Implementation of IDisposable for both manual Dispose() and finalizer-called disposal of resources.
        /// </summary>
        /// <param name="disposing">
        /// Specified as true when Dispose() was called, false if this is called from the finalizer.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationSource.Dispose();
                taskThrottler?.Dispose();
            }
        }
        #endregion
    }
}
