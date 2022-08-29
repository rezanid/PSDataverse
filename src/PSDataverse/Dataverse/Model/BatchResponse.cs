using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PSDataverse.Dataverse.Model
{
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
            var reader = new StringReader(response);
            var batchResponse = ParseBatchResponseHeader(reader);
            var operationResponse = OperationResponse.Parse(reader);
            while (operationResponse != null)
            {
                batchResponse.Operations.Add(operationResponse);
                operationResponse = OperationResponse.Parse(reader);
            }
            batchResponse.IsSuccessful =
                batchResponse.Operations.Count > 1 || batchResponse.Operations[0].Error == null;
            return batchResponse;
        }

        #region Private Methods

        private static BatchResponse ParseBatchResponseHeader(StringReader reader)
        {
            //--batchresponse_0ece16b0-e21d-4eb1-8805-feb2a61b887e
            var buffer = reader.ReadLine();
            if (!buffer.StartsWith("--batchresponse_", StringComparison.OrdinalIgnoreCase))
            {
                var length = Math.Min(buffer.Length, 16);
                throw new ParseException(
                    string.Format(CultureInfo.InvariantCulture, "Line 1: Expected \"--batchresponse_\" but found\"{0}\".", buffer.Substring(0, length)));
            }
            var batchResponseId = buffer.Substring(16);
            //Content-Type: multipart/mixed; boundary=changesetresponse_66ffbfa0-8e37-4eb1-b843-1b4260b0235e
            buffer = reader.ReadLine();
            if (!buffer.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
            {
                throw new ParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Line 2: Expected \"Content-Type:\", but found \"{0}\".", buffer.Substring(0, 13)));
            }
            var segments = buffer.Substring(13).Trim().Split(new string[] { "; ", ";" }, StringSplitOptions.None);
            if (!segments[1].StartsWith("boundary=changesetresponse_", StringComparison.OrdinalIgnoreCase))
            {
                throw new ParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Line 2: Expected \"boundary=changesetresponse_\" as the second part of content type, but found \"{0}\".",
                        segments[1].Substring(0, 27)));
            }
            reader.ReadLine();
            return new BatchResponse(batchResponseId, segments[1].Substring(27));
        }

        #endregion
    }
}
