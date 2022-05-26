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

        [Parameter(Position=2, Mandatory = false)]
        public int Retry { get; set; }

        [Parameter(Position = 3, Mandatory = false)]
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
        private ConcurrentBag<Task<(TimeSpan, BatchResponse)>> batches;
        private Stopwatch stopwatch;
        private SemaphoreSlim throttler;

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
                batches = new();
                throttler = new SemaphoreSlim(20);
            }
            operationCounter = 0;
        }

        protected override void ProcessRecord()
        {
            //TODO: Implement exception handling and logging.
            //TODO: Implement retry logic
            //TODO: Implement IoC
            base.ProcessRecord();

            //var operationProcessor = (OperationProcessor)GetVariableValue(Globals.VariableNameOperationProcessor);

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
            if (!"GET".Equals(op.Method, StringComparison.OrdinalIgnoreCase) && op.Value == null)
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
            if (batches?.Any() ?? false)
            {
                try
                {
                    Task.WaitAll(batches.ToArray());
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
            if (batches == null) { 
                stopwatch.Stop();
                WriteInformation($"Send-Dataverse completed - Elapsed: {stopwatch.Elapsed}, Batches: {batchCounter}, Operations: {operationCounter}.", new string[] { "Dataverse" });
                return; 
            }
            foreach (var item in batches)
            {
                // item.Key   : Task<TimeSpan, BatchResult>
                // item.Value : Batch

                if (item.IsFaulted)
                {
                    WriteError(new ErrorRecord(item.Exception.InnerException, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
                }
                else if (item.IsCanceled)
                {
                    var exception = new BatchException<JObject>("Batch has been canceled");
                    WriteError(new ErrorRecord(exception, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
                }
                else
                {
                    if (!OutputTable)
                    {
                        WriteObject(item.Result.Item2);
                    }
                    else
                    {
                        var tbl = new System.Data.DataTable();
                        tbl.Columns.Add("Id", typeof(string));
                        tbl.Columns.Add("Response", typeof(string));
                        tbl.Columns.Add("Succeeded", typeof(bool));
                        foreach (var response in item.Result.Item2.Operations)
                        {
                            tbl.Rows.Add(response.ContentId, response.Headers?["OData-EntityId"], true);
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
            batches.Add(SendBatchAsync());
        }

        // private async Task<(TimeSpan, Batch<JObject>)> SendBatchAsync()
        private async Task<(TimeSpan, BatchResponse)> SendBatchAsync()
        {
            var batchStopWatch = Stopwatch.StartNew();
            var batch = new Batch<JObject>(operations);
            operations.Clear();

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
            return (batchStopWatch.Elapsed, response);
        }
    }
}