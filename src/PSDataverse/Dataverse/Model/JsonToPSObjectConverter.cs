namespace PSDataverse.Dataverse.Model;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Management.Automation;
using System.Text.Json;
using System.Threading.Tasks;

public class JsonToPSObjectConverter
{
    public PSObject FromODataJsonString(string jsonString)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonString);
            var rootElement = document.RootElement;

            return ConvertJsonElementToPSObject(rootElement) as PSObject;
        }
        catch (JsonException ex)
        {
            // You can't use WriteError here because it's specific to the Cmdlet.
            // Consider either returning a default value, throwing the exception, or using some other form of error handling.
            throw new InvalidOperationException("Failed to parse JSON.", ex);
        }
    }

    private object ConvertJsonElementToPSObject(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => ConvertJsonObjectToPSObject(element),
            JsonValueKind.Array => ConvertJsonArrayToPSObject(element),
            _ => element.ToString()
        };

    private PSObject ConvertJsonObjectToPSObject(JsonElement element)
    {
        var psObj = new PSObject();

        foreach (var prop in element.EnumerateObject())
        {
            psObj.Properties.Add(new PSNoteProperty(prop.Name, ConvertJsonElementToPSObject(prop.Value)));
        }

        return psObj;
    }

    private object ConvertJsonArrayToPSObject(JsonElement element)
    {
        const int parallelThreshold = 500;

        if (element.GetArrayLength() > parallelThreshold)
        {
            var results = new ConcurrentBag<object>();
            Parallel.ForEach(element.EnumerateArray(), item => results.Add(ConvertJsonElementToPSObject(item)));
            return results.ToList();
        }
        else
        {
            return element.EnumerateArray()
                          .Select(ConvertJsonElementToPSObject)
                          .ToList();
        }
    }
}
