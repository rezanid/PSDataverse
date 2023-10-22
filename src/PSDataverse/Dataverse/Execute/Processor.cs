namespace PSDataverse.Dataverse.Execute;

using PSDataverse.Dataverse.Model;

public abstract class Processor<T>
{
    public string ExtractEntityName(Operation operation)
    {
        var uriSegments = operation.Uri.Split('/');
        var entitySegment = uriSegments[^1] == "$ref" ? uriSegments[^3] : uriSegments[^1];
        var entityNameEnd = entitySegment.IndexOf("(", System.StringComparison.Ordinal);
        if (entityNameEnd == -1)
        { entityNameEnd = entitySegment.Length; }
        return entitySegment[..entityNameEnd];
    }
}
