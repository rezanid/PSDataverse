namespace PSDataverse;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed record BulkOperationCapabilities(
    string TableLogicalName,
    string TableSetName,
    bool CreateMultiple,
    bool UpdateMultiple,
    DateTimeOffset CheckedAt);

internal sealed class BulkOperationCapabilityCache : IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<BulkOperationCapabilities>>> entries =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object aliasLock = new();
    private bool disposed;

    public async Task<(BulkOperationCapabilities Capabilities, bool FromCache)> GetAsync(
        string key,
        bool refresh,
        Func<Task<BulkOperationCapabilities>> loader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(loader);

        var candidate = new Lazy<Task<BulkOperationCapabilities>>(
            loader,
            LazyThreadSafetyMode.ExecutionAndPublication);
        Lazy<Task<BulkOperationCapabilities>> selected;
        lock (aliasLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (refresh)
            {
                RemoveEntryAndAliases(key);
            }
            selected = entries.GetOrAdd(key, candidate);
        }
        var fromCache = !ReferenceEquals(candidate, selected);
        try
        {
            var capabilities = await selected.Value.ConfigureAwait(false);
            RememberAliases(key, capabilities, selected);
            return (capabilities, fromCache);
        }
        catch
        {
            lock (aliasLock)
            {
                ((ICollection<KeyValuePair<string, Lazy<Task<BulkOperationCapabilities>>>>)entries)
                    .Remove(new KeyValuePair<string, Lazy<Task<BulkOperationCapabilities>>>(key, selected));
            }
            throw;
        }
    }

    public void Dispose()
    {
        lock (aliasLock)
        {
            entries.Clear();
            disposed = true;
        }
    }

    private void RememberAliases(
        string requestedKey,
        BulkOperationCapabilities result,
        Lazy<Task<BulkOperationCapabilities>> capabilities)
    {
        lock (aliasLock)
        {
            if (disposed ||
                !entries.TryGetValue(requestedKey, out var current) ||
                !ReferenceEquals(current, capabilities))
            {
                return;
            }
            entries.TryAdd(ForLogicalName(result.TableLogicalName), capabilities);
            if (!string.IsNullOrWhiteSpace(result.TableSetName))
            {
                entries.TryAdd(ForTableSetName(result.TableSetName), capabilities);
            }
        }
    }

    private void RemoveEntryAndAliases(string key)
    {
        lock (aliasLock)
        {
            if (!entries.TryRemove(key, out var removed))
            {
                return;
            }
            foreach (var entry in entries.Where(entry => ReferenceEquals(entry.Value, removed)))
            {
                ((ICollection<KeyValuePair<string, Lazy<Task<BulkOperationCapabilities>>>>)entries)
                    .Remove(entry);
            }
        }
    }

    public static string ForLogicalName(string logicalName)
        => $"LOGICAL:{logicalName.Trim().ToUpperInvariant()}";

    public static string ForTableSetName(string tableSetName)
        => $"SET:{tableSetName.Trim().ToUpperInvariant()}";
}
