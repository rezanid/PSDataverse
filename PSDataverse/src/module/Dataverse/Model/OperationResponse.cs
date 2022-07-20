using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

#nullable enable
namespace DataverseModule.Dataverse.Model
{
    public class OperationResponse
    {
        public string? ContentId { get; set; }
        public HttpStatusCode? StatusCode { get; set; }
        public string? Content { get; set; }
        public OperationError? Error { get; set; }
        public Dictionary<string, string>? Headers { get; set; }

        #region ctors
        public OperationResponse() { }

        public OperationResponse(HttpStatusCode statusCode, string? contentId)
        {
            StatusCode = statusCode;
            ContentId = contentId;
        }

        public OperationResponse(HttpStatusCode statusCode, string? contentId, OperationError? error)
            : this(statusCode, contentId)
        {
            Error = error;
        }

        public OperationResponse(HttpStatusCode statusCode, string? contentId, string content)
            : this(statusCode, contentId)
        {
            Content = content;
        }

        public OperationResponse(HttpStatusCode statusCode, string? contentId, OperationError? error, Dictionary<string, string>? headers)
            : this(statusCode, contentId, error)
        {
            Headers = headers;
        }

        public OperationResponse(HttpStatusCode statusCode, string? contentId, string content, Dictionary<string, string>? headers)
            : this(statusCode, contentId, content)
        {
            Headers = headers;
        }

        public OperationResponse(HttpStatusCode statusCode, string? contentId, Dictionary<string, string>? headers)
            : this(statusCode, contentId)
        {
            Headers = headers;
        }

        public OperationResponse(HttpStatusCode statusCode, string? contentId, OperationError? error, string? content, Dictionary<string, string>? headers)
            : this(statusCode, contentId, error, headers)
        {
            Content = content;
        }
        #endregion

        public static OperationResponse? From(HttpResponseMessage message)
        {
            if (message == null) { throw new ArgumentNullException(nameof(message)); }
            if (message.Content == null) { throw new InvalidOperationException($"{nameof(message)}'s Content cannot be null"); }
            return new OperationResponse(
                statusCode: message.StatusCode,
                contentId: message.Headers.TryGetValues("Content-ID", out var values) ? string.Join(',', values) : "",
                error: (int)message.StatusCode >= 400 ? System.Text.Json.JsonSerializer.Deserialize<OperationError>(message!.Content!.ToString() ?? "") : null,
                content: message.StatusCode == HttpStatusCode.OK ? message!.Content!.ReadAsStringAsync().Result : null,
                headers: message.Headers.ToDictionary(h => h.Key, h => string.Join(',', h.Value)));
        }

        public static OperationResponse? Parse(StringReader reader)
        {
            //--changesetresponse_66ffbfa0-8e37-4eb1-b843-1b4260b0235e
            var buffer = reader.ReadLine();
            if (buffer == null) { return null; }
            if (buffer.StartsWith("--batchresponse_", StringComparison.OrdinalIgnoreCase) && buffer.EndsWith("--", StringComparison.OrdinalIgnoreCase))
            {
                // end of batch
                return null;
            }
            if (!buffer.StartsWith("--changesetresponse_", StringComparison.OrdinalIgnoreCase))
            {
                throw new ParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Expected \"--changesetresponse_\", but found \"{0}\".", buffer.Substring(0, 20)));
            }
            if (buffer.EndsWith("--", StringComparison.OrdinalIgnoreCase))
            {
                // end of changeset
                buffer = reader.ReadLine();
                if ((!buffer?.StartsWith("--batchresponse_", StringComparison.OrdinalIgnoreCase) ?? false) || (!buffer?.EndsWith("--", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    throw new ParseException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Expected \"--batchresponse_<batch-id>--\", but found \"{0}\".", buffer?.Substring(0, 16)));
                }
                return null;
            }

            //Content-Type: application/http
            //Content-Transfer-Encoding: binary
            //Content-ID: 1
            /*while ((buffer = reader.ReadLine()) != string.Empty)
            {
                if (buffer.StartsWith("Content-ID:"))
                {
                    contentId = int.Parse(buffer.Substring(11).TrimStart());
                }
            }*/
            var headers = ParseHeaders(reader);
            string? contentId = "";
            headers?.TryGetValue("Content-ID", out contentId);

            //HTTP/1.1 412 Precondition Failed
            //*********###*xxxxxxxxxxxxxxxxxxxxx<CRLF>
            buffer = reader.ReadLine();
            if (string.IsNullOrEmpty(buffer)) { return null; }
            var status = int.Parse(buffer.Substring(9, 3), NumberStyles.None, CultureInfo.InvariantCulture);
            // Reason text could also be extracted by `buffer.Substring(13)`;

            //Content-Type: application/json; odata.metadata=minimal<CRLF>
            //OData-Version: 4.0<CRLF>
            /*while ((buffer = reader.ReadLine()) != string.Empty)
            {
                // Skip all the headers.
            }*/
            headers = ParseHeaders(reader);


            if ((199 < status) && (status < 300))
            {
                //HTTP/1.1 204 No Content
                reader.ReadLine();
                return new OperationResponse((HttpStatusCode)status, contentId, headers);
            }
            else
            {
                // Example 1:
                // "HTTP/1.1 412 Precondition Failed\r\n"
                // Example 2:
                // "HTTP/1.1 429 Unknown Status Code\r\n"
                var content = new StringBuilder();
                buffer = reader.ReadLine();
                while (buffer != null && !buffer.StartsWith("--changesetresponse_", StringComparison.OrdinalIgnoreCase))
                {
                    content.AppendLine(buffer);
                    buffer = reader.ReadLine();
                }
                var json = JObject.Parse(content.ToString());
                var error = (status == 429) ?
                    ThrottlingRateLimitExceededExceptionToOperationError(json) :
                    json?.SelectToken("error")?.ToObject<OperationError>();
                return new OperationResponse((HttpStatusCode)status, contentId, error, headers);
            }
        }

        public bool IsRecoverable()
        {
            if (Error == null) { return false; }
            return
                Error.Message == "Generic SQL error." ||
                Error.Code == "429";
        }

        private static Dictionary<string, string>? ParseHeaders(
            StringReader reader,
            bool skipValues = false)
        {
            //Example 1:
            // Content-Type: application/http
            // Content-Transfer-Encoding: binary
            // Content-ID: 1
            //
            // Example 2:
            // Content-Type: application/json; odata.metadata=minimal
            // OData-Version: 4.0
            string? buffer;
            if (skipValues)
            {
                while (reader?.ReadLine() != string.Empty) { };
                return null;
            }
            var headers = new Dictionary<string, string>();
            while (!string.IsNullOrEmpty((buffer = reader?.ReadLine())))
            {
                var separatorPos = buffer.IndexOf(':');
                var key = buffer.Substring(0, separatorPos > 0 ? separatorPos : buffer.Length);
                var value = buffer[(separatorPos + 1)..].Trim();
                if (headers.TryGetValue(key, out string? existingValue))
                {
                    value = existingValue + "; " + value;
                }
                headers[key] = value;
            }

            return headers;
        }

        private static OperationError ThrottlingRateLimitExceededExceptionToOperationError(
            JObject json)
        {
            return new OperationError
            {
                Code = json["ErrorCode"]?.ToString(),
                // Ignore ErrorMessage because it is always the same as Message.
                Message = json["Message"]?.ToString(),
                Type = json["ExceptionType"]?.ToString(),
                StackTrace = json["StackTrace"]?.ToString()
            };
        }
    }
}
#nullable restore
