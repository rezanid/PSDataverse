namespace DataverseModule.Auth;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

internal class AuthenticationService
{
    public IAuthenticator Authenticator { get; set; }

    public AuthenticationService(IAuthenticator authenticator)
    {
        Authenticator = authenticator;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationParameters parameters,
        Action<string> onMessageForUser = default,
        CancellationToken cancellationToken = default)
    {
        var current = Authenticator;
        while (current != null && !current.CanAuthenticate(parameters))
        {
            current = current.NextAuthenticator;
        }
        return await current?.AuthenticateAsync(parameters, onMessageForUser, cancellationToken);
    }
}
