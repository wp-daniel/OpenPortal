using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>
/// Resolves accounts for the Identity services, so that the "not authenticated" and "no such user" rules
/// are defined once instead of being restated by every service.
/// </summary>
internal sealed class UserLookup
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;

    public UserLookup(UserManager<ApplicationUser> userManager, ICurrentUser currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async Task<Result<ApplicationUser>> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        return user is null
            ? Result<ApplicationUser>.Failure(UserErrors.UserNotFound)
            : Result<ApplicationUser>.Success(user);
    }

    public Task<Result<ApplicationUser>> FindCurrentAsync(CancellationToken cancellationToken)
    {
        return _currentUser.UserId is Guid userId
            ? FindAsync(userId, cancellationToken)
            : Task.FromResult(Result<ApplicationUser>.Failure(UserErrors.NotAuthenticated));
    }
}

/// <summary>
/// Second authorisation gate for administrative operations.
/// <para>
/// Endpoints already require the administrator policy, but services re-check the caller's role so the
/// rule survives being called from a host, a background job or a future module that forgot the attribute.
/// </para>
/// </summary>
internal static class CallerGuard
{
    public static Result EnsureAdministrator(ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        var isAdministrator = currentUser.IsAuthenticated
            && currentUser.Roles.Contains(Identity.Domain.Users.Roles.Administrator, StringComparer.Ordinal);

        return isAdministrator ? Result.Success() : Result.Failure(UserErrors.Forbidden);
    }
}