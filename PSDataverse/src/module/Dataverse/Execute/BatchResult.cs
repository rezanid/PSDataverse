using DataverseModule.Dataverse.Model;
using System;

namespace DataverseModule.Dataverse.Execute
{
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
}