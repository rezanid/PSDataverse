namespace PSDataverse.Dataverse.Execute;
using System;
using System.Management.Automation;
using PSDataverse.Dataverse.Model;

public class OperationHandler
{
    private readonly IOperationReporter reporter;
    private readonly OperationProcessor processor;
    private readonly JsonToPSObjectConverter jsonConverter = new();

    public OperationHandler(OperationProcessor operationProcessor, IOperationReporter operationReporter)
        => (processor, reporter) = (operationProcessor, operationReporter);

    public void ReportMissingOperationError()
    {
        var errMessage = "No operation has been given. Please provide an operation using either of -InputOperation or -InputObject arguments.";
        reporter.WriteError(new ErrorRecord(new InvalidOperationException(errMessage), Globals.ErrorIdMissingOperation, ErrorCategory.ConnectionError, null));
    }

    public static void AssignDefaultHttpMethod(Operation<string> op)
    {
        if (!op.HasValue && op.Method is null)
        {
            op.Method = "GET";
        }
        else
        {
            op.Method ??= "POST";
        }
    }

    public void ExecuteSingleOperation(Operation<string> op, string accessToken, bool autoPagination)
    {
        processor.AuthenticationToken = accessToken;
        try
        {
            var response = processor.ExecuteAsync(op).Result;
            var opResponse = OperationResponse.From(response);

            HandleResponsePagination(op, opResponse, autoPagination);
        }
        catch (OperationException ex)
        {
            reporter.WriteError(new ErrorRecord(ex, Globals.ErrorIdOperationException, ErrorCategory.WriteError, null));
        }
        catch (AggregateException ex) when (ex.InnerException is OperationException)
        {
            reporter.WriteError(new ErrorRecord(ex.InnerException, Globals.ErrorIdOperationException, ErrorCategory.WriteError, null));
        }
        reporter.WriteInformation("Dataverse operation successful.", ["dataverse"]);
    }

    private void HandleResponsePagination(Operation<string> op, OperationResponse opResponse, bool autoPagination)
    {
        if (string.IsNullOrEmpty(opResponse.ContentId))
        {
            opResponse.ContentId = op.ContentId;
        }
        if (autoPagination)
        {
            ProcessPaginatedResults(op, opResponse);
        }
        else
        {
            reporter.WriteObject(opResponse);
        }
    }

    private void ProcessPaginatedResults(Operation<string> op, OperationResponse opResponse)
    {
        do
        {
            var result = jsonConverter.FromODataJsonString(opResponse.Content);
            reporter.WriteObject(result);
            var nextPage = result.Properties["@odata.nextLink"]?.Value as string;

            if (!string.IsNullOrEmpty(nextPage))
            {
                op.Uri = nextPage;
                var response = processor.ExecuteAsync(op).Result;
                opResponse = OperationResponse.From(response);
            }
            else
            {
                break;
            }
        } while (true);
    }
}

