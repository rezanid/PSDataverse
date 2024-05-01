namespace PSDataverse.Dataverse.Model;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Newtonsoft.Json;

[Serializable]
public class Operation
{
    [NonSerialized]
    private string contentId;

    internal virtual bool HasValue => false;

    public virtual string GetValueAsJsonString() => null;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string ContentId
    {
        get => contentId;
        set => contentId = value;
    }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string> Headers { get; set; }
    public string Method { get; set; }
    public string Uri { get; set; }
    public int RunCount { get; set; }
    public override string ToString() => $"ContentID: {ContentId}, Method: {Method}, Url: {Uri}";
}

[Serializable]
public class Operation<T> : Operation
{
    public T Value { get; set; }
    internal override bool HasValue => Value != null;
    public override string GetValueAsJsonString()
    {
        if (!HasValue)
        { return null; }
        if (Value is string str)
        { return str; }
        if (Value is Newtonsoft.Json.Linq.JObject jobj)
        { return jobj.ToString(Formatting.None); }
        return System.Text.Json.JsonSerializer.Serialize(Value);
    }
    public override string ToString() => $"ContentID: {ContentId}, Method: {Method}, Url: {Uri}, Value: {Value}";
}
