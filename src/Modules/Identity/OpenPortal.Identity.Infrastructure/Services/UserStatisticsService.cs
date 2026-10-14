using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <inheritdoc />
internal sealed class UserStatisticsService : IUserStatisticsService
{
    private readonly IdentityDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AdministrationGuard _guard;
    private readonly IClock _clock;

    public UserStatisticsService(
        IdentityDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        AdministrationGuard guard,
        IClock clock)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _guard = guard;
        _clock = clock;
    }

    public async Task<Result<UserStatisticsDto>> GetAsync(DateTimeOffset periodStart, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ViewStatistics, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<UserStatisticsDto>.Failure(guard.Error);
        }

        var now = _clock.UtcNow;
        var users = _userManager.Users.AsNoTracking();

        var administrators = await _dbContext.UserRoles
            .Where(userRole => _dbContext.Roles.Any(role => role.Id == userRole.RoleId && role.Name == Roles.Administrator))
            .Select(userRole => userRole.UserId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<UserStatisticsDto>.Success(new UserStatisticsDto(
            Total: await users.CountAsync(cancellationToken).ConfigureAwait(false),
            Administrators: administrators,
            Locked: await users
                .CountAsync(user => user.LockoutEnabled && user.LockoutEnd != null && user.LockoutEnd > now, cancellationToken)
                .ConfigureAwait(false),
            PendingApproval: await users.CountAsync(user => user.PendingApproval, cancellationToken).ConfigureAwait(false),
            TwoFactorEnabled: await users.CountAsync(user => user.TwoFactorEnabled, cancellationToken).ConfigureAwait(false),
            NewInPeriod: await users.CountAsync(user => user.CreatedAtUtc >= periodStart, cancellationToken).ConfigureAwait(false),
            SignedInInPeriod: await users
                .CountAsync(user => user.LastSignInAtUtc != null && user.LastSignInAtUtc >= periodStart, cancellationToken)
                .ConfigureAwait(false),
            NeverSignedIn: await users.CountAsync(user => user.LastSignInAtUtc == null, cancellationToken).ConfigureAwait(false),
            PendingInvitations: await _dbContext.Invitations
                .CountAsync(
                    invitation => invitation.RevokedAtUtc == null && invitation.AcceptedAtUtc == null && invitation.ExpiresAtUtc > now,
                    cancellationToken)
                .ConfigureAwait(false)));
    }
}
