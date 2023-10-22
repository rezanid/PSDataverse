namespace PSDataverse.Dataverse;

using System;
using Newtonsoft.Json;

public class ThrottlingExceededException : Exception
{
    public WebApiFault Details { get; set; }

    public ThrottlingExceededException() : base() { }
    public ThrottlingExceededException(string message) : base(message) { }
    public ThrottlingExceededException(string message, Exception inner) : base(message, inner) { }
    public ThrottlingExceededException(WebApiFault details) : base(details.Message) => Details = details;
    public override string ToString() => JsonConvert.SerializeObject(Details, Formatting.None);
}
