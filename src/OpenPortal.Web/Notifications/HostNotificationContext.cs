using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Notifications;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Notifications;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Web.Authorization;

namespace OpenPortal.Web.Notifications;

/// <summary>
/// Tells the Notifications module who is calling and who a notice's roles name: the administrators and the members
/// of the groups granted the pages that do that work. The pages are the same ones that open the matching
/// endpoints, so whoever is notified can act on the notice.
/// </summary>
internal sealed class HostNotificationContext : INotificationContext
{
    /// <summary>Each kind of work the modules address notices to, and the pages that allow it.</summary>
    public static IReadOnlyDictionary<string, string[]> RolePages { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [AccessNotificationRoles.RequestReviewers] = [PortalPages.AccessRequests],
        [AccessNotificationRoles.ApplicationReviewers] = [PortalPages.Applications],
        [IdentityNotificationRoles.UserReviewers] = [PortalPages.Users],
    };

    private readonly ICurrentUser _currentUser;
    private readonly IUserLookupService _users;
    private readonly IPagePermissionEvaluator _pages;

    public HostNotificationContext(ICurrentUser currentUser, IUserLookupService users, IPagePermissionEvaluator pages)
    {
        _currentUser = currentUser;
        _users = users;
        _pages = pages;
    }

    public Guid? CurrentUserId => _currentUser.IsAuthenticated ? _currentUser.UserId : null;

    public async Task<IReadOnlyCollection<Guid>> ResolveRoleAsync(string role, CancellationToken cancellationToken)
    {
        if (!RolePages.TryGetValue(role, out var pages))
        {
            return [];
        }

        var administrators = await _users.ListAdministratorIdsAsync(cancellationToken).ConfigureAwait(false);
        var holders = await _pages.GetHoldersAsync(pages, cancellationToken).ConfigureAwait(false);

        return administrators.Concat(holders).ToHashSet();
    }
}
