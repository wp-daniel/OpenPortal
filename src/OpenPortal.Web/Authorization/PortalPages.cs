using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;

namespace OpenPortal.Web.Authorization;

/// <summary>
/// The pages of the portal that an administrator can grant to groups. This is the single list: the page
/// permission screens, the session and the endpoint checks all read it.
/// <para>
/// To add a page: declare its key here and add it to <see cref="All"/> (with its translation keys), put
/// <see cref="RequirePortalPageAttribute"/> on the endpoints it uses, and give the client route
/// <c>handle.page</c> and the sidebar entry <c>page</c> with the same key. It then appears in the permission
/// matrix by itself. <c>PortalPageCatalogTests</c> fails if an administrative endpoint is not tied to a page or
/// a page has no endpoint.
/// </para>
/// <para>
/// Administrators open every page without a grant. Pages that must stay administrator-only (such as the page
/// permissions themselves) are simply not listed and keep <see cref="Policies.AdministratorOnly"/>.
/// </para>
/// </summary>
public static class PortalPages
{
    public const string Users = "users";
    public const string Groups = "groups";
    public const string Access = "access";
    public const string Applications = "applications";
    public const string ContentProfile = "content.profile";
    public const string ContentProjects = "content.projects";
    public const string Audit = "audit";

    /// <summary>Every grantable page, in the order the administration screens list them.</summary>
    public static IReadOnlyList<PortalPageDto> All { get; } =
    [
        new(Users, "nav.users", "nav.identity"),
        new(Groups, "nav.groups", "nav.identity"),
        new(Access, "nav.access", "nav.identity"),
        new(Applications, "nav.applications", "nav.applications"),
        new(ContentProfile, "nav.profile", "nav.content"),
        new(ContentProjects, "nav.projects", "nav.content"),
        new(Audit, "nav.audit", "nav.security"),
    ];
}

/// <summary>Serves <see cref="PortalPages.All"/> to the Access module, which stores grants against the keys.</summary>
internal sealed class PortalPageCatalog : IPortalPageCatalog
{
    private static readonly HashSet<string> Keys = PortalPages.All.Select(page => page.Key).ToHashSet(StringComparer.Ordinal);

    public IReadOnlyList<PortalPageDto> Pages => PortalPages.All;

    public bool IsKnown(string? pageKey) => pageKey is not null && Keys.Contains(pageKey);
}
