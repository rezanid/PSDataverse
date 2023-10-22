namespace PSDataverse.Dataverse.Model;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[Serializable]
public class ChangeSet<T>
{
    private List<Operation<T>> _Operations;

    public string Id { get; set; }

    public IEnumerable<Operation<T>> Operations
    {
        get => _Operations;
        set => _Operations = value?.ToList();
    }

    public ChangeSet() { }

    public void RemoveOperation(string contentId)
    {
        var operation = _Operations.Find(o => Equals(o.ContentId == contentId, StringComparison.Ordinal));
        if (operation == null)
        {
            throw new ArgumentOutOfRangeException(nameof(contentId), $"No operation has been found with the given {nameof(contentId)}.");
        }
        _Operations.Remove(operation);
    }

    public void RemoveOperation(Operation<T> operation) => _Operations.Remove(operation);

    public override string ToString()
    {
        var sb = new StringBuilder();
        var i = 0;
        // var ToJson =
        //     typeof(T).Name.Equals("JObject", StringComparison.Ordinal) ?
        //     new Func<object, string>(ConvertJObjectToJson) :
        //     new Func<object, string>(ConvertToJson);
        var ToJson = Operations is IEnumerable<Operation<JObject>> ?
            new Func<object, string>(ConvertJObjectToJson) :
            new Func<object, string>(ConvertToJson);

        if (string.IsNullOrEmpty(Id))
        {
            Id = Guid.NewGuid().ToString();
        }

        foreach (var operation in Operations)
        {
            //if (!operation.ContentId.HasValue) { operation.ContentId = ++i; };
            if (string.IsNullOrEmpty(operation.ContentId))
            { operation.ContentId = (++i).ToString(CultureInfo.InvariantCulture); }
            sb.Append("--changeset_").AppendLine(Id);
            sb.AppendLine("Content-Type:application/http");
            sb.AppendLine("Content-Transfer-Encoding:binary");
            sb.Append("Content-ID:").AppendLine(operation.ContentId.ToString()).AppendLine();
            sb.Append(operation.Method).Append(' ').Append(operation.Uri).Append(' ').AppendLine("HTTP/1.1");
            if (operation.HasValue)
            { sb.AppendLine("Content-Type:application/json;type=entry"); }
            if (operation.Headers != null)
            {
                foreach (var header in operation.Headers)
                {
                    sb.AppendLine(header.Key + ":" + header.Value);
                }
            }
            sb.AppendLine();
            if (operation.HasValue)
            { sb.AppendLine(ToJson(operation.Value)); }
        }

        // Terminator
        sb.Append("--changeset_").Append(Id).AppendLine("--");

        return sb.ToString();
    }

    private string ConvertJObjectToJson(object obj) => ((JObject)obj).ToString(Formatting.None);

    private string ConvertToJson(object obj) => System.Text.Json.JsonSerializer.Serialize(obj);
}
