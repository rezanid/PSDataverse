namespace PSDataverse.Dataverse.Model;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(response);
        var reader = new StringReader(response);
        var batchResponse = ParseBatchResponseHeader(reader);
        var operationResponse = OperationResponse.Parse(reader);
        while (operationResponse != null)
        {
            batchResponse.Operations.Add(operationResponse);
            operationResponse = OperationResponse.Parse(reader);
        }
        if (batchResponse.Operations.Count == 0)
        {
            throw new ParseException("The Dataverse batch response did not contain any operation responses.");
        }
        batchResponse.IsSuccessful = batchResponse.Operations.TrueForAll(operation => operation.Error == null);
        return batchResponse;
    }

    #region Private Methods

    private static BatchResponse ParseBatchResponseHeader(StringReader reader)
    {
        //--batchresponse_0ece16b0-e21d-4eb1-8805-feb2a61b887e
        var buffer = reader.ReadLine();
        if (buffer is null || !buffer.StartsWith("--batchresponse_", StringComparison.OrdinalIgnoreCase))
        {
            var found = buffer ?? "<end of response>";
            var length = Math.Min(found.Length, 16);
            throw new ParseException(
                string.Format(CultureInfo.InvariantCulture, "Line 1: Expected \"--batchresponse_\" but found \"{0}\".", found[..length]));
        }
        var batchResponseId = buffer[16..];
        //Content-Type: multipart/mixed; boundary=changesetresponse_66ffbfa0-8e37-4eb1-b843-1b4260b0235e
        buffer = reader.ReadLine();
        if (buffer is null || !buffer.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
        {
            throw new ParseException(
                "Line 2: Expected a Content-Type header for the batch response.");
        }
        var segments = buffer[13..].Trim().Split(new string[] { "; ", ";" }, StringSplitOptions.None);
        var boundary = Array.Find(segments, segment =>
            segment.Trim().StartsWith("boundary=changesetresponse_", StringComparison.OrdinalIgnoreCase))?.Trim();
        if (boundary is null)
        {
            throw new ParseException(
                "Line 2: Expected a boundary=changesetresponse_ parameter in the Content-Type header.");
        }
        reader.ReadLine();
        return new BatchResponse(batchResponseId, boundary[27..]);
    }

    #endregion
}
