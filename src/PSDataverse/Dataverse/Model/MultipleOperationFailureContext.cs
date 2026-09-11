namespace PSDataverse;

using System.Collections.Generic;

public sealed class MultipleOperationFailureContext
{
    public string ActionName { get; set; }
    public string TableSetName { get; set; }
    public string TableLogicalName { get; set; }
    public int ChunkNumber { get; set; }
    public int ChunkCount { get; set; }
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
    public int StartRow { get; set; }
    public int EndRow { get; set; }
    public string ContentId { get; set; }
    public object[] InputRows { get; set; } = [];
    public List<int> SuccessfulChunkNumbers { get; set; } = [];
    public List<string> FailedContentIds { get; set; } = [];

    public string CreateErrorMessage(string detail)
        => $"{ActionName} failed for chunk {ChunkNumber} of {ChunkCount} " +
           $"(input rows {StartRow}-{EndRow}, content ID '{ContentId}') " +
           $"on table '{TableSetName}': {detail}";
}
