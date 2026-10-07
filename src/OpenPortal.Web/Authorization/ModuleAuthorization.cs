using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Domain;
using OpenPortal.Content.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Web.Authorization;

// The modules' second authorization gate, answered from the caller's pages. Each module names the operation
// it is about to perform; only the host knows which pages allow it. The page lists mirror the
// [RequirePortalPage] attributes on the matching endpoints, so the endpoint and the service agree.

/// <summary>Answers the Access module's authorization question.</summary>
internal sealed class AccessAdminAuthorization : IAccessAdminAuthorization
{
    private readonly CurrentPagePermissions _permissions;

    public AccessAdminAuthorization(CurrentPagePermissions permissions)
    {
        _permissions = permissions;
    }

    public async Task<Result> EnsureCanAsync(AccessOperation operation, CancellationToken cancellationToken)
    {
        string[] pages = operation switch
        {
            AccessOperation.ListApplications => [PortalPages.Applications, PortalPages.Groups, PortalPages.Users],
            AccessOperation.ManageApplications => [PortalPages.Applications],
            AccessOperation.ListGroups => [PortalPages.Groups, PortalPages.Access],
            AccessOperation.ManageGroups => [PortalPages.Groups],
            AccessOperation.ViewTree => [PortalPages.Access],
            AccessOperation.ViewUserAccess or AccessOperation.ManageUserGrants => [PortalPages.Access, PortalPages.Users],
            AccessOperation.ManageGroupGrants => [PortalPages.Access, PortalPages.Groups],

            // Granting pages is never delegated: whoever could would be able to grant themselves anything.
            AccessOperation.ManagePagePermissions => [],
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

        var allowed = _permissions.IsAdministrator
            || await _permissions.HoldsAnyAsync(pages, cancellationToken).ConfigureAwait(false);

        return allowed ? Result.Success() : Result.Failure(AccessErrors.AdministrationForbidden);
    }
}

/// <summary>Answers the Identity module's authorization question for administering accounts.</summary>
internal sealed class UserAdministrationAuthorization : IUserAdministrationAuthorization
{
    private readonly CurrentPagePermissions _permissions;

    public UserAdministrationAuthorization(CurrentPagePermissions permissions)
    {
        _permissions = permissions;
    }

    public async Task<Result> EnsureCanAsync(UserAdministrationOperation operation, CancellationToken cancellationToken)
    {
        string[] pages = operation switch
        {
            UserAdministrationOperation.ListUsers => [PortalPages.Users, PortalPages.Groups, PortalPages.Access],
            UserAdministrationOperation.ManageUsers => [PortalPages.Users],
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

        return await _permissions.HoldsAnyAsync(pages, cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : Result.Failure(UserErrors.Forbidden);
    }
}

/// <summary>Answers the Content module's authorization question.</summary>
internal sealed class ContentEditAuthorization : IContentEditAuthorization
{
    private readonly CurrentPagePermissions _permissions;

    public ContentEditAuthorization(CurrentPagePermissions permissions)
    {
        _permissions = permissions;
    }

    public async Task<Result> EnsureCanEditAsync(ContentArea area, CancellationToken cancellationToken)
    {
        var page = area switch
        {
            ContentArea.Profile => PortalPages.ContentProfile,
            ContentArea.Projects => PortalPages.ContentProjects,
            _ => throw new ArgumentOutOfRangeException(nameof(area), area, null),
        };

        return await _permissions.HoldsAnyAsync([page], cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : Result.Failure(Error.Forbidden(
                "content.edit_forbidden",
                "Editing site content requires an administrator account or a page granted to one of your groups."));
    }
}

/// <summary>Publishes the user's pages in the session: every page for an administrator, else their groups' pages.</summary>
internal sealed class PortalUserPageSource : IUserPageSource
{
    private readonly IPagePermissionEvaluator _evaluator;

    public PortalUserPageSource(IPagePermissionEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    public async Task<IReadOnlyList<string>> GetPagesAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roles);

        return roles.Contains(Roles.Administrator, StringComparer.Ordinal)
            ? PortalPages.All.Select(page => page.Key).ToList()
            : await _evaluator.GetPagesAsync(userId, cancellationToken).ConfigureAwait(false);
    }
}
