using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

internal sealed class IdentityUserLookupService : IUserLookupService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClock _clock;

    public IdentityUserLookupService(UserManager<ApplicationUser> userManager, IClock clock)
    {
        _userManager = userManager;
        _clock = clock;
    }

    public async Task<UserReferenceDto?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var users = await FindManyAsync([userId], cancellationToken).ConfigureAwait(false);

        return users.Count == 0 ? null : users[0];
    }

    public async Task<UserReferenceDto?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var user = await _userManager.FindByEmailAsync(email.Trim()).ConfigureAwait(false);

        return user is null ? null : await FindAsync(user.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ListAdministratorIdsAsync(CancellationToken cancellationToken)
    {
        var administrators = await _userManager.GetUsersInRoleAsync(Roles.Administrator).ConfigureAwait(false);

        return administrators.Select(user => user.Id).ToList();
    }

    public async Task<IReadOnlyList<UserReferenceDto>> FindManyAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return [];
        }

        var ids = userIds.Distinct().ToArray();

        var users = await _userManager.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.Email, user.DisplayName, user.LockoutEnd, user.PendingApproval, user.DeletionScheduledAtUtc, user.EmailConfirmed, user.Language })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = _clock.UtcNow;

        return users
            .Select(user => new UserReferenceDto(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                IsActive: !user.PendingApproval && (user.LockoutEnd is null || user.LockoutEnd <= now),
                DeletionScheduled: user.DeletionScheduledAtUtc != null,
                EmailConfirmed: user.EmailConfirmed,
                Language: user.Language))
            .ToList();
    }
}
