using DataverseModule.Dataverse.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataverseModule.Dataverse.Execute
{
    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> SendAsJsonAsync<T>(
            this HttpClient client, HttpMethod method, string requestUri, IEnumerable<T> values)
        {
            var sb = new StringBuilder(1000);
            var jsonSettings = new JsonSerializerSettings() { DefaultValueHandling = DefaultValueHandling.Ignore };
            foreach (var value in values)
            {
                sb.Append(JsonConvert.SerializeObject(value, jsonSettings));
            }
            var request = new HttpRequestMessage(method, requestUri) { Content = new StringContent(sb.ToString()) };
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            return await client.SendAsync(request, CancellationToken.None);
        }

        public static async Task<HttpResponseMessage> SendAsync(
            this HttpClient client, HttpMethod method, string requestUri, Batch<JObject> batch)
        {
            var request = new HttpRequestMessage(method, requestUri)
            {
                Content = new StringContent(batch.ToString())
            };
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/mixed;boundary=batch_" + batch.Id);
            return await client.SendAsync(request, CancellationToken.None);
        }

        public static async Task<HttpResponseMessage> SendAsync(
            this HttpClient client, Operation<JObject> operation)
        {
            var request = new HttpRequestMessage(new HttpMethod(operation.Method), operation.Uri);
            if (operation?.Value != null)
            {
                request.Content = new StringContent(operation?.Value?.ToString());
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            }
            if (operation.Headers != null)
            {
                foreach (var header in operation.Headers)
                {
                    request.Content.Headers.Add(header.Key, header.Value);
                }
            }
            return await client.SendAsync(request, CancellationToken.None);
        }
    }
}
