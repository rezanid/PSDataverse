namespace PSDataverse;

using System;
using System.Collections;
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
using PSDataverse.Dataverse;
using PSDataverse.Dataverse.Execute;
using PSDataverse.Dataverse.Model;
using PSDataverse.Extensions;

[Cmdlet(VerbsLifecycle.Invoke, "DataverseRequest", DefaultParameterSetName = "Object")]
public class InvokeDataverseRequestCmdlet : DataverseCmdlet, IOperationReporter
{
    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Operation", ValueFromPipeline = true)]
    public Operation<string> InputOperation { get; set; }

    //[Parameter(Position = 0, Mandatory = true, ParameterSetName = "Json", ValueFromPipeline = true)]
    //public string InputJson { get; set; }

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Object", ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public PSObject InputObject { get; set; }

    [Parameter(Position = 0, Mandatory = true, ParameterSetName = "Request", ValueFromPipeline = true)]
    [ValidateNotNullOrEmpty]
    public string Uri { get; set; }

    [Parameter(ParameterSetName = "Request")]
    [ValidateSet("GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS")]
    public string Method { get; set; }

    [Parameter(ParameterSetName = "Request")]
    public object Body { get; set; }

    [Parameter(ParameterSetName = "Request")]
    public IDictionary Headers { get; set; }

    [Parameter(ParameterSetName = "Request")]
    public string ContentId { get; set; }

    [Parameter(Position = 1, Mandatory = false)]
    [Alias("BatchCapacity")]
    [ValidateRange(0, 1000)]
    public int BatchSize { get; set; } = 0;

    [Parameter(Position = 2, Mandatory = false)]
    [Alias("ThrottleLimit")]
    [ValidateRange(0, int.MaxValue)]
    public int MaxDop { get; set; } = 0;

    [Parameter(Position = 3, Mandatory = false)]
    public SwitchParameter OutputTable { get; set; }

    [Parameter(Position = 4, Mandatory = false)]
    public SwitchParameter AutoPaginate { get; set; }

    [Parameter]
    [ValidateSet("Completion", "Input")]
    public string OutputOrder { get; set; } = "Completion";

    [Parameter]
    public DataverseConnection Connection { get; set; }

    [Parameter]
    public string ConnectionName { get; set; }

    private bool IsOnPremise;
    private bool isValidationFailed;
    private string accessToken;
    private DataverseConnection activeConnection;
    private OperationProcessor operationProcessor;
    private OperationHandler operationHandler;
    private BatchProcessor batchProcessor;
    private int operationCounter;
    private int batchCounter;
    private List<Operation<string>> operations;
    private Stopwatch stopwatch;
    private BoundedChannelScheduler<Batch<string>, Batch<string>> batchScheduler;
    private readonly SortedDictionary<long, ScheduledResult<Batch<string>>> orderedResults = [];
    private long nextOutputSequence;
    private int effectiveMaxDop;

    private const int DefaultMaxDop = 20;
    private static readonly string[] ValidMethodsWithoutPayload = ["GET", "DELETE", "HEAD", "OPTIONS"];

    protected override void BeginProcessing()
    {
        base.BeginProcessing();

        stopwatch = Stopwatch.StartNew();

        if (Connection is not null && !string.IsNullOrWhiteSpace(ConnectionName))
        {
            WriteError(new ErrorRecord(
                new PSArgumentException("Specify either -Connection or -ConnectionName, not both."),
                Globals.ErrorIdConnectionNotFound,
                ErrorCategory.InvalidArgument,
                ConnectionName));
            isValidationFailed = true;
            return;
        }
        activeConnection = Connection ?? GetConnectionRegistry(create: false)?.Get(ConnectionName);
        if (activeConnection is null)
        {
            var message = string.IsNullOrWhiteSpace(ConnectionName)
                ? "No active connection detected. Run Connect-Dataverse first."
                : $"No Dataverse connection named '{ConnectionName}' exists.";
            WriteError(new ErrorRecord(
                new InvalidOperationException(message),
                string.IsNullOrWhiteSpace(ConnectionName) ? Globals.ErrorIdNotConnected : Globals.ErrorIdConnectionNotFound,
                ErrorCategory.ConnectionError,
                ConnectionName));
            isValidationFailed = true;
            return;
        }
        var serviceProvider = activeConnection.Services;
        operationProcessor = serviceProvider.GetService<OperationProcessor>();
        batchProcessor = serviceProvider.GetService<BatchProcessor>();
        operationHandler = new(operationProcessor, this);
        if (!VerifyConnection())
        {
            isValidationFailed = true;
            return;
        }

        if (BatchSize > 0)
        {
            operations = new List<Operation<string>>(BatchSize);
            effectiveMaxDop = ResolveMaxDop(MaxDop);
            batchScheduler = new BoundedChannelScheduler<Batch<string>, Batch<string>>(
                Math.Max(1, effectiveMaxDop * 2),
                effectiveMaxDop,
                SendBatchAsync,
                CancellationToken,
                batch => batch.Response?.RecommendedDegreeOfParallelism);
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
            operationHandler.ExecuteSingleOperation(op, accessToken, AutoPaginate.IsPresent, CancellationToken);
            return;
        }

        operations.Add(op);

        if (operations.Count >= BatchSize)
        {
            batchProcessor.AuthenticationToken = accessToken;
            QueueBatch();
            DrainBatchResults(waitForCompletion: false);
        }
    }

