using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Web.Authorization;

/// <summary>
/// Names of the authorization policies the host defines.
/// <para>
/// Policies are referenced by name from <c>[Authorize]</c> attributes, which means a typo becomes a runtime
/// failure rather than a compile error. Centralising the strings here keeps that risk to one file.
/// </para>
/// </summary>
public static class Policies
{
    /// <summary>
    /// Requires an authenticated account holding the administrator role. Applied to the endpoints that are
    /// never delegated to a group (the page permissions); the others use <see cref="RequirePortalPageAttribute"/>.
    /// </summary>
    public const string AdministratorOnly = "AdministratorOnly";
}

/// <summary>Shared authorization requirements, so the host and the modules agree on who counts as an admin.</summary>
public static class AuthorizationSetup
{
    /// <summary>
    /// Registers the policies the host enforces.
    /// <para>
    /// The check is written against the claim directly instead of using <c>RequireRole</c>, so that it
    /// behaves identically regardless of how the role claim type has been configured on the cookie scheme.
    /// The application services perform the same check through <c>ICurrentUser</c>, and both read the same
    /// claim, so the endpoint and the service can never disagree.
    /// </para>
    /// </summary>
    public static IServiceCollection AddOpenPortalAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                Policies.AdministratorOnly,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(ClaimTypes.Role, Roles.Administrator));
        });

        // Page checks: [RequirePortalPage] carries its own requirement, answered from the caller's pages.
        services.AddScoped<CurrentPagePermissions>();
        services.AddScoped<IAuthorizationHandler, PortalPageAuthorizationHandler>();

        return services;
    }
}