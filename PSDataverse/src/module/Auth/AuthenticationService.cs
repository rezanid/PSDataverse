namespace PSDataverse.Auth;

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
        if (current == null)
        {
            throw new InvalidOperationException("Unable to detect required authentication flow. Please check the input parameters and try again.");
        }
        return await current?.AuthenticateAsync(parameters, onMessageForUser, cancellationToken);
    }
}
