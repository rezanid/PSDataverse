namespace PSDataverse.Dataverse.Execute;

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

internal sealed record ScheduledResult<T>(long Sequence, T Value, Exception Error)
{
    public bool IsSuccess => Error is null;
}

internal sealed class BoundedChannelScheduler<TInput, TOutput> : IAsyncDisposable
{
    private readonly Channel<ScheduledItem> input;
    private readonly Channel<ScheduledResult<TOutput>> output;
    private readonly Func<TInput, CancellationToken, Task<TOutput>> handler;
    private readonly Func<TOutput, int?> degreeOfParallelismHint;
    private readonly AdaptiveConcurrencyGate concurrencyGate;
    private readonly CancellationToken cancellationToken;
    private readonly Task[] workers;
    private long sequence = -1;
    private int remainingWorkers;

    public BoundedChannelScheduler(
        int capacity,
        int degreeOfParallelism,
        Func<TInput, CancellationToken, Task<TOutput>> handler,
        CancellationToken cancellationToken,
        Func<TOutput, int?> degreeOfParallelismHint = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(degreeOfParallelism, 1);
        ArgumentNullException.ThrowIfNull(handler);

        this.handler = handler;
        this.degreeOfParallelismHint = degreeOfParallelismHint;
        this.cancellationToken = cancellationToken;
        concurrencyGate = new AdaptiveConcurrencyGate(degreeOfParallelism);
        input = Channel.CreateBounded<ScheduledItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = degreeOfParallelism == 1
        });
        output = Channel.CreateUnbounded<ScheduledResult<TOutput>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = degreeOfParallelism == 1
        });
        remainingWorkers = degreeOfParallelism;
        workers = new Task[degreeOfParallelism];
        for (var index = 0; index < workers.Length; index++)
        {
            workers[index] = RunWorkerAsync();
        }
    }

    public ChannelReader<ScheduledResult<TOutput>> Results => output.Reader;
    internal int DegreeOfParallelism => concurrencyGate.Limit;

    public ValueTask EnqueueAsync(TInput value)
        => input.Writer.WriteAsync(
            new ScheduledItem(Interlocked.Increment(ref sequence), value),
            cancellationToken);

    public void Complete() => input.Writer.TryComplete();

    private async Task RunWorkerAsync()
    {
        try
        {
            await foreach (var item in input.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                ScheduledResult<TOutput> result;
                try
                {
                    TOutput value;
                    using (await concurrencyGate.EnterAsync(cancellationToken).ConfigureAwait(false))
                    {
                        value = await handler(item.Value, cancellationToken).ConfigureAwait(false);
                    }
                    var hint = degreeOfParallelismHint?.Invoke(value);
                    if (hint.HasValue)
                    {
                        await concurrencyGate.UpdateLimitAsync(hint.Value, cancellationToken).ConfigureAwait(false);
                    }
                    result = new ScheduledResult<TOutput>(item.Sequence, value, null);
                }
                catch (Exception exception)
                {
                    result = new ScheduledResult<TOutput>(item.Sequence, default, exception);
                }
                await output.Writer.WriteAsync(result, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The active item carries cancellation as its result; queued work is abandoned.
        }
        finally
        {
            if (Interlocked.Decrement(ref remainingWorkers) == 0)
            {
                output.Writer.TryComplete();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Complete();
        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        concurrencyGate.Dispose();
    }

    private sealed record ScheduledItem(long Sequence, TInput Value);
}

internal sealed class AdaptiveConcurrencyGate : IDisposable
{
    private readonly int maximum;
    private readonly SemaphoreSlim permits;
    private readonly SemaphoreSlim adjustment = new(1, 1);
    private int limit;
    private int reservedPermits;

    public AdaptiveConcurrencyGate(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximum, 1);
        this.maximum = maximum;
        limit = maximum;
        permits = new SemaphoreSlim(maximum, maximum);
    }

    public int Limit => Volatile.Read(ref limit);

    public async ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await permits.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(permits);
    }

    public async Task UpdateLimitAsync(int requestedLimit, CancellationToken cancellationToken)
    {
        var newLimit = Math.Clamp(requestedLimit, 1, maximum);
        await adjustment.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var currentLimit = limit;
            if (newLimit < currentLimit)
            {
                var permitsToReserve = currentLimit - newLimit;
                for (var index = 0; index < permitsToReserve; index++)
                {
                    await permits.WaitAsync(cancellationToken).ConfigureAwait(false);
                    reservedPermits++;
                }
            }
            else if (newLimit > currentLimit)
            {
                var permitsToRestore = Math.Min(newLimit - currentLimit, reservedPermits);
                if (permitsToRestore > 0)
                {
                    reservedPermits -= permitsToRestore;
                    permits.Release(permitsToRestore);
                }
            }
            Volatile.Write(ref limit, newLimit);
        }
        finally
        {
            adjustment.Release();
        }
    }

    public void Dispose()
    {
        adjustment.Dispose();
        permits.Dispose();
    }

    private sealed class Lease(SemaphoreSlim permits) : IDisposable
    {
        private SemaphoreSlim permits = permits;

        public void Dispose() => Interlocked.Exchange(ref permits, null)?.Release();
    }
}
