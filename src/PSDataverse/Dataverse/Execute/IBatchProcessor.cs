namespace PSDataverse.Dataverse.Execute;

using System.Collections.Generic;
using PSDataverse.Dataverse.Model;

public interface IBatchProcessor<T>
{
    //Task ProcessAsync(Batch<T> batch);
    //IAsyncEnumerable<HttpResponseMessage> ProcessAsync(Batch<T> batch);
    IAsyncEnumerable<BatchResponse> ProcessAsync(Batch<T> batch);
    string ExtractEntityName(Operation operation);
}
