namespace PSDataverse;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DataverseConnectionRegistry : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, DataverseConnection> connections =
        new(StringComparer.OrdinalIgnoreCase);
    private string defaultName;
    private bool disposed;

    public IReadOnlyList<DataverseConnection> Connections
    {
        get
        {
            lock (gate)
            {
                return connections.Values.OrderBy(connection => connection.Name).ToArray();
            }
        }
    }

    public DataverseConnection Default
    {
        get
        {
            lock (gate)
            {
                return defaultName is not null && connections.TryGetValue(defaultName, out var connection)
                    ? connection
                    : null;
            }
        }
    }

    public void Add(DataverseConnection connection, bool setDefault = true)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ObjectDisposedException.ThrowIf(disposed, this);
        DataverseConnection replaced = null;
        lock (gate)
        {
            if (connections.TryGetValue(connection.Name, out replaced))
            {
                replaced.IsDefault = false;
            }
            connections[connection.Name] = connection;
            if (setDefault || defaultName is null)
            {
                SetDefaultCore(connection.Name);
            }
        }
        if (!ReferenceEquals(replaced, connection))
        {
            replaced?.Dispose();
        }
    }

    public DataverseConnection Get(string name = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        lock (gate)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Default;
            }
            return connections.TryGetValue(name, out var connection) ? connection : null;
        }
    }

    public DataverseConnection SetDefault(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ObjectDisposedException.ThrowIf(disposed, this);
        lock (gate)
        {
            if (!connections.TryGetValue(name, out var connection))
            {
                throw new KeyNotFoundException($"No Dataverse connection named '{name}' exists.");
            }
            SetDefaultCore(name);
            return connection;
        }
    }

    public DataverseConnection Remove(string name = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        DataverseConnection removed;
        lock (gate)
        {
            name = string.IsNullOrWhiteSpace(name) ? defaultName : name;
            if (name is null || !connections.Remove(name, out removed))
            {
                return null;
            }
            removed.IsDefault = false;
            if (string.Equals(defaultName, name, StringComparison.OrdinalIgnoreCase))
            {
                defaultName = connections.Keys.OrderBy(value => value).FirstOrDefault();
                if (defaultName is not null)
                {
                    connections[defaultName].IsDefault = true;
                }
            }
        }
        removed.Dispose();
        return removed;
    }

    private void SetDefaultCore(string name)
    {
        foreach (var connection in connections.Values)
        {
            connection.IsDefault = string.Equals(connection.Name, name, StringComparison.OrdinalIgnoreCase);
        }
        defaultName = name;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        DataverseConnection[] snapshot;
        lock (gate)
        {
            snapshot = connections.Values.ToArray();
            connections.Clear();
            defaultName = null;
            disposed = true;
        }
        foreach (var connection in snapshot)
        {
            connection.Dispose();
        }
    }
}
