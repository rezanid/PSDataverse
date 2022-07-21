using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace PSDataverse.Dataverse.Model
{
    [Serializable]
    public class Batch<T>
    {
        public string Id { get; set; }
        public ChangeSet<T> ChangeSet { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public BatchResponse Response { get; set; }
        public int RunCount { get; set; }

        public Batch() { }

        public Batch(ChangeSet<T> changeSet)
        {
            Id = Guid.NewGuid().ToString();
            ChangeSet = changeSet;
        }

        public Batch(IEnumerable<Operation<T>> operations)
        {
            Id = Guid.NewGuid().ToString();
            ChangeSet = new ChangeSet<T> { Id = Guid.NewGuid().ToString(), Operations = operations };
        }

        public Batch(IEnumerable<T> values, string method, string Uri)
        {
            Id = Guid.NewGuid().ToString();
            ChangeSet = new ChangeSet<T>
            {
                Id = Guid.NewGuid().ToString(),
                Operations = values.Select(v => new Operation<T>() { Method = method, Uri = Uri, Value = v }).ToList()
            };
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            if (string.IsNullOrEmpty(Id))
            {
                Id = Guid.NewGuid().ToString();
            }
            if (string.IsNullOrEmpty(ChangeSet.Id))
            {
                ChangeSet.Id = Guid.NewGuid().ToString();
            }

            // Batch Header
            sb.Append("--batch_").AppendLine(Id);
            sb.Append("Content-Type: multipart/mixed;boundary=changeset_").AppendLine(ChangeSet.Id).AppendLine();

            // Change Set
            sb.Append(ChangeSet.ToString());

            // Batch Terminator
            sb.AppendLine().Append("--batch_").Append(Id).AppendLine("--");

            return sb.ToString();
        }

        public MemoryStream ToStringCompressedStream(CompressionLevel level)
        {
            var content = ToString();
            var bytes = Encoding.UTF8.GetBytes(content);
            var compressed = new MemoryStream();
            using (var compressor = new GZipStream(compressed, level, true))
            {
                compressor.Write(bytes, 0, bytes.Length);
            }
            compressed.Seek(0, SeekOrigin.Begin);
            return compressed;
        }

        public MemoryStream ToCompressedJsonStream(CompressionLevel level)
        {
            var content = JsonConvert.SerializeObject(this, Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(content);
            var compressed = new MemoryStream();
            using (var compressor = new GZipStream(compressed, level, true))
            {
                compressor.Write(bytes, 0, bytes.Length);
            }
            compressed.Seek(0, SeekOrigin.Begin);
            return compressed;
        }

        public string ToStringCompressedBase64(CompressionLevel level)
        {
            var bytes = ToStringCompressedStream(level).ToArray();
            return Convert.ToBase64String(bytes);
        }

        public string ToJsonCompressedBase64(CompressionLevel level) => Convert.ToBase64String(ToJsonCompressed(level));

        public byte[] ToJsonCompressed(CompressionLevel level)
        {
            using var stream = ToCompressedJsonStream(level);
            return stream.ToArray();
        }

        public IEnumerable<byte[]> ToJsonCompressed(
            CompressionLevel compressionLevel,
            int maxBinarySize,
            bool useFirstOperationIdAsBatchId)
        {
            return GenerateBatches(ChangeSet.Operations, compressionLevel, maxBinarySize, useFirstOperationIdAsBatchId);
        }

        public IEnumerable<byte[]> ToJsonCompressed(
            CompressionLevel compressionLevel,
            int maxBinarySize)
        {
            return GenerateBatches(ChangeSet.Operations, compressionLevel, maxBinarySize, useFirstOperationIdAsBatchId: false);
        }

        private static IEnumerable<byte[]> GenerateBatches(
            IEnumerable<Operation<T>> operations,
            CompressionLevel compressionLevel,
            int maxBinarySize)
        {
            var batch = new Batch<T>(operations);
            var compressedBatch = batch.ToJsonCompressed(compressionLevel);
            if (operations.Count() == 1 && compressedBatch.Length > maxBinarySize)
            {
                var errorMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "Even with one operation, the size of the batch is {0} which is larger than maximum allowed ({1}).",
                    compressedBatch.Length, maxBinarySize);
                throw new InvalidOperationException(errorMessage);
            }
            else if (compressedBatch.Length > maxBinarySize)
            {
                var firstCount = operations.Count() / 2;
                var v1 = GenerateBatches(operations.Take(firstCount).ToList(), compressionLevel, maxBinarySize);
                var v2 = GenerateBatches(operations.Skip(firstCount).ToList(), compressionLevel, maxBinarySize);
                foreach (var item in v1)
                {
                    yield return item;
                }
                foreach (var item in v2)
                {
                    yield return item;
                }
            }
            else
            {
                yield return compressedBatch;
            }

            yield break;
        }

        private static IEnumerable<byte[]> GenerateBatches(
            IEnumerable<Operation<T>> operations,
            CompressionLevel compressionLevel,
            int maxBinarySize,
            bool useFirstOperationIdAsBatchId)
        {
            var batch = new Batch<T>(operations);
            if (useFirstOperationIdAsBatchId)
            {
                batch.Id = operations.First().ContentId;
            }
            var compressedBatch = batch.ToJsonCompressed(compressionLevel);
            if (operations.Count() == 1 && compressedBatch.Length > maxBinarySize)
            {
                var errorMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "Even with one operation, the size of the batch is {0} which is larger than maximum allowed ({1}).",
                    compressedBatch.Length, maxBinarySize);
                throw new InvalidOperationException(errorMessage);
            }
            else if (compressedBatch.Length > maxBinarySize)
            {
                var firstCount = operations.Count() / 2;
                var v1 = GenerateBatches(operations.Take(firstCount).ToList(), compressionLevel, maxBinarySize);
                var v2 = GenerateBatches(operations.Skip(firstCount).ToList(), compressionLevel, maxBinarySize);
                foreach (var item in v1)
                {
                    yield return item;
                }
                foreach (var item in v2)
                {
                    yield return item;
                }
            }
            else
            {
                yield return compressedBatch;
            }

            yield break;
        }

        public HttpRequestMessage ToHttpRequest(string requestUri)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(ToString())
            };
            AddRequiredHeadersToRequest(request);
            return request;
        }

        public HttpRequestMessage ToCompressedHttpRequest(string requestUri, CompressionLevel level)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StreamContent(ToStringCompressedStream(level))
            };
            AddRequiredHeadersToRequest(request);
            return request;
        }

        internal static Batch<T> Parse(string compressedBase64String)
        {
            var bytes = Convert.FromBase64String(compressedBase64String);
            using (var compressedStream = new MemoryStream(bytes))
            {
                using var decompressed = new MemoryStream();
                using (var decompressor = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    decompressor.CopyTo(decompressed);
                }
                decompressed.Seek(0, SeekOrigin.Begin);
                bytes = decompressed.ToArray();
            }
            var json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<Batch<T>>(json);
        }

        #region Private Methods

        private void AddRequiredHeadersToRequest(HttpRequestMessage request)
        {
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/mixed;boundary=batch_" + Id);
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
            request.Content.Headers.Add("OData-MaxVersion", "4.0");
            request.Content.Headers.Add("OData-Version", "4.0");
        }

        #endregion
    }
}
