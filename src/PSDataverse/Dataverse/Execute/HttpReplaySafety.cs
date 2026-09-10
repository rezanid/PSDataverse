namespace PSDataverse.Dataverse.Execute;

using System;

internal static class HttpReplaySafety
{
    public static bool IsReplaySafe(string method)
        => method is not null &&
           (method.Equals("GET", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("HEAD", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase));
}
