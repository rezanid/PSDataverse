namespace PSDataverse.Dataverse;

using System;
using PSDataverse.Dataverse.Model;

public class BatchException<T> : Exception
{
    public Batch<T> Batch { get; set; }
    public Guid CorrelationId { get; set; }
    public BatchException() { }
    public BatchException(string message) : base(message) { }
    public BatchException(string message, Exception inner) : base(message, inner) { }
}
