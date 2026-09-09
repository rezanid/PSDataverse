#nullable enable
namespace PSDataverse.Dataverse.Model;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class BatchResponseParser
{
    private const string BatchPrefix = "--batchresponse_";
    private const string ChangeSetPrefix = "changesetresponse_";

    public static BatchResponse Parse(string response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(response);

        var cursor = new LineCursor(response);
        var openingBoundary = cursor.ReadRequired("a batch response boundary").TrimStart('\uFEFF');
        if (!openingBoundary.StartsWith(BatchPrefix, StringComparison.OrdinalIgnoreCase) ||
            openingBoundary.EndsWith("--", StringComparison.Ordinal))
        {
            throw cursor.Error($"Expected an opening '{BatchPrefix}<id>' boundary but found '{Limit(openingBoundary)}'.");
        }

        var outerBoundary = openingBoundary[2..];
        var batchId = openingBoundary[BatchPrefix.Length..];
        var outerHeaders = ReadHeaders(cursor, "batch response");
        if (!outerHeaders.TryGetValue("Content-Type", out var contentType))
        {
            throw cursor.Error("The batch response is missing its Content-Type header.");
        }

        var changeSetBoundary = ParseBoundary(contentType, cursor);
        var boundaryId = changeSetBoundary.StartsWith(ChangeSetPrefix, StringComparison.OrdinalIgnoreCase)
            ? changeSetBoundary[ChangeSetPrefix.Length..]
            : changeSetBoundary;
        var result = new BatchResponse(batchId, boundaryId);
        var openChangeSet = $"--{changeSetBoundary}";
        var closeChangeSet = $"--{changeSetBoundary}--";
        var closeBatch = $"--{outerBoundary}--";
        var batchClosed = false;

        while (!cursor.End)
        {
            cursor.SkipBlankLines();
            var boundary = cursor.Peek();
            if (boundary is null)
            {
                break;
            }
            if (string.Equals(boundary, closeBatch, StringComparison.OrdinalIgnoreCase))
            {
                cursor.Read();
                batchClosed = true;
                break;
            }
            if (string.Equals(boundary, closeChangeSet, StringComparison.OrdinalIgnoreCase))
            {
                cursor.Read();
                cursor.SkipBlankLines();
                ExpectBoundary(cursor, closeBatch);
                batchClosed = true;
                break;
            }
            if (!string.Equals(boundary, openChangeSet, StringComparison.OrdinalIgnoreCase))
            {
                throw cursor.Error(
                    $"Expected changeset boundary '{openChangeSet}' but found '{Limit(boundary)}'.");
            }

            cursor.Read();
            result.Operations.Add(ParseOperation(cursor, closeChangeSet));
        }

        cursor.SkipBlankLines();
        if (!cursor.End)
        {
            throw cursor.Error($"Unexpected content after the closing batch boundary: '{Limit(cursor.Peek())}'.");
        }
        if (!batchClosed)
        {
            throw cursor.Error($"The response ended before closing batch boundary '{closeBatch}'.");
        }
        if (result.Operations.Count == 0)
        {
            throw new ParseException("The Dataverse batch response did not contain any operation responses.");
        }

        result.IsSuccessful = result.Operations.TrueForAll(operation => operation.Error is null);
        return result;
    }

    private static OperationResponse ParseOperation(
        LineCursor cursor,
        string closeChangeSet)
    {
        var headers = ReadHeaders(cursor, "operation MIME part");
        headers.TryGetValue("Content-ID", out var contentId);

        var statusLine = cursor.ReadRequired("an HTTP status line");
        var statusParts = statusLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (statusParts.Length < 2 ||
            !statusParts[0].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(statusParts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var status))
        {
            throw cursor.Error($"Invalid HTTP status line '{Limit(statusLine)}'.");
        }

        var reasonPhrase = statusParts.Length == 3 ? statusParts[2] : null;
        MergeHeaders(headers, ReadHeaders(cursor, "operation HTTP response"));

        var bodyLines = new List<string>();
        while (!cursor.End && !IsBoundaryCandidate(cursor.Peek()))
        {
            bodyLines.Add(cursor.Read()!);
        }
        if (cursor.End)
        {
            throw cursor.Error($"The operation body ended before changeset boundary '{closeChangeSet}'.");
        }
        while (bodyLines.Count > 0 && bodyLines[^1].Length == 0)
        {
            bodyLines.RemoveAt(bodyLines.Count - 1);
        }

        var body = bodyLines.Count == 0 ? null : string.Join('\n', bodyLines);
        var statusCode = (HttpStatusCode)status;
        var error = status is >= 200 and <= 299
            ? null
            : ParseError(statusCode, reasonPhrase, body);

        return new OperationResponse(statusCode, contentId, error, body, headers);
    }

