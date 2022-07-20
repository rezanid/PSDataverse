using DataverseModule.Dataverse.Model;

namespace DataverseModule.Dataverse.Execute
{
    public abstract class Processor<T>
    {
        public string ExtractEntityName(Operation<T> operation)
        {
            var uriSegments = operation.Uri.Split('/');
            string entitySegment = uriSegments[^1] == "$ref" ? uriSegments[^3] : uriSegments[^1];
            var entityNameEnd = entitySegment.IndexOf("(", System.StringComparison.Ordinal);
            if (entityNameEnd == -1) { entityNameEnd = entitySegment.Length; }
            return entitySegment.Substring(0, entityNameEnd);
        }
    }
}
