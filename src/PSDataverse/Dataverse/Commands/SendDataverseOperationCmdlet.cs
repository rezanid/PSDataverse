namespace PSDataverse;

using PSDataverse.Auth;
using PSDataverse.Dataverse;
using PSDataverse.Dataverse.Execute;
using PSDataverse.Dataverse.Model;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using PSDataverse.Extensions;
using Microsoft.PowerShell.Commands;

[Cmdlet(VerbsCommunications.Send, "DataverseOperation", DefaultParameterSetName = "Object")]
public class SendDataverseOperationCmdlet : DataverseCmdlet
{
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Operation", ValueFromPipeline = true)]
    public Operation<string> InputOperation { get; set; }

    //[Parameter(Position = 0, Mandatory = true, ParameterSetName = "Json", ValueFromPipeline = true)]
    //public string InputJson { get; set; }

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
    private AuthenticationParameters dataverseCnnStr;
    private AuthenticationService authenticationService;
    private DateTimeOffset? authExpiresOn;
    private OperationProcessor operationProcessor;
    private BatchProcessor batchProcessor;
    private int operationCounter;
    private int batchCounter;
    private ConcurrentBag<Operation<string>> operations;
    private List<Task<Batch<string>>> tasks;
    private Stopwatch stopwatch;
    private SemaphoreSlim taskThrottler;

    private static readonly string[] ValidMethodsWithoutPayload = { "GET", "DELETE" };

    protected override void BeginProcessing()
    {
        base.BeginProcessing();

        stopwatch = Stopwatch.StartNew();

        var serviceProvider = (IServiceProvider)GetVariableValue(Globals.VariableNameServiceProvider);
        operationProcessor = serviceProvider.GetService<OperationProcessor>();
        batchProcessor = serviceProvider.GetService<BatchProcessor>();
        authenticationService = serviceProvider.GetService<AuthenticationService>();

        if (!VerifyConnection())
        {
            isValidationFailed = true;
            return;
        }

        if (BatchSize > 0)
        {
            operations = new(new List<Operation<string>>(BatchSize));
            tasks = new();
            taskThrottler = new SemaphoreSlim(MaxDop <= 0 ? 20 : MaxDop);
        }
        operationCounter = 0;
    }

