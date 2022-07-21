using PSDataverse.Dataverse.Model;
using System.Collections.Generic;
using System.Net.Http;

namespace PSDataverse.Dataverse.Execute
{
    public interface IBatchProcessor<T>
    {
        //Task ProcessAsync(Batch<T> batch);
        //IAsyncEnumerable<HttpResponseMessage> ProcessAsync(Batch<T> batch);
        IAsyncEnumerable<BatchResponse> ProcessAsync(Batch<T> batch);
        string ExtractEntityName(Operation<T> operation);
    }
}