    private static OperationError ParseError(HttpStatusCode statusCode, string? reasonPhrase, string? body)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                var json = JObject.Parse(body);
                var oDataError = json.SelectToken("error")?.ToObject<OperationError>();
                if (oDataError is not null)
                {
                    return oDataError;
                }

                if ((int)statusCode == 429 || json["ErrorCode"] is not null || json["Message"] is not null)
                {
                    return new OperationError
                    {
                        Code = json["ErrorCode"]?.ToString() ?? ((int)statusCode).ToString(CultureInfo.InvariantCulture),
                        Message = json["Message"]?.ToString() ?? body,
                        Type = json["ExceptionType"]?.ToString(),
                        StackTrace = json["StackTrace"]?.ToString()
                    };
                }
            }
            catch (JsonException)
            {
                // Preserve the unparsed body in the typed fallback error.
            }
        }

        return new OperationError
        {
            Code = ((int)statusCode).ToString(CultureInfo.InvariantCulture),
            Message = string.IsNullOrWhiteSpace(body) ? reasonPhrase ?? "Dataverse request failed." : body
        };
    }

    private static Dictionary<string, string> ReadHeaders(LineCursor cursor, string section)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (!cursor.End)
        {
            var line = cursor.ReadRequired($"a header or blank line in the {section}");
            if (line.Length == 0)
            {
                return headers;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                throw cursor.Error($"Invalid header in the {section}: '{Limit(line)}'.");
            }
            AddHeader(headers, line[..separator].Trim(), line[(separator + 1)..].Trim());
        }

        throw cursor.Error($"Unexpected end of response while reading the {section} headers.");
    }

    private static string ParseBoundary(string contentType, LineCursor cursor)
    {
        var mediaTypeAndParameters = contentType.Split(';', StringSplitOptions.TrimEntries);
        if (mediaTypeAndParameters.Length == 0 ||
            !string.Equals(mediaTypeAndParameters[0], "multipart/mixed", StringComparison.OrdinalIgnoreCase))
        {
            throw cursor.Error($"Expected multipart/mixed but found '{Limit(contentType)}'.");
        }

        foreach (var parameter in mediaTypeAndParameters.Skip(1))
        {
            var separator = parameter.IndexOf('=');
            if (separator > 0 &&
                string.Equals(parameter[..separator].Trim(), "boundary", StringComparison.OrdinalIgnoreCase))
            {
                var boundary = parameter[(separator + 1)..].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(boundary))
                {
                    return boundary;
                }
            }
        }

        throw cursor.Error("The batch Content-Type header does not contain a boundary parameter.");
    }

    private static void MergeHeaders(Dictionary<string, string> target, Dictionary<string, string> source)
    {
        foreach (var header in source)
        {
            AddHeader(target, header.Key, header.Value);
        }
    }

    private static void AddHeader(Dictionary<string, string> headers, string name, string value)
    {
        headers[name] = headers.TryGetValue(name, out var existing)
            ? $"{existing}, {value}"
            : value;
    }

    private static void ExpectBoundary(LineCursor cursor, string expected)
    {
        var actual = cursor.ReadRequired($"closing boundary '{expected}'");
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw cursor.Error($"Expected closing boundary '{expected}' but found '{Limit(actual)}'.");
        }
    }

    private static bool IsBoundaryCandidate(string? line)
        => line?.StartsWith("--changesetresponse_", StringComparison.OrdinalIgnoreCase) is true ||
           line?.StartsWith("--batchresponse_", StringComparison.OrdinalIgnoreCase) is true;

    private static string Limit(string? value)
        => value is null ? "<end of response>" : value.Length <= 120 ? value : value[..120] + "…";

    private sealed class LineCursor
    {
        private readonly string[] lines;
        private int index;

        public LineCursor(string value)
        {
            var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            lines = normalized.Split('\n');
        }

        public bool End => index >= lines.Length;
        public int LineNumber => Math.Min(index + 1, lines.Length);
        public string? Peek() => End ? null : lines[index];
        public string? Read() => End ? null : lines[index++];

        public string ReadRequired(string expected)
            => Read() ?? throw Error($"Unexpected end of response; expected {expected}.");

        public void SkipBlankLines()
        {
            while (!End && Peek()!.Length == 0)
            {
                index++;
            }
        }

        public ParseException Error(string message) => new($"Line {LineNumber}: {message}");
    }
}