    protected override void ProcessRecord()
    {
        base.ProcessRecord();

        if (isValidationFailed || !VerifyConnection())
        { return; }

        if (!TryGetInputOperation(out var op))
        {
            var errMessage = "No operation has been given. Please provide an operation using either of -InputOperation or -InputObject arguments.";
            WriteError(new ErrorRecord(new InvalidOperationException(errMessage), Globals.ErrorIdMissingOperation, ErrorCategory.ConnectionError, null));
            return;
        }

        if (!op.HasValue && op.Method is null)
        { op.Method = "GET"; }
        else if (op.Method is null)
        { op.Method = "POST"; }
        else
        { /* no other possibility */ }

        ValidateOperation(op);

        Interlocked.Increment(ref operationCounter);

        if (BatchSize <= 0)
        {
            operationProcessor.AuthenticationToken = accessToken;
            try
            {
                var response = operationProcessor.ExecuteAsync(op).Result;
                var opResponse = OperationResponse.From(response);
                if (string.IsNullOrEmpty(opResponse.ContentId))
                { opResponse.ContentId = op.ContentId; }
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
            MakeAndSendBatchThenOutput(waitForAll: false);
        }
    }

    protected override void EndProcessing()
    {
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

        base.EndProcessing();
    }

    private bool TryGetInputOperation(out Operation<string> operation)
    {
        if (InputOperation is not null)
        {
            operation = InputOperation;
            return true;
        }
        //TODO: Remove InputJson parameter
        //if (InputJson is not null)
        //{
        //    string input = null;
        //    // If the given string is not in JSON format, assume it's a URL.
        //    if (!InputJson.StartsWith('{')) { input = $"{{\"Uri\":\"{InputJson}\"}}"; }
        //    operation = JsonConvert.DeserializeObject<Operation<string>>(input ?? InputJson);
        //    return true;
        //}
        if (InputObject is not null)
        {
            if (InputObject.BaseObject is string str)
            {
                string input = null;
                // If the given string is not in JSON format, assume it's a URL.
                if (!str.StartsWith('{'))
                {
                    input = $"{{\"Uri\":\"{str}\"}}";
                }
                operation = JsonConvert.DeserializeObject<Operation<string>>(input ?? str);
                return true;
            }
            if (InputObject.BaseObject is IDictionary dictionary)
            {
                operation = new Operation<string>
                {
                    ContentId = InputObject.TryGetPropertyValue("ContentId"),
                    Method = InputObject.TryGetPropertyValue("Method"),
                    Uri = InputObject.TryGetPropertyValue("Uri"),
                    Headers = dictionary.TryGetValue("Headers", out var headers) && headers != null ?
                        (headers as IDictionary).Cast<DictionaryEntry>().ToDictionary(e => e.Key.ToString(), e => e.Value.ToString())
                        : null,
                    Value = dictionary.TryGetValue("Value", out var value) && value != null ? ConvertToJson(value) : null
                };
                return true;
            }
        }
        operation = null;
        return false;
    }

    private string ConvertToJson(object objectToProcess)
    {
        var context = new JsonObject.ConvertToJsonContext(
                    maxDepth: 10,
                    enumsAsStrings: false,
                    compressOutput: true,
                    stringEscapeHandling: StringEscapeHandling.Default,
                    targetCmdlet: this,
                    cancellationToken: CancellationToken);
        return JsonObject.ConvertToJson(objectToProcess, in context);
    }

    private static void ValidateOperation(Operation<string> operation)
    {
        if (operation is null)
        {
            throw new InvalidOperationException("Operation parameter is not provided.");
        }
        if (!ValidMethodsWithoutPayload.Contains(operation.Method, StringComparer.OrdinalIgnoreCase) && !operation.HasValue)
        {
            throw new InvalidOperationException(
                $"Operation does not have a 'Value' but it has a {operation.Method} method. All operations with non-GET method should have a value.");
        }
    }

    private bool VerifyConnection()
    {
        accessToken = (string)GetVariableValue(Globals.VariableNameAccessToken);
        authExpiresOn = (DateTimeOffset?)GetVariableValue(Globals.VariableNameAccessTokenExpiresOn);
        dataverseCnnStr = (AuthenticationParameters)GetVariableValue(Globals.VariableNameConnectionString);
        if (string.IsNullOrEmpty(accessToken))
        {
            var errMessage = "No active connection detect. Please first authenticate using Connect-Dataverse cmdlet.";
            WriteError(new ErrorRecord(new InvalidOperationException(errMessage), Globals.ErrorIdNotConnected, ErrorCategory.ConnectionError, null));
            return false;
        }
        if (authExpiresOn <= DateTimeOffset.Now && dataverseCnnStr == null)
        {
            var errMessage = "Active connection has expired. Please authenticate again using Connect-Dataverse cmdlet.";
            WriteError(new ErrorRecord(new InvalidOperationException(errMessage), Globals.ErrorIdConnectionExpired, ErrorCategory.ConnectionError, null));
            return false;
        }
        if (dataverseCnnStr != null && authExpiresOn <= DateTimeOffset.Now)
        {
            var authResult = authenticationService.AuthenticateAsync(dataverseCnnStr, OnMessageForUser, CancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
            SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessToken, authResult.AccessToken, ScopedItemOptions.AllScope));
            SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessTokenExpiresOn, authResult.ExpiresOn, ScopedItemOptions.AllScope));
        }
        return true;
    }

    private void OnMessageForUser(string message) => WriteInformation(message, new string[] { "dataverse" });

    private bool IsNewBatchNeeded() => (BatchSize > 0 && operationCounter == 0) || operationCounter % BatchSize == 0;

    private void MakeAndSendBatchThenOutput(bool waitForAll)
    {
        if (operations?.Count > 0)
        {
            var batch = new Batch<string>(operations);
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

    private void WriteOutput(Batch<string> batch)
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

    private async Task<Batch<string>> SendBatchAsync(Batch<string> batch)
    {
        WriteInformation($"Batch-{batch.Id}[total:{batch.ChangeSet.Operations.Count()}, starting: {batch.ChangeSet.Operations.First().ContentId}] being sent...", new string[] { "dataverse" });
        BatchResponse response = null;
        try
        {
            await taskThrottler.WaitAsync();
            response = await batchProcessor.ExecuteBatchAsync(batch, CancellationToken);
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
            throw new BatchException<string>($"Batch has been faild due to: {ex.Message}", ex) { Batch = batch };
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

    protected override void Dispose(bool disposing)
    {
        if (Disposed)
        { return; }
        if (disposing)
        {
            taskThrottler?.Dispose();
        }
        base.Dispose(disposing);
    }
}
