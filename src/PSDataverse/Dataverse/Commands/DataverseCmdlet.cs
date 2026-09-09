namespace PSDataverse;

using System;
using System.Management.Automation;
using System.Threading;

public abstract class DataverseCmdlet : PSCmdlet, IDisposable
{
    private CancellationTokenSource cancellationSource;
    protected Guid CorrelationId { get; private set; }
    protected CancellationToken CancellationToken => cancellationSource.Token;
    protected bool Disposed { get; set; }

    protected DataverseConnectionRegistry GetConnectionRegistry(bool create = true)
    {
        var registry = GetVariableValue(Globals.VariableNameConnectionRegistry) as DataverseConnectionRegistry;
        if (registry is null && create)
        {
            registry = new DataverseConnectionRegistry();
            SessionState.PSVariable.Set(
                new PSVariable(Globals.VariableNameConnectionRegistry, registry, ScopedItemOptions.AllScope));
        }
        return registry;
    }

    protected DataverseConnection ResolveConnection(
        DataverseConnection connection = null,
        string connectionName = null)
    {
        if (connection is not null && !string.IsNullOrWhiteSpace(connectionName))
        {
            throw new PSArgumentException("Specify either -Connection or -ConnectionName, not both.");
        }
        var resolved = connection ?? GetConnectionRegistry(create: false)?.Get(connectionName);
        if (resolved is null)
        {
            var message = string.IsNullOrWhiteSpace(connectionName)
                ? "No active connection detected. Run Connect-Dataverse first."
                : $"No Dataverse connection named '{connectionName}' exists.";
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException(message),
                string.IsNullOrWhiteSpace(connectionName) ? Globals.ErrorIdNotConnected : Globals.ErrorIdConnectionNotFound,
                ErrorCategory.ConnectionError,
                connectionName));
        }
        return resolved;
    }

    protected override void BeginProcessing()
    {
        cancellationSource ??= new CancellationTokenSource();

        if (CorrelationId == default)
        { CorrelationId = Guid.NewGuid(); }
    }

    protected override void EndProcessing()
    {
        CleanupCancellationSource();
        base.EndProcessing();
    }

    /// <summary>
    /// Process the stop (Ctrl+C) signal.
    /// </summary>
    protected override void StopProcessing()
    {
        CleanupCancellationSource();
        base.StopProcessing();
    }

    private void CleanupCancellationSource()
    {
        if (cancellationSource == null)
        { return; }
        if (!cancellationSource.IsCancellationRequested)
        {
            cancellationSource.Cancel();
        }

        cancellationSource.Dispose();
        cancellationSource = null;
    }

    #region Dispose Pattern

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed)
        { return; }
        if (disposing)
        {
            cancellationSource?.Dispose();
        }
        Disposed = true;
    }
    #endregion

}
