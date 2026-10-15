using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>Deletes an account and records it. See <see cref="IAccountErasure"/>.</summary>
internal sealed class IdentityAccountErasure : IAccountErasure
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserLookup _userLookup;
    private readonly IAuditTrail _audit;

    public IdentityAccountErasure(UserManager<ApplicationUser> userManager, UserLookup userLookup, IAuditTrail audit)
    {
        _userManager = userManager;
        _userLookup = userLookup;
        _audit = audit;
    }

    public async Task<Result> EraseAsync(Guid userId, AuditSubject? actor, CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindAsync(userId, cancellationToken).ConfigureAwait(false);

        return lookup.IsFailure
            ? Result.Failure(lookup.Error)
            : await EraseAsync(lookup.Value, actor, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> DueForDeletionAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await _userManager.Users
            .Where(user => user.DeletionScheduledAtUtc != null && user.DeletionScheduledAtUtc <= now)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Deletes an account already loaded (and, for an administrator's request, already checked).</summary>
    public async Task<Result> EraseAsync(ApplicationUser user, AuditSubject? actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Named before it goes: the entry keeps the email the account had.
        var subject = user.ToAuditSubject();

        // Roles, claims, logins, the picture, legal acceptances and tokens go with the account (cascading keys in
        // the Identity schema).
        var deleted = await _userManager.DeleteAsync(user).ConfigureAwait(false);
        if (!deleted.Succeeded)
        {
            return Result.Failure(deleted.ToError(UserErrors.DeleteFailed));
        }

        await _audit.RecordAsync(
                AuditEvent.Succeeded(IdentityAuditActions.UserDeleted, subject) with { Actor = actor },
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
