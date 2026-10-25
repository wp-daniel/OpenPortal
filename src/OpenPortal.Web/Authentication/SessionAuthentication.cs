using System.Security.Claims;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Settings;

namespace OpenPortal.Web.Authentication;

/// <summary>
/// How a portal session was established (RFC 8176 <c>amr</c> values), read from its cookie. Identity writes
/// <c>pwd</c> for a password sign-in and <c>mfa</c> once an authenticator or recovery code was given; the portal's
/// passkey sign-in writes <c>hwk</c>, <c>user</c> and <c>mfa</c>.
/// </summary>
public static class SessionAuthentication
{
    public static IReadOnlyList<string> Methods(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return [];
        }

        // Identity writes the external provider's name (google, microsoft) as the authentication method of a session
        // it opened through one; the portal reports that as fed.
        var methods = principal.FindAll(AuthenticationMethods.ClaimType)
            .Select(claim => claim.Value)
            .Concat(principal.FindAll(ClaimTypes.AuthenticationMethod)
                .Select(claim => ExternalLoginProviders.IsKnown(claim.Value) ? AuthenticationMethods.Federated : claim.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Identity marks a completed second step with mfa only; the password (or the provider) came first.
        if (methods.Contains(AuthenticationMethods.MultiFactor) && !methods.Contains(AuthenticationMethods.HardwareKey)
            && !methods.Contains(AuthenticationMethods.Password) && !methods.Contains(AuthenticationMethods.Federated))
        {
            methods.Insert(0, AuthenticationMethods.Password);
        }

        return methods;
    }

    /// <summary>Whether the session was established with more than one factor.</summary>
    public static bool IsMultiFactor(ClaimsPrincipal? principal) =>
        Methods(principal).Contains(AuthenticationMethods.MultiFactor, StringComparer.Ordinal);
}
