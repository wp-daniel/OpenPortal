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
            .Select(user => new { user.Id, user.Email, user.DisplayName, user.LockoutEnd })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = _clock.UtcNow;

        return users
            .Select(user => new UserReferenceDto(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                IsActive: user.LockoutEnd is null || user.LockoutEnd <= now))
            .ToList();
    }
}
