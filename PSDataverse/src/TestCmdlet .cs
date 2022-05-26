using MigrationModule.Dataverse;
using MigrationModule.Dataverse.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace DataverseModule
{
    [Cmdlet(VerbsDiagnostic.Test, "Dataverse")]
    public class TestCmdlet : PSCmdlet
    {
        [Parameter(Position=0, Mandatory = true, ParameterSetName = "Object")]
        public Operation<JObject> Operation { get; set; }

        [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Json", ValueFromPipeline = true)]
        public string OperationJson { get; set; }

        [Parameter(Position = 1, Mandatory = false)]
        public int BatchCapacity { get; set; } = 0;

        [Parameter(Position=2, Mandatory = false)]
        public int Retry { get; set; }

        private int operationCounter = 0;
        private int batchCounter = 0;
        private System.Collections.Concurrent.ConcurrentBag<Operation<JObject>> operations;// List<Operation<JObject>> operations;
        private List<Task<(TimeSpan, Batch<JObject>)>> batches;
        private Stopwatch _timer;

        protected override void BeginProcessing()
        //protected override Task BeginProcessingAsync()
        {
            _timer = Stopwatch.StartNew();
            if (BatchCapacity > 0) 
            { 
                operations = new(new List<Operation<JObject>>(BatchCapacity));
                batches = new();
            }
            operationCounter = 0;
            //return Task.CompletedTask;
        }

        protected override void ProcessRecord()
        //protected override Task ProcessRecordAsync()
        {
            Interlocked.Increment(ref operationCounter);

            var operation = new Operation<JObject>() { ContentId = operationCounter.ToString(), Value = new JObject() };

            if (BatchCapacity <= 0)
            {
                SendOperation(operation);
                return;
            }

            operations.Add(operation);

            if (IsNewBatchNeeded())
            {
                SendBatch();
            }
            //return Task.CompletedTask;
        }

        private void SendOperation(Operation<JObject> operation)
        {
            WriteObject($"Operation send {operation}");
        }

        protected override void EndProcessing()
        //protected override Task EndProcessingAsync()
        {
            if (!operations.IsEmpty)
            {
                SendBatch();
            }
            try
            {
                Task.WaitAll(batches.ToArray());
            }
            catch (AggregateException ex)
            {
                foreach (var exception in ex.InnerExceptions)
                {
                    WriteError(new ErrorRecord(exception, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
                }
            }
            _timer.Stop();
            WriteObject($"Total operations: {operationCounter}, total batches: {batchCounter}, total time: {_timer.Elapsed}");
            foreach (var task in batches.Where(b => b.IsCompletedSuccessfully))
            {
                WriteObject(task.Result.ToString());
            }
        }

        private void SendBatch()
        {
            //var batch = new Batch<JObject>(operations);
            //WriteObject($"Batch sent {batch}");
            //operations.Clear();
            //++batchCounter;
            batches.Add(SendBatchAsync());
            //batches.RemoveAll(t => t.IsCompletedSuccessfully);
            //var failedBatches = batches.Where(t => t.IsFaulted).ToList();
            //foreach (var batch in failedBatches)
            //{
            //    try
            //    {
            //        batch.Wait();
            //    }
            //    catch (Exception ex)
            //    {
            //        WriteError(new ErrorRecord(ex, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
            //    }
            //}
            //if (batches.Count > 0)
            //{
            //    WriteObject($"There are still {batches.Count} batches!");
            //}
        }

        private async Task<(TimeSpan,Batch<JObject>)> SendBatchAsync()
        {
            var r = new Random();
            var timer = Stopwatch.StartNew();
            var batch = new Batch<JObject>(operations);
            operations.Clear();
            WriteObject($"Batch being sent ({batch.ChangeSet.Operations.Count()}) {batch.Id}...");
            var waitFactor = r.Next(2, 10);
            await Task.Delay(waitFactor * 1000);
            Interlocked.Increment(ref batchCounter);
            if (waitFactor > 8) { throw new BatchException<JObject> { Batch = batch }; }
            return (timer.Elapsed, batch);
        }

        private bool IsNewBatchNeeded() => BatchCapacity > 0 && operationCounter == 0 || operationCounter % BatchCapacity == 0;
    }
}