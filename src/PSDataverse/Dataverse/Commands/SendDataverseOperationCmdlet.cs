namespace PSDataverse;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerShell.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PSDataverse.Auth;
using PSDataverse.Dataverse;
using PSDataverse.Dataverse.Execute;
using PSDataverse.Dataverse.Model;
using PSDataverse.Extensions;

[Cmdlet(VerbsCommunications.Send, "DataverseOperation", DefaultParameterSetName = "Object")]
public class SendDataverseOperationCmdlet : DataverseCmdlet, IOperationReporter
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

    [Parameter(Position = 5, Mandatory = false)]
    public SwitchParameter AutoPaginate { get; set; }

    private bool isValidationFailed;
    private string accessToken;
    private AuthenticationParameters dataverseCnnStr;
    private AuthenticationService authenticationService;
    private DateTimeOffset? authExpiresOn;
    private OperationProcessor operationProcessor;
    private OperationHandler operationHandler;
    private BatchProcessor batchProcessor;
    private int operationCounter;
    private int batchCounter;
    private ConcurrentBag<Operation<string>> operations;
    private List<Task<Batch<string>>> tasks;
    private Stopwatch stopwatch;
    private SemaphoreSlim taskThrottler;

    private static readonly string[] ValidMethodsWithoutPayload = ["GET", "DELETE"];

    protected override void BeginProcessing()
    {
        base.BeginProcessing();

        stopwatch = Stopwatch.StartNew();

        var serviceProvider = (IServiceProvider)GetVariableValue(Globals.VariableNameServiceProvider);
        operationProcessor = serviceProvider.GetService<OperationProcessor>();
        batchProcessor = serviceProvider.GetService<BatchProcessor>();
        authenticationService = serviceProvider.GetService<AuthenticationService>();
        operationHandler = new(operationProcessor, this);
        if (!VerifyConnection())
        {
            isValidationFailed = true;
            return;
        }

        if (BatchSize > 0)
        {
            operations = [.. new List<Operation<string>>(BatchSize)];
            tasks = [];
            taskThrottler = new SemaphoreSlim(MaxDop <= 0 ? 20 : MaxDop);
        }
        operationCounter = 0;
    }

    protected override void ProcessRecord()
    {
        base.ProcessRecord();

        if (isValidationFailed || !VerifyConnection())
        {
            return;
        }

        if (!TryGetInputOperation(out var op))
        {
            operationHandler.ReportMissingOperationError();
            return;
        }

        OperationHandler.AssignDefaultHttpMethod(op); // This remains static, so called directly on the class

        ValidateOperation(op);

        _ = Interlocked.Increment(ref operationCounter);

        if (BatchSize <= 0)
        {
            operationHandler.ExecuteSingleOperation(op, accessToken, AutoPaginate.IsPresent);
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
            WriteInformation($"Send-Dataverse completed - Elapsed: {stopwatch.Elapsed}, Batches: {batchCounter}, Operations: {operationCounter}.", ["Dataverse"]);
            return;
        }
        stopwatch.Stop();
        _ = taskThrottler.Release();
        taskThrottler.Dispose();
        WriteInformation($"Send-Dataverse completed - Elapsed: {stopwatch.Elapsed}, Batches: {batchCounter}, Operations: {operationCounter}.", ["Dataverse"]);

        base.EndProcessing();
    }

    private bool TryGetInputOperation(out Operation<string> operation)
    {
        if (InputOperation is not null)
        {
            operation = InputOperation;
            return true;
        }
        if (InputObject is not null)
        {
            if (InputObject.BaseObject is string str)
            {
                // If the given string is not in JSON format, assume it's a URL.
                if (!str.StartsWith('{'))
                {
                    str = $"{{\"Uri\":\"{str}\"}}";
                }
                var jobject = JObject.Parse(str);
                operation = new Operation<string>
                {
                    ContentId = jobject.TryGetValue("ContentId", out var contentId) ? contentId.ToString() : null,
                    Method = jobject.TryGetValue("Method", out var method) ? method.ToString() : null,
                    Uri = jobject.TryGetValue("Uri", out var uri) ? uri.ToString() : null,
                    Headers = jobject.TryGetValue("Headers", out var headers) ? headers.ToObject<Dictionary<string, string>>() : null,
                    Value = jobject.TryGetValue("Value", out var value) ? value.ToString(Formatting.None, []) : null
                };
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
        // if (authExpiresOn <= DateTimeOffset.Now && dataverseCnnStr == null)
        // {
        //     var errMessage = "Active connection has expired. Please authenticate again using Connect-Dataverse cmdlet.";
        //     WriteError(new ErrorRecord(new InvalidOperationException(errMessage), Globals.ErrorIdConnectionExpired, ErrorCategory.ConnectionError, null));
        //     return false;
        // }
        // if (dataverseCnnStr != null && authExpiresOn <= DateTimeOffset.Now)
        // {
        var authResult = authenticationService.AuthenticateAsync(dataverseCnnStr, OnMessageForUser, CancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessToken, authResult.AccessToken, ScopedItemOptions.AllScope));
        SessionState.PSVariable.Set(new PSVariable(Globals.VariableNameAccessTokenExpiresOn, authResult.ExpiresOn, ScopedItemOptions.AllScope));
        // }
        return true;
    }

    private void OnMessageForUser(string message) => WriteInformation(message, ["dataverse"]);

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
            _ = tbl.Columns.Add("Id", typeof(string));
            _ = tbl.Columns.Add("BatchId", typeof(Guid));
            _ = tbl.Columns.Add("Response", typeof(string));
            _ = tbl.Columns.Add("Succeeded", typeof(bool));
            if (batch.Response.IsSuccessful)
            {
                foreach (var response in batch.Response.Operations)
                {
                    var msg = response.Headers != null && response.Headers.TryGetValue("OData-EntityId", out var id) ? id : null;
                    _ = tbl.Rows.Add(response.ContentId, batch.Id, msg, true);
                }
            }
            else
            {
                var failedOperationIds = new string[batch.Response.Operations.Count];
                var i = 0;
                foreach (var response in batch.Response.Operations) // There will only be one op, when batch fails.
                {
                    _ = tbl.Rows.Add(response.ContentId, batch.Id, response.Error.ToString(), false);
                    failedOperationIds[i] = response.ContentId;
                    ++i;
                }
                foreach (var op in batch.ChangeSet.Operations.Where(op => !failedOperationIds.Contains(op.ContentId)))
                {
                    _ = tbl.Rows.Add(op.ContentId, batch.Id, null, false);
                }
            }
            WriteObject(tbl);
        }
    }

    private async Task<Batch<string>> SendBatchAsync(Batch<string> batch)
    {
        WriteInformation($"Batch-{batch.Id}[total:{batch.ChangeSet.Operations.Count()}, starting: {batch.ChangeSet.Operations.First().ContentId}] being sent...", ["dataverse"]);
        BatchResponse response = null;
        try
        {
            await taskThrottler.WaitAsync();
            response = await batchProcessor.ExecuteBatchAsync(batch, CancellationToken);
            if (response is not null)
            {
                WriteInformation($"Batch-{batch.Id} completed.", ["dataverse"]);
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
            _ = taskThrottler.Release();
            _ = Interlocked.Increment(ref batchCounter);
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

    public void WriteInformation(string messageData, string[] tags) => base.WriteInformation(messageData, tags);
}
