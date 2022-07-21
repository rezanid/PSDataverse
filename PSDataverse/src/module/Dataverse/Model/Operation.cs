using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace PSDataverse.Dataverse.Model
{
    [Serializable]
    public class Operation<T>
    {
        [NonSerialized]
        private string _ContentId;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ContentId
        {
            get { return _ContentId; }
            set { _ContentId = value; }
        }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> Headers { get; set; }
        public T Value { get; set; }
        public string Method { get; set; }
        public string Uri { get; set; }
        public int RunCount { get; set; }
        public override string ToString() => $"ContentID: {ContentId}, Method: {Method}, Url: {Uri}, Value: {Value}";
    }
}
