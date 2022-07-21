namespace PSDataverse;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

public interface IAuthenticator
{
    IAuthenticator NextAuthenticator { get; set; }

    Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationParameters parameters, Action<string> onMessageForUser = default, CancellationToken cancellationToken = default);

    bool CanAuthenticate(AuthenticationParameters parameters);

    Task<AuthenticationResult> TryAuthenticateAsync(
        AuthenticationParameters parameters, Action<string> onMessageForUser = default, CancellationToken cancellationToken = default);
}
