namespace PSDataverse.Dataverse.Model;

using System;

public class OperationException : Exception
{
    public OperationError Error { get; set; }
    public string EntityName { get; set; }
    public string BatchId { get; set; }
    public Guid CorrelationId { get; set; }
    public OperationException() { }
    public OperationException(string message) : base(message) { }
    public OperationException(string message, Exception inner) : base(message, inner) { }
}

public class OperationException<T> : OperationException
{
    public Operation<T> Operation { get; set; }
    public OperationException() { }
    public OperationException(string message) : base(message) { }
    public OperationException(string message, Exception inner) : base(message, inner) { }
}
