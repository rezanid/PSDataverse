namespace PSDataverse;

using System;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PSDataverse.Auth;

internal static class DataverseConnectionFactory
{
    public static async Task<DataverseConnection> CreateMsalAsync(
        string name,
        Uri serviceUrl,
        string apiVersion,
        DataverseAuthenticationKind kind,
        AuthenticationParameters parameters,
        Action<string> onMessageForUser,
        CancellationToken cancellationToken)
    {
        var services = CreateServices(serviceUrl, apiVersion);
        DataverseConnection connection = null;
        try
        {
            connection = new DataverseConnection(
                name,
                serviceUrl,
                apiVersion,
                kind,
                services,
                async token =>
                {
                    parameters.Account = connection.AuthenticationAccount;
                    var result = await services.GetRequiredService<AuthenticationService>()
                        .AuthenticateAsync(parameters, onMessageForUser, token)
                        .ConfigureAwait(false);
                    connection.AuthenticationAccount = result.Account;
                    connection.Account = result.Account?.Username ?? "application";
                    return new DataverseAccessToken(result.AccessToken, result.ExpiresOn);
                });
            connection.AuthenticationParameters = parameters;
            _ = await connection.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            connection?.Dispose();
            if (connection is null)
            {
                services.Dispose();
            }
            throw;
        }
    }

    public static DataverseConnection CreateOnPremises(
        string name,
        Uri serviceUrl,
        string apiVersion)
        => new(name, serviceUrl, apiVersion, DataverseAuthenticationKind.OnPremises,
            CreateServices(serviceUrl, apiVersion));

    public static DataverseConnection CreateAccessToken(
        string name,
        Uri serviceUrl,
        string apiVersion,
        SecureString token,
        DateTimeOffset expiresOn)
    {
        var accessToken = new DataverseAccessToken(Unprotect(token), expiresOn);
        var connection = new DataverseConnection(
            name,
            serviceUrl,
            apiVersion,
            DataverseAuthenticationKind.AccessToken,
            CreateServices(serviceUrl, apiVersion));
        connection.SetAccessToken(accessToken);
        return connection;
    }

    public static async Task<DataverseConnection> CreateTokenProviderAsync(
        string name,
        Uri serviceUrl,
        string apiVersion,
        ScriptBlock provider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Task<DataverseAccessToken> Invoke(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var output = provider.Invoke(token);
            if (output.Count != 1)
            {
                throw new InvalidOperationException("A Dataverse token provider must return exactly one value.");
            }
            return Task.FromResult(ConvertToken(output[0]));
        }

        var connection = new DataverseConnection(
            name,
            serviceUrl,
            apiVersion,
            DataverseAuthenticationKind.TokenProvider,
            CreateServices(serviceUrl, apiVersion),
            Invoke);
        try
        {
            _ = await connection.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public static string Unprotect(SecureString value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var pointer = IntPtr.Zero;
        try
        {
            pointer = Marshal.SecureStringToBSTR(value);
            return Marshal.PtrToStringBSTR(pointer);
        }
        finally
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.ZeroFreeBSTR(pointer);
            }
        }
    }

    private static ServiceProvider CreateServices(Uri serviceUrl, string apiVersion)
        => new Startup(serviceUrl, apiVersion)
            .ConfigureServices(new ServiceCollection())
            .BuildServiceProvider();

    private static DataverseAccessToken ConvertToken(PSObject value)
    {
        var baseObject = value.BaseObject;
        if (baseObject is DataverseAccessToken result)
        {
            return result;
        }
        if (baseObject is SecureString secure)
        {
            var token = Unprotect(secure);
            return new DataverseAccessToken(token, ReadJwtExpiry(token) ?? DateTimeOffset.UtcNow.AddMinutes(50));
        }
        if (baseObject is string text)
        {
            return new DataverseAccessToken(text, ReadJwtExpiry(text) ?? DateTimeOffset.UtcNow.AddMinutes(50));
        }

        var tokenProperty = value.Properties["Token"]?.Value ?? value.Properties["AccessToken"]?.Value;
        var expiryProperty = value.Properties["ExpiresOn"]?.Value;
        var tokenValue = tokenProperty switch
        {
            SecureString secureToken => Unprotect(secureToken),
            string stringToken => stringToken,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            throw new InvalidOperationException(
                "A token provider must return a string, SecureString, DataverseAccessToken, or an object with Token and ExpiresOn properties.");
        }
        var expiresOn = expiryProperty is null
            ? ReadJwtExpiry(tokenValue) ?? DateTimeOffset.UtcNow.AddMinutes(50)
            : LanguagePrimitives.ConvertTo<DateTimeOffset>(expiryProperty);
        return new DataverseAccessToken(tokenValue, expiresOn);
    }

    private static DateTimeOffset? ReadJwtExpiry(string token)
    {
        try
        {
            var segments = token.Split('.');
            if (segments.Length < 2)
            {
                return null;
            }
            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return document.RootElement.TryGetProperty("exp", out var exp)
                ? DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64())
                : null;
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
