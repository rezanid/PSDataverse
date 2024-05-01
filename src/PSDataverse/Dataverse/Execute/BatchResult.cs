namespace PSDataverse.Dataverse.Execute;

using System;
using PSDataverse.Dataverse.Model;

public class BatchResult
{
    public TimeSpan Elapsed { get; set; }
    public BatchResponse Response { get; set; }
    public BatchResult(TimeSpan elapsed, BatchResponse response)
    {
        Elapsed = elapsed;
        Response = response;
    }
}
