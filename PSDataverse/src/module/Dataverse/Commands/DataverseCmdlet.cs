namespace DataverseModule;

using System;
using System.Management.Automation;
using System.Threading;

public abstract class DataverseCmdlet : PSCmdlet, IDisposable
{
    private Guid correlationId;
    private CancellationTokenSource cancellationSource;
    protected Guid CorrelationId => correlationId;
    protected CancellationToken CancellationToken => cancellationSource.Token;
    protected bool Disposed { get; set;}

    protected override void BeginProcessing()
    {
        cancellationSource ??= new CancellationTokenSource();

        if (correlationId == default) { correlationId = Guid.NewGuid(); }
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
        if (cancellationSource == null) { return; }
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
        if (Disposed) { return; }
        if (disposing)
        {
            cancellationSource?.Dispose();
        }
        Disposed = true;
    }
    #endregion

}
