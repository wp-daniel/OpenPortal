using System.Security.Claims;

namespace OpenPortal.Client;

/// <summary>The organization (tenant) a user signed in for, as the portal names it in the tokens.</summary>
/// <param name="Id">Stable identifier (<c>org_id</c>): key the application's data by it.</param>
/// <param name="Slug">Short, immutable name (<c>org_slug</c>), also what a sign-in can ask for.</param>
/// <param name="Name">Display name (<c>org_name</c>); may change, so never key data by it.</param>
/// <param name="Role">The user's role in it (<c>org_role</c>): <c>member</c> or <c>admin</c>.</param>
public sealed record OpenPortalOrganization(Guid Id, string Slug, string Name, string Role)
{
    public bool IsAdmin => string.Equals(Role, "admin", StringComparison.Ordinal);
}

/// <summary>Reads what the portal puts in a signed-in user's claims.</summary>
public static class OpenPortalClaims
{
    public const string OrganizationId = "org_id";
    public const string OrganizationSlug = "org_slug";
    public const string OrganizationName = "org_name";
    public const string OrganizationRole = "org_role";

    /// <summary>
    /// The organization the user signed in for, or null when the application does not work per organization (or
    /// the user belongs to none and the application allows that).
    /// </summary>
    public static OpenPortalOrganization? GetOpenPortalOrganization(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Guid.TryParse(user.FindFirst(OrganizationId)?.Value, out var id)
            ? new OpenPortalOrganization(
                id,
                user.FindFirst(OrganizationSlug)?.Value ?? string.Empty,
                user.FindFirst(OrganizationName)?.Value ?? string.Empty,
                user.FindFirst(OrganizationRole)?.Value ?? "member")
            : null;
    }
}
