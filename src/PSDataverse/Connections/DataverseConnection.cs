namespace PSDataverse;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

public sealed class DataverseConnection : IDisposable
{
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private readonly Func<CancellationToken, Task<DataverseAccessToken>> tokenProvider;
    private bool disposed;
    private DataverseAccessToken accessToken;

    internal DataverseConnection(
        string name,
        Uri serviceUrl,
        string apiVersion,
        DataverseAuthenticationKind authenticationKind,
        IServiceProvider services,
        Func<CancellationToken, Task<DataverseAccessToken>> tokenProvider = null)
    {
        Name = name;
        ServiceUrl = serviceUrl;
        ApiVersion = apiVersion;
        AuthenticationKind = authenticationKind;
        Services = services;
        this.tokenProvider = tokenProvider;
    }

    public string Name { get; }
    public Uri ServiceUrl { get; }
    public string ApiVersion { get; }
    public DataverseAuthenticationKind AuthenticationKind { get; }
    public string Account { get; internal set; }
    public DateTimeOffset? ExpiresOn => accessToken?.ExpiresOn;
    public bool IsDefault { get; internal set; }
    public bool IsConnected => !disposed;

    internal IServiceProvider Services { get; }
    internal AuthenticationParameters AuthenticationParameters { get; set; }
    internal IAccount AuthenticationAccount { get; set; }
    internal bool IsOnPremises => AuthenticationKind == DataverseAuthenticationKind.OnPremises;
    internal BulkOperationCapabilityCache BulkOperationCapabilities { get; } = new();

    internal void SetAccessToken(DataverseAccessToken value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value.Token))
        {
            throw new ArgumentException("Access token cannot be empty.", nameof(value));
        }
        if (value.ExpiresOn <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Access token is already expired.", nameof(value));
        }
        accessToken = value;
    }

    internal async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsOnPremises)
        {
            return string.Empty;
        }
        if (HasUsableToken())
        {
            return accessToken.Token;
        }
        if (tokenProvider is null)
        {
            throw new InvalidOperationException($"The access token for connection '{Name}' has expired. Reconnect first.");
        }

        await tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (HasUsableToken())
            {
                return accessToken.Token;
            }
            var refreshedToken = await tokenProvider(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Token provider for connection '{Name}' returned no token.");
            if (string.IsNullOrWhiteSpace(refreshedToken.Token))
            {
                throw new InvalidOperationException($"Token provider for connection '{Name}' returned an empty token.");
            }
            if (refreshedToken.ExpiresOn <= DateTimeOffset.UtcNow)
            {
                throw new InvalidOperationException($"Token provider for connection '{Name}' returned an expired token.");
            }
            accessToken = refreshedToken;
            return accessToken.Token;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private bool HasUsableToken()
        => accessToken is not null && accessToken.ExpiresOn >
            (tokenProvider is null ? DateTimeOffset.UtcNow : DateTimeOffset.UtcNow.AddMinutes(5));

    public override string ToString() => $"{Name} [{AuthenticationKind}] {ServiceUrl}";

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        (Services as IDisposable)?.Dispose();
        BulkOperationCapabilities.Dispose();
        tokenLock.Dispose();
        accessToken = null;
        disposed = true;
    }
}
