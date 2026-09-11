namespace PSDataverse;

using System;

public sealed class DataverseBulkOperationSupport
{
    public string TableLogicalName { get; init; }
    public string TableSetName { get; init; }
    public string Operation { get; init; }
    public bool Supported { get; init; }
    public bool CreateMultiple { get; init; }
    public bool UpdateMultiple { get; init; }
    public bool UpsertMultiple => CreateMultiple && UpdateMultiple;
    public bool FromCache { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
}
