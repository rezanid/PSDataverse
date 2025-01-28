namespace PSDataverse;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

//TODO: Implement IDictionary<TKey, TValue>.
public class AsyncDictionary<TKey, TValue> : IDisposable
{
    private readonly ConcurrentDictionary<TKey, TValue> dictionary = new();
    private readonly ConcurrentDictionary<TKey, SemaphoreSlim> locks = new();
    private bool disposedValue;

    public async Task<TValue> GetOrAddAsync(TKey key, Func<TKey, Task<TValue>> valueFactory)
    {
        var semaphore = locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        try
        {
            // Check if the value has been added by another thread
            if (dictionary.TryGetValue(key, out TValue existingValue))
            {
                return existingValue;
            }

            // Create and store the value if successful
            TValue value = await valueFactory(key);
            dictionary[key] = value;

            return value;
        }
        finally
        {
            semaphore.Release();
            // Avoid removing a semaphore that another thread may need
            if (locks.TryGetValue(key, out var existingSemaphore) && existingSemaphore.CurrentCount == 1)
            {
                locks.TryRemove(key, out _);
            }
        }
    }

    public async Task<TValue> GetOrAddAsync(
        TKey key,
        Func<TKey, CancellationToken, Task<TValue>> valueFactory,
        CancellationToken cancellationToken)
    {
        var semaphore = locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (dictionary.TryGetValue(key, out TValue existingValue))
            {
                return existingValue;
            }

            TValue value = await valueFactory(key, cancellationToken);
            dictionary[key] = value;

            return value;
        }
        finally
        {
            semaphore.Release();
            // Avoid removing a semaphore that another thread may need
            if (locks.TryGetValue(key, out var existingSemaphore) && existingSemaphore.CurrentCount == 1)
            {
                locks.TryRemove(key, out _);
            }
        }
    }

    public bool TryRemove(TKey key, out TValue value)
    {
        bool removed = dictionary.TryRemove(key, out value);
        if (removed)
        {
            if (locks.TryRemove(key, out var semaphore))
            {
                semaphore.Dispose();
            }
        }
        return removed;
    }

    #region Disposable
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                foreach (var semaphore in locks.Values)
                {
                    semaphore.Dispose();
                }
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
