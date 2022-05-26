using Newtonsoft.Json;
using System;

namespace DataverseModule.Dataverse.Model
{
    [Serializable]
    public class OperationError
    {
        [JsonProperty("code", NullValueHandling = NullValueHandling.Ignore)]
        public string Code { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }
        [JsonProperty("stacktrace", NullValueHandling = NullValueHandling.Ignore)]
        public string StackTrace { get; set; }
        [JsonProperty("innererror", NullValueHandling = NullValueHandling.Ignore)]
        public OperationError InnerError { get; set; }

        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
    }
}
