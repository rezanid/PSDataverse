namespace PSDataverse;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AsyncKeyedLock;

//TODO: Implement IDictionary<TKey, TValue>.
internal class AsyncDictionary<TKey, TValue> : IDisposable
{
    private readonly ConcurrentDictionary<TKey, TValue> dictionary = new();
    private readonly AsyncKeyedLocker<TKey> locks = new();
    private bool disposedValue;

    public async Task<TValue> GetOrAddAsync(TKey key, Func<TKey, Task<TValue>> valueFactory)
    {
        if (dictionary.TryGetValue(key, out var existingValue))
        {
            return existingValue;
        }

        using (await locks.LockAsync(key))
        {
            // Check if the value has been added by another thread
            if (dictionary.TryGetValue(key, out existingValue))
            {
                return existingValue;
            }

            // Create and store the value if successful
            var value = await valueFactory(key);
            dictionary[key] = value;

            return value;
        }
    }

    public async Task<TValue> GetOrAddAsync(
        TKey key,
        Func<TKey, CancellationToken, Task<TValue>> valueFactory,
        CancellationToken cancellationToken)
    {
        if (dictionary.TryGetValue(key, out var existingValue))
        {
            return existingValue;
        }

        using (await locks.LockAsync(key, cancellationToken))
        {
            // Check if the value has been added by another thread
            if (dictionary.TryGetValue(key, out existingValue))
            {
                return existingValue;
            }

            // Create and store the value if successful
            var value = await valueFactory(key, cancellationToken);
            dictionary[key] = value;

            return value;
        }
    }

    public bool TryRemove(TKey key, out TValue value)
    {
        using var lockAcquired = locks.LockOrNull(key, 0);
        if (lockAcquired is null)
        { // another thread is adding to the dictionary, we want to avoid a race condition
            value = default;
            return false;
        }
        return dictionary.TryRemove(key, out value);
    }

    #region Disposable
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                locks.Dispose();
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
