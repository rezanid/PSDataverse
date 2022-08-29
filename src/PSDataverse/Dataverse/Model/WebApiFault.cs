using Newtonsoft.Json;
using System;

namespace PSDataverse.Dataverse
{
    public class WebApiFault
    {
        [JsonIgnore]
        public TimeSpan? RetryAfter { get; set; }

        [JsonProperty()]
        public string Message { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ExceptionMessage { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ExceptionType { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string StackTrace { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(Int32Converter))]
        public int? ErrorCode { get; set; }

        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
    }
}
