using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Grants;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.Access.Application.Notifications;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// Granting an application to one user and withdrawing it, written once for the administration screens and
/// the application API. The callers check who may do it; this does it, revokes tokens and audits.
/// </summary>
internal sealed class UserGrantOperations
{
    private readonly AccessDbContext _db;
    private readonly AccessQueries _access;
    private readonly IUserDirectory _directory;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly INotifier _notifier;

    public UserGrantOperations(
        AccessDbContext db,
        AccessQueries access,
        IUserDirectory directory,
        IClock clock,
        IAuditTrail audit,
        INotifier notifier)
    {
        _db = db;
        _access = access;
        _directory = directory;
        _clock = clock;
        _audit = audit;
        _notifier = notifier;
    }

    /// <summary>
    /// Grants the application to the user. With <paramref name="roles"/>, the grant carries exactly those roles;
    /// without, an existing grant keeps its roles. Returns whether anything changed. A new grant tells the user,
    /// unless <paramref name="notifyUser"/> is false because the caller tells them in its own words (an approved
    /// access request).
    /// </summary>
    public Task<Result<bool>> GrantAsync(
        PortalApplication application,
        Guid userId,
        IReadOnlyList<string>? roles,
        CancellationToken cancellationToken) =>
        GrantAsync(application, userId, roles, notifyUser: true, cancellationToken);

    /// <inheritdoc cref="GrantAsync(PortalApplication, Guid, IReadOnlyList{string}?, CancellationToken)" />
    public async Task<Result<bool>> GrantAsync(
        PortalApplication application,
        Guid userId,
        IReadOnlyList<string>? roles,
        bool notifyUser,
        CancellationToken cancellationToken,
        ExpiryChange? expiry = null)
    {
        roles = NormaliseRoles(roles);
        if (roles is not null && !roles.All(application.HasRole))
        {
            return Result<bool>.Failure(AccessErrors.RoleNotFound);
        }

        var users = await _directory.FindAsync([userId], cancellationToken).ConfigureAwait(false);
        if (!users.TryGetValue(userId, out var user))
        {
            return Result<bool>.Failure(AccessErrors.UserNotFound);
        }

        var grant = await _db.UserGrants
            .SingleOrDefaultAsync(candidate => candidate.ApplicationId == application.Id && candidate.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        var created = grant is null;
        if (grant is null)
        {
            grant = new ApplicationUserGrant(application.Id, userId, _clock.UtcNow);
            _db.UserGrants.Add(grant);
        }

        var rolesChanged = roles is not null && grant.SetRoles(roles);
        var expiryChanged = expiry is not null && grant.SetExpiry(expiry.ExpiresAtUtc);

        if (!created && !rolesChanged)
        {
            if (expiryChanged)
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return Result<bool>.Success(expiryChanged);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    created ? AccessAuditActions.UserGranted : AccessAuditActions.UserRolesChanged,
                    new AuditSubject(AuditSubjectTypes.User, userId.ToString(), user.Email),
                    AccessAudit.Details(
                        ("application", application.DisplayName),
                        ("clientId", application.ClientId),
                        ("roles", AccessAudit.JoinRoles(grant.Roles)))),
                cancellationToken)
            .ConfigureAwait(false);

        if (created && notifyUser && application.Status == ApplicationStatus.Active)
        {
            await _notifier.NotifyAsync(
                    new Notice
                    {
                        Type = AccessNotificationTypes.ApplicationGranted,
                        Audience = NoticeAudience.Users(userId),
                        Parameters = new Dictionary<string, string?> { ["application"] = application.DisplayName },
                        Link = "/",
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Removes the user's own grant on the application, revoking their tokens unless a group still grants it.
    /// Returns whether there was a grant to remove.
    /// </summary>
    public async Task<bool> RevokeAsync(PortalApplication application, Guid userId, CancellationToken cancellationToken)
    {
        var grant = await _db.UserGrants
            .SingleOrDefaultAsync(candidate => candidate.ApplicationId == application.Id && candidate.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (grant is null)
        {
            return false;
        }

        _db.UserGrants.Remove(grant);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _access.RevokeIfNoLongerAllowedAsync(application, [userId], cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    AccessAuditActions.UserRevoked,
                    await _directory.UserSubjectAsync(userId, cancellationToken).ConfigureAwait(false),
                    AccessAudit.Details(("application", application.DisplayName), ("clientId", application.ClientId))),
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>Role keys are compared lower-cased, as the domain stores them.</summary>
    public static IReadOnlyList<string>? NormaliseRoles(IReadOnlyList<string>? roles) =>
        roles?.Select(role => (role ?? string.Empty).Trim().ToLowerInvariant()).ToList();
}
