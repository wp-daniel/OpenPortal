using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Audit.Application.Abstractions;
using OpenPortal.Audit.Domain;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Domain;
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

            AccessOperation.ReviewAccessRequests => [PortalPages.AccessRequests],
            AccessOperation.ListOrganizations => [PortalPages.Organizations, PortalPages.Users],
            AccessOperation.ManageOrganizations => [PortalPages.Organizations],
            AccessOperation.ViewStatistics => [PortalPages.Statistics],
            AccessOperation.ReviewAccess => [PortalPages.AccessReviews],

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
    private readonly IApplicationApiCaller _application;
    private readonly IAccessEvaluator _access;

    public UserAdministrationAuthorization(
        CurrentPagePermissions permissions,
        IApplicationApiCaller application,
        IAccessEvaluator access)
    {
        _permissions = permissions;
        _application = application;
        _access = access;
    }

    public async Task<Result> EnsureCanAsync(UserAdministrationOperation operation, CancellationToken cancellationToken)
    {
        // An application's backend may only create ordinary accounts, and only while its token carries the scope
        // and the scope is still enabled for it.
        if (_application.ClientId is { } clientId)
        {
            return operation == UserAdministrationOperation.ProvisionUsers
                && _application.HasScope(ApplicationApiScopes.UsersCreate)
                && await _access.EvaluateApplicationAsync(clientId, cancellationToken).ConfigureAwait(false) is { Allowed: true } decision
                && decision.ApiScopes.Contains(ApplicationApiScopes.UsersCreate, StringComparer.Ordinal)
                    ? Result.Success()
                    : Result.Failure(UserErrors.Forbidden);
        }

        string[] pages = operation switch
        {
            UserAdministrationOperation.ListUsers => [PortalPages.Users, PortalPages.Groups, PortalPages.Access],
            UserAdministrationOperation.ManageUsers or UserAdministrationOperation.ProvisionUsers => [PortalPages.Users],
            UserAdministrationOperation.ExchangeUsers => [PortalPages.Users],
            UserAdministrationOperation.ViewStatistics => [PortalPages.Statistics],
            UserAdministrationOperation.ManageLegalDocuments => [PortalPages.Legal],
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

        return await _permissions.HoldsAnyAsync(pages, cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : Result.Failure(UserErrors.Forbidden);
    }
}

/// <summary>
/// Answers Identity's question "may the caller invite into this organization" from the Access module: the
/// organizations page or being one of the organization's administrators.
/// </summary>
internal sealed class OrganizationInvitationAuthorization : IOrganizationInvitationAuthorization
{
    private readonly IOrganizationService _organizations;

    public OrganizationInvitationAuthorization(IOrganizationService organizations)
    {
        _organizations = organizations;
    }

    public Task<Result> EnsureCanInviteToAsync(Guid organizationId, CancellationToken cancellationToken) =>
        _organizations.EnsureCanManageMembersAsync(organizationId, cancellationToken);
}

/// <summary>Answers the Audit module's authorization question: reading the log needs the audit page.</summary>
internal sealed class AuditLogAuthorization : IAuditLogAuthorization
{
    private readonly CurrentPagePermissions _permissions;

    public AuditLogAuthorization(CurrentPagePermissions permissions)
    {
        _permissions = permissions;
    }

    public async Task<Result> EnsureCanReadAsync(CancellationToken cancellationToken) =>
        await _permissions.HoldsAnyAsync([PortalPages.Audit], cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : Result.Failure(AuditErrors.ReadForbidden);

    public async Task<Result> EnsureCanReadStatisticsAsync(CancellationToken cancellationToken) =>
        await _permissions.HoldsAnyAsync([PortalPages.Statistics], cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : Result.Failure(AuditErrors.StatisticsForbidden);
}

/// <summary>Answers the Notifications module's authorization question: managing announcements needs their page.</summary>
internal sealed class AnnouncementAuthorization : IAnnouncementAuthorization
{
    private readonly CurrentPagePermissions _permissions;

    public AnnouncementAuthorization(CurrentPagePermissions permissions)
    {
        _permissions = permissions;
    }

    public async Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken) =>
        await _permissions.HoldsAnyAsync([PortalPages.Announcements], cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : Result.Failure(AnnouncementErrors.ManageForbidden);
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

/// <summary>
/// Answers the Notifications module's question for email delivery (SMTP settings, the delivery queue):
/// administrators only, like the other portal settings, since the SMTP password is a credential.
/// </summary>
internal sealed class DeliveryAdministrationAuthorization : IDeliveryAdministrationAuthorization
{
    private readonly CurrentPagePermissions _permissions;

    public DeliveryAdministrationAuthorization(CurrentPagePermissions permissions)
    {
        _permissions = permissions;
    }

    public Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_permissions.IsAdministrator ? Result.Success() : Result.Failure(OutboxErrors.ManageForbidden));
}
