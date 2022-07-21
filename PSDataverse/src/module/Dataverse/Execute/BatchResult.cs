using PSDataverse.Dataverse.Model;
using System;

namespace PSDataverse.Dataverse.Execute
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
