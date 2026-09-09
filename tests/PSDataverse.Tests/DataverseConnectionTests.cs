namespace PSDataverse.Tests;

using System.Security;
using FluentAssertions;

public class DataverseConnectionTests
{
    [Fact]
    public async Task ConcurrentTokenRequestsUseOneRefresh()
    {
        var refreshCount = 0;
        using var connection = new DataverseConnection(
            "primary",
            new Uri("https://example.crm.dynamics.com/"),
            "v9.2",
            DataverseAuthenticationKind.TokenProvider,
            new DisposableServiceProvider(),
            async cancellationToken =>
            {
                Interlocked.Increment(ref refreshCount);
                await Task.Delay(20, cancellationToken);
                return new DataverseAccessToken("token", DateTimeOffset.UtcNow.AddHours(1));
            });

        var tokens = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(_ => connection.GetAccessTokenAsync(CancellationToken.None)));

        tokens.Should().OnlyContain(token => token == "token");
        refreshCount.Should().Be(1);
    }

    [Fact]
    public void RegistryReplacesAndDisposesNamedConnections()
    {
        using var registry = new DataverseConnectionRegistry();
        var firstServices = new DisposableServiceProvider();
        var first = CreateConnection("shared", firstServices);
        var second = CreateConnection("shared", new DisposableServiceProvider());

        registry.Add(first);
        registry.Add(second);

        registry.Default.Should().BeSameAs(second);
        second.IsDefault.Should().BeTrue();
        first.IsConnected.Should().BeFalse();
        firstServices.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void RegistryMaintainsOneDefaultWhenConnectionsAreRemoved()
    {
        using var registry = new DataverseConnectionRegistry();
        var alpha = CreateConnection("alpha", new DisposableServiceProvider());
        var beta = CreateConnection("beta", new DisposableServiceProvider());
        registry.Add(alpha);
        registry.Add(beta, setDefault: false);

        registry.SetDefault("beta").Should().BeSameAs(beta);
        _ = registry.Remove("beta");

        registry.Default.Should().BeSameAs(alpha);
        alpha.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task SecureAccessTokenIsUsableButNotPubliclyExposed()
    {
        using var secureToken = new SecureString();
        foreach (var character in "secret-token")
        {
            secureToken.AppendChar(character);
        }
        secureToken.MakeReadOnly();
        using var connection = DataverseConnectionFactory.CreateAccessToken(
            "token",
            new Uri("https://example.crm.dynamics.com/"),
            "v9.2",
            secureToken,
            DateTimeOffset.UtcNow.AddHours(1));

        var token = await connection.GetAccessTokenAsync(CancellationToken.None);

        token.Should().Be("secret-token");
        typeof(DataverseConnection).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain(["Token", "AccessToken"]);
    }

    [Fact]
    public async Task FixedTokenRemainsUsableInsideRefreshWindow()
    {
        using var secureToken = new SecureString();
        foreach (var character in "short-lived")
        {
            secureToken.AppendChar(character);
        }
        using var connection = DataverseConnectionFactory.CreateAccessToken(
            "short",
            new Uri("https://example.crm.dynamics.com/"),
            "v9.2",
            secureToken,
            DateTimeOffset.UtcNow.AddMinutes(2));

        (await connection.GetAccessTokenAsync(CancellationToken.None)).Should().Be("short-lived");
    }

    private static DataverseConnection CreateConnection(
        string name,
        IServiceProvider services)
        => new(
            name,
            new Uri($"https://{name}.crm.dynamics.com/"),
            "v9.2",
            DataverseAuthenticationKind.AccessToken,
            services);

    private sealed class DisposableServiceProvider : IServiceProvider, IDisposable
    {
        public bool IsDisposed { get; private set; }
        public object GetService(Type serviceType) => null!;
        public void Dispose() => IsDisposed = true;
    }
}
