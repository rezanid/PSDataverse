namespace PSDataverse;

using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using PSDataverse.Dataverse.Execute;
using PSDataverse.Dataverse.Model;

[Cmdlet(VerbsDiagnostic.Test, "DataverseBulkOperationSupport", DefaultParameterSetName = LogicalNameParameterSet)]
[OutputType(typeof(bool), typeof(DataverseBulkOperationSupport))]
public sealed class TestDataverseBulkOperationSupportCmdlet : DataverseCmdlet
{
    private const string LogicalNameParameterSet = "LogicalName";
    private const string TableSetNameParameterSet = "TableSetName";
    private DataverseConnection activeConnection;
    private OperationProcessor operationProcessor;

    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = LogicalNameParameterSet)]
    [ValidatePattern("^[A-Za-z_][A-Za-z0-9_]*$")]
    public string LogicalName { get; set; }

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = TableSetNameParameterSet)]
    [ValidatePattern("^[A-Za-z_][A-Za-z0-9_]*$")]
    public string TableSetName { get; set; }

    [Parameter(Mandatory = true, Position = 1)]
    [ValidateSet("CreateMultiple", "UpdateMultiple", "UpsertMultiple")]
    public string Operation { get; set; }

    [Parameter]
    public SwitchParameter Detailed { get; set; }

    [Parameter]
    public SwitchParameter Refresh { get; set; }

    [Parameter]
    public DataverseConnection Connection { get; set; }

    [Parameter]
    public string ConnectionName { get; set; }

    protected override void BeginProcessing()
    {
        base.BeginProcessing();
        activeConnection = ResolveConnection(Connection, ConnectionName);
        operationProcessor = activeConnection.Services.GetRequiredService<OperationProcessor>();
        operationProcessor.AuthenticationToken = activeConnection
            .GetAccessTokenAsync(CancellationToken)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    protected override void ProcessRecord()
    {
        base.ProcessRecord();
        var identifier = ParameterSetName == TableSetNameParameterSet ? TableSetName : LogicalName;
        var key = ParameterSetName == TableSetNameParameterSet
            ? BulkOperationCapabilityCache.ForTableSetName(identifier)
            : BulkOperationCapabilityCache.ForLogicalName(identifier);
        try
        {
            var cached = activeConnection.BulkOperationCapabilities
                .GetAsync(key, Refresh, LoadCapabilitiesAsync)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            var result = CreateResult(cached.Capabilities, cached.FromCache, Operation);
            WriteObject(Detailed ? result : result.Supported);
        }
        catch (Exception exception)
        {
            WriteError(new ErrorRecord(
                exception,
                Globals.ErrorIdBulkCapabilityInspection,
                ErrorCategory.ResourceUnavailable,
                identifier));
        }
    }

    private async Task<BulkOperationCapabilities> LoadCapabilitiesAsync()
    {
        var logicalName = LogicalName;
        var tableSetName = TableSetName;
        if (ParameterSetName == TableSetNameParameterSet)
        {
            var escapedSetName = TableSetName.Replace("'", "''", StringComparison.Ordinal);
            var tableJson = await GetJsonAsync(
                $"EntityDefinitions?$select=LogicalName,EntitySetName&$filter=EntitySetName%20eq%20'{escapedSetName}'")
                .ConfigureAwait(false);
            var matches = tableJson["value"]?.Children<JObject>().ToArray() ?? [];
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one table with entity set name '{TableSetName}', but found {matches.Length}.");
            }
            logicalName = matches[0].Value<string>("LogicalName");
            tableSetName = matches[0].Value<string>("EntitySetName");
        }

        var escapedLogicalName = logicalName.Replace("'", "''", StringComparison.Ordinal);
        var uri = "sdkmessagefilters?$select=sdkmessagefilterid" +
            "&$expand=sdkmessageid($select=name)" +
            "&$filter=(sdkmessageid/name%20eq%20@create%20or%20sdkmessageid/name%20eq%20@update)" +
            $"%20and%20primaryobjecttypecode%20eq%20@table&@create='CreateMultiple'&@update='UpdateMultiple'&@table='{escapedLogicalName}'";
        var json = await GetJsonAsync(uri).ConfigureAwait(false);
        return ParseCapabilities(json, logicalName, tableSetName, DateTimeOffset.UtcNow);
    }

    private async Task<JObject> GetJsonAsync(string uri)
    {
        using var response = await operationProcessor.ExecuteAsync(
            new Operation<string> { Method = HttpMethod.Get.Method, Uri = uri },
            CancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(CancellationToken).ConfigureAwait(false);
        return JObject.Parse(content);
    }

    internal static BulkOperationCapabilities ParseCapabilities(
        JObject response,
        string logicalName,
        string tableSetName,
        DateTimeOffset checkedAt)
    {
        var messages = response["value"]?.Children<JObject>()
            .Select(item => item["sdkmessageid"]?.Value<string>("name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        return new BulkOperationCapabilities(
            logicalName,
            tableSetName,
            messages.Contains("CreateMultiple"),
            messages.Contains("UpdateMultiple"),
            checkedAt);
    }

    internal static DataverseBulkOperationSupport CreateResult(
        BulkOperationCapabilities capabilities,
        bool fromCache,
        string operation)
        => new()
        {
            TableLogicalName = capabilities.TableLogicalName,
            TableSetName = capabilities.TableSetName,
            Operation = operation,
            Supported = operation switch
            {
                "CreateMultiple" => capabilities.CreateMultiple,
                "UpdateMultiple" => capabilities.UpdateMultiple,
                "UpsertMultiple" => capabilities.CreateMultiple && capabilities.UpdateMultiple,
                _ => false
            },
            CreateMultiple = capabilities.CreateMultiple,
            UpdateMultiple = capabilities.UpdateMultiple,
            FromCache = fromCache,
            CheckedAt = capabilities.CheckedAt
        };
}
