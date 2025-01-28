namespace PSDataverse;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

internal interface IAuthenticator
{
    internal IAuthenticator NextAuthenticator { get; set; }

    internal Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationParameters parameters, Action<string> onMessageForUser = default, CancellationToken cancellationToken = default);

    internal bool CanAuthenticate(AuthenticationParameters parameters);

    internal Task<AuthenticationResult> TryAuthenticateAsync(
        AuthenticationParameters parameters, Action<string> onMessageForUser = default, CancellationToken cancellationToken = default);
}
