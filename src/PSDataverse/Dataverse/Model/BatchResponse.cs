namespace PSDataverse.Dataverse.Model;

using System;
using System.Collections.Generic;

[Serializable]
public class BatchResponse
{
    public string Id { get; }
    public string BoundaryId { get; }
    public bool IsSuccessful { get; set; }
    public List<OperationResponse> Operations { get; set; }

    public BatchResponse() { }

    public BatchResponse(string batchId, string boundaryId)
    {
        Id = batchId;
        BoundaryId = boundaryId;
        Operations = new List<OperationResponse>();
    }

    public static BatchResponse Parse(string response)
        => BatchResponseParser.Parse(response);
}