    protected override void EndProcessing()
    {
        if (BatchSize > 0)
        {
            if (operations.Count > 0)
            {
                batchProcessor.AuthenticationToken = accessToken;
                QueueBatch();
            }
            batchScheduler.Complete();
            DrainBatchResults(waitForCompletion: true);
        }
        if (BatchSize == 0)
        {
            stopwatch.Stop();
            WriteInformation($"Send-Dataverse completed - Elapsed: {stopwatch.Elapsed}, Batches: {batchCounter}, Operations: {operationCounter}.", ["Dataverse"]);
            return;
        }
        stopwatch.Stop();
        WriteInformation($"Send-Dataverse completed - Elapsed: {stopwatch.Elapsed}, Batches: {batchCounter}, Operations: {operationCounter}.", ["Dataverse"]);

        base.EndProcessing();
    }

    private bool TryGetInputOperation(out Operation<string> operation)
    {
        if (ParameterSetName == "Request")
        {
            operation = new Operation<string>
            {
                ContentId = ContentId,
                Method = Method,
                Uri = Uri,
                Headers = Headers?.Cast<DictionaryEntry>().ToDictionary(
                    entry => entry.Key.ToString(),
                    entry => entry.Value?.ToString()),
                Value = Body switch
                {
                    null => null,
                    string value => value,
                    _ => ConvertToJson(Body)
                }
            };
            return true;
        }
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
                    operation = new Operation<string> { Uri = str };
                    return true;
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
        IsOnPremise = activeConnection.IsOnPremises;
        if (IsOnPremise) { return true; }
        try
        {
            accessToken = activeConnection.GetAccessTokenAsync(CancellationToken)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception exception)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException(
                    $"Connection '{activeConnection.Name}' could not refresh its access token: {exception.Message}",
                    exception),
                Globals.ErrorIdConnectionExpired,
                ErrorCategory.AuthenticationError,
                activeConnection));
            return false;
        }
    }

    internal static int ResolveMaxDop(int maxDop)
        => maxDop <= 0 ? DefaultMaxDop : Math.Min(maxDop, 1024);

    private void QueueBatch()
    {
        var batch = new Batch<string>(operations.ToArray());
        operations.Clear();
        _ = Interlocked.Increment(ref batchCounter);
        WriteInformation($"Batch-{batch.Id}[total:{batch.ChangeSet.Operations.Count()}, starting: {batch.ChangeSet.Operations.First().ContentId}] queued.", ["dataverse"]);
        batchScheduler.EnqueueAsync(batch).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private void DrainBatchResults(bool waitForCompletion)
    {
        do
        {
            while (batchScheduler.Results.TryRead(out var result))
            {
                AcceptBatchResult(result);
            }
            if (!waitForCompletion)
            {
                return;
            }
        }
        while (batchScheduler.Results.WaitToReadAsync(CancellationToken).AsTask()
            .ConfigureAwait(false).GetAwaiter().GetResult());
    }

    private void AcceptBatchResult(ScheduledResult<Batch<string>> result)
    {
        if (OutputOrder.Equals("Input", StringComparison.OrdinalIgnoreCase))
        {
            orderedResults.Add(result.Sequence, result);
            while (orderedResults.Remove(nextOutputSequence, out var next))
            {
                WriteBatchResult(next);
                nextOutputSequence++;
            }
            return;
        }
        WriteBatchResult(result);
    }

    private void WriteBatchResult(ScheduledResult<Batch<string>> result)
    {
        if (result.IsSuccess)
        {
            WriteOutput(result.Value);
            return;
        }
        if (result.Error is OperationCanceledException)
        {
            throw result.Error;
        }
        WriteError(new ErrorRecord(result.Error, Globals.ErrorIdBatchFailure, ErrorCategory.WriteError, this));
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

    private async Task<Batch<string>> SendBatchAsync(Batch<string> batch, CancellationToken cancellationToken)
    {
        BatchResponse response = null;
        try
        {
            response = await batchProcessor.ExecuteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BatchException<string>($"Batch failed: {ex.Message}", ex) { Batch = batch };
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
            if (batchScheduler is not null)
            {
                batchScheduler.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }
        base.Dispose(disposing);
    }

    public void WriteInformation(string messageData, string[] tags) => base.WriteInformation(messageData, tags);
}
