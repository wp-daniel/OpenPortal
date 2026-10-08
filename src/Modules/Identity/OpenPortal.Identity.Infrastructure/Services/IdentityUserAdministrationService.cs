using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <inheritdoc />
internal sealed class IdentityUserAdministrationService : IUserAdministrationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IdentityDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly UserLookup _userLookup;
    private readonly AdministrationGuard _guard;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;

    public IdentityUserAdministrationService(
        UserManager<ApplicationUser> userManager,
        IdentityDbContext dbContext,
        ICurrentUser currentUser,
        UserLookup userLookup,
        AdministrationGuard guard,
        IClock clock,
        IAuditTrail audit)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _currentUser = currentUser;
        _userLookup = userLookup;
        _guard = guard;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<PagedResult<UserSummaryDto>>> ListUsersAsync(
        UserListQuery query,
        CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ListUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<PagedResult<UserSummaryDto>>.Failure(guard.Error);
        }

        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, UserListQuery.MaxPageSize);

        var usersQuery = _userManager.Users.AsNoTracking();

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // Compare against upper-cased values rather than using a LIKE pattern: SQLite's LIKE is
            // case-insensitive for ASCII while PostgreSQL's is case-sensitive, so an explicit upper-case
            // comparison keeps search behaviour identical on every provider.
            var term = search.ToUpperInvariant();
            usersQuery = usersQuery.Where(user =>
                (user.NormalizedEmail != null && user.NormalizedEmail.Contains(term))
                || user.DisplayName.ToUpper().Contains(term)
                || (user.PhoneNumber != null && user.PhoneNumber.Contains(search))
                || (user.Company != null && user.Company.ToUpper().Contains(term)));
        }

        if (query.Administrator is bool administrator)
        {
            usersQuery = usersQuery.Where(user => _dbContext.UserRoles.Any(userRole =>
                userRole.UserId == user.Id
                && _dbContext.Roles.Any(r => r.Id == userRole.RoleId && r.Name == Roles.Administrator)) == administrator);
        }

        var now = _clock.UtcNow;
        var inactiveSince = now.AddDays(-UserStatusFilter.InactiveAfterDays);
        usersQuery = query.Status?.Trim().ToLowerInvariant() switch
        {
            UserStatusFilter.Locked => usersQuery.Where(user =>
                user.LockoutEnabled && user.LockoutEnd != null && user.LockoutEnd > now),
            UserStatusFilter.Unconfirmed => usersQuery.Where(user => !user.EmailConfirmed),
            UserStatusFilter.Inactive => usersQuery.Where(user =>
                user.LastSignInAtUtc == null || user.LastSignInAtUtc < inactiveSince),
            UserStatusFilter.Active => usersQuery.Where(user =>
                !(user.LockoutEnabled && user.LockoutEnd != null && user.LockoutEnd > now)),
            _ => usersQuery,
        };

        var totalCount = await usersQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var users = await usersQuery
            .OrderBy(user => user.NormalizedEmail)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var userIds = users.Select(user => user.Id).ToArray();
        var rolesByUser = await LoadRolesAsync(userIds, cancellationToken).ConfigureAwait(false);

        var items = users
            .Select(user => ToSummary(user, rolesByUser.GetValueOrDefault(user.Id) ?? [], now))
            .ToArray();

        return Result<PagedResult<UserSummaryDto>>.Success(
            new PagedResult<UserSummaryDto>(items, page, pageSize, totalCount));
    }

    public async Task<Result<UserSummaryDto>> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<UserSummaryDto>.Failure(guard.Error);
        }

        var lookup = await _userLookup.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<UserSummaryDto>.Failure(lookup.Error);
        }

        var roles = await LoadRolesAsync([userId], cancellationToken).ConfigureAwait(false);

        return Result<UserSummaryDto>.Success(ToSummary(lookup.Value, roles.GetValueOrDefault(userId) ?? [], _clock.UtcNow));
    }

    public async Task<Result<UserSummaryDto>> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<UserSummaryDto>.Failure(guard.Error);
        }

        ArgumentNullException.ThrowIfNull(request);

        var roles = RolesFor(request.IsAdministrator);
        var assignable = _guard.EnsureMayAssign(roles);
        if (assignable.IsFailure)
        {
            return Result<UserSummaryDto>.Failure(assignable.Error);
        }

        // Validate the details before touching the store so bad input is a 400, not a half-made account.
        var account = ApplicationUser.Create(Guid.NewGuid(), request.Email, request.ToDetails(), _clock.UtcNow);
        if (account.IsFailure)
        {
            return Result<UserSummaryDto>.Failure(account.Error);
        }

        var user = account.Value;
        var created = await _userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return Result<UserSummaryDto>.Failure(created.ToError(UserErrors.SaveFailed));
        }

        var assigned = await _userManager.AddToRolesAsync(user, roles).ConfigureAwait(false);
        if (!assigned.Succeeded)
        {
            // Never leave a half-provisioned account behind.
            await _userManager.DeleteAsync(user).ConfigureAwait(false);

            return Result<UserSummaryDto>.Failure(assigned.ToError(UserErrors.RoleAssignmentFailed));
        }

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    IdentityAuditActions.UserCreated,
                    user.ToAuditSubject(),
                    new Dictionary<string, string?> { ["administrator"] = request.IsAdministrator ? "true" : "false" }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<UserSummaryDto>.Success(ToSummary(user, roles, _clock.UtcNow));
    }

    public async Task<Result<UserSummaryDto>> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<UserSummaryDto>.Failure(guard.Error);
        }

        ArgumentNullException.ThrowIfNull(request);

        var lookup = await _userLookup.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<UserSummaryDto>.Failure(lookup.Error);
        }

        var user = lookup.Value;
        var effectiveRoles = RolesFor(request.IsAdministrator);

        var changeable = await _guard.EnsureMayChangeAsync(user).ConfigureAwait(false);
        var assignable = changeable.IsSuccess ? _guard.EnsureMayAssign(effectiveRoles) : changeable;
        if (assignable.IsFailure)
        {
            return Result<UserSummaryDto>.Failure(assignable.Error);
        }

        var currentRoles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);

        if (_currentUser.UserId == userId && !SameMembership(currentRoles, effectiveRoles))
        {
            // An administrator editing their own roles could lock themselves out, and a role they grant
            // themselves is a privilege change that should go through a separate, audited path.
            return Result<UserSummaryDto>.Failure(UserErrors.CannotModifyOwnRoles);
        }

        var rename = user.UpdateDetails(request.ToDetails(), _clock.UtcNow);
        if (rename.IsFailure)
        {
            return Result<UserSummaryDto>.Failure(rename.Error);
        }

        var persisted = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!persisted.Succeeded)
        {
            return Result<UserSummaryDto>.Failure(persisted.ToError(UserErrors.SaveFailed));
        }

        var removed = currentRoles.Except(effectiveRoles, StringComparer.Ordinal).ToArray();
        if (removed.Length > 0)
        {
            var removal = await _userManager.RemoveFromRolesAsync(user, removed).ConfigureAwait(false);
            if (!removal.Succeeded)
            {
                return Result<UserSummaryDto>.Failure(removal.ToError(UserErrors.RoleUpdateFailed));
            }
        }

        var added = effectiveRoles.Except(currentRoles, StringComparer.Ordinal).ToArray();
        if (added.Length > 0)
        {
            var addition = await _userManager.AddToRolesAsync(user, added).ConfigureAwait(false);
            if (!addition.Succeeded)
            {
                return Result<UserSummaryDto>.Failure(addition.ToError(UserErrors.RoleUpdateFailed));
            }
        }

        await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.UserUpdated, user.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        // A change of privilege is its own entry, so it stands out in the log rather than hiding in an edit.
        var wasAdministrator = currentRoles.Contains(Roles.Administrator, StringComparer.Ordinal);
        if (wasAdministrator != request.IsAdministrator)
        {
            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        request.IsAdministrator ? IdentityAuditActions.AdministratorGranted : IdentityAuditActions.AdministratorRevoked,
                        user.ToAuditSubject()),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<UserSummaryDto>.Success(ToSummary(user, effectiveRoles, _clock.UtcNow));
    }

    public async Task<Result> ResetPasswordAsync(
        Guid userId,
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result.Failure(guard.Error);
        }

        ArgumentNullException.ThrowIfNull(request);

        var lookup = await _userLookup.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result.Failure(lookup.Error);
        }

        var changeable = await _guard.EnsureMayChangeAsync(lookup.Value).ConfigureAwait(false);
        if (changeable.IsFailure)
        {
            return changeable;
        }

        // The reset token is generated server-side and immediately consumed: this is an administrative
        // "set a new password" action, not a self-service mail-based recovery flow. Resetting also rotates
        // the security stamp, which terminates the target's existing sessions.
        var token = await _userManager
            .GeneratePasswordResetTokenAsync(lookup.Value)
            .ConfigureAwait(false);

        var reset = await _userManager
            .ResetPasswordAsync(lookup.Value, token, request.NewPassword)
            .ConfigureAwait(false);

        if (!reset.Succeeded)
        {
            return Result.Failure(reset.ToError(UserErrors.SaveFailed));
        }

        await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.PasswordReset, lookup.Value.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        // Deleting yourself would end your own session mid-request and could remove the last administrator.
        if (_currentUser.UserId == userId)
        {
            return Result.Failure(UserErrors.CannotDeleteSelf);
        }

        var lookup = await _userLookup.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result.Failure(lookup.Error);
        }

        var changeable = await _guard.EnsureMayChangeAsync(lookup.Value).ConfigureAwait(false);
        if (changeable.IsFailure)
        {
            return changeable;
        }

        // Named before it goes: the entry keeps the email the account had.
        var subject = lookup.Value.ToAuditSubject();

        // Roles, claims, logins and the picture go with the account (cascading keys in the Identity schema).
        var deleted = await _userManager.DeleteAsync(lookup.Value).ConfigureAwait(false);
        if (!deleted.Succeeded)
        {
            return Result.Failure(deleted.ToError(UserErrors.DeleteFailed));
        }

        await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.UserDeleted, subject), cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// The roles behind the administrator flag: every account holds <see cref="Roles.User"/>, and an
    /// administrator also holds <see cref="Roles.Administrator"/>.
    /// </summary>
    private static IReadOnlyList<string> RolesFor(bool isAdministrator) =>
        isAdministrator ? [Roles.User, Roles.Administrator] : [Roles.User];

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadRolesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        // IdentityUserRole<TKey> exposes no Role navigation property, so the role names come from an explicit
        // join. Two queries instead of a navigation keeps the projection translatable on every provider.
        var assignments = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Select(userRole => new { userRole.UserId, userRole.RoleId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (assignments.Count == 0)
        {
            return [];
        }

        var roleNames = await _dbContext.Roles
            .AsNoTracking()
            .Where(role => assignments.Select(assignment => assignment.RoleId).Contains(role.Id))
            .Select(role => new { role.Id, role.Name })
            .ToDictionaryAsync(role => role.Id, role => role.Name, cancellationToken)
            .ConfigureAwait(false);

        return assignments
            .GroupBy(assignment => assignment.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(assignment => roleNames.GetValueOrDefault(assignment.RoleId))
                    .Where(name => name is not null)
                    .Select(name => name!)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
    }

    private static bool SameMembership(IEnumerable<string> left, IEnumerable<string> right)
    {
        var leftSet = left.ToHashSet(StringComparer.Ordinal);

        return leftSet.SetEquals(right);
    }

    private static UserSummaryDto ToSummary(ApplicationUser user, IReadOnlyList<string> roles, DateTimeOffset now) => new(
        Id: user.Id,
        Email: user.Email ?? string.Empty,
        DisplayName: user.DisplayName,
        EmailConfirmed: user.EmailConfirmed,
        IsLockedOut: user.LockoutEnabled && user.LockoutEnd is not null && user.LockoutEnd > now,
        LockoutEndUtc: user.LockoutEnd,
        CreatedAtUtc: user.CreatedAtUtc,
        IsAdministrator: roles.Contains(Roles.Administrator, StringComparer.Ordinal),
        FirstName: user.FirstName,
        LastName: user.LastName,
        PhoneNumber: user.PhoneNumber,
        JobTitle: user.JobTitle,
        Company: user.Company,
        Department: user.Department,
        AddressLine: user.AddressLine,
        City: user.City,
        PostalCode: user.PostalCode,
        Country: user.Country,
        AvatarUpdatedAtUtc: user.AvatarUpdatedAtUtc,
        LastSignInAtUtc: user.LastSignInAtUtc);
}