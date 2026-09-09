namespace PSDataverse;

using System;

public sealed record DataverseAccessToken(string Token, DateTimeOffset ExpiresOn);
