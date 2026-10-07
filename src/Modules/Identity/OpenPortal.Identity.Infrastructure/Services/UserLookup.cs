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
/// Endpoints already check the caller's pages, but services re-check through the host's
/// <see cref="IUserAdministrationAuthorization"/> so the rule survives being called from a host, a background
/// job or a future module that forgot the attribute. On top of that, this module's own rule: only an
/// administrator may grant the administrator role or change an administrator's account, so a page delegated
/// to a group can never be used to climb above it.
/// </para>
/// </summary>
internal sealed class AdministrationGuard
{
    private readonly IUserAdministrationAuthorization _authorization;
    private readonly ICurrentUser _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdministrationGuard(
        IUserAdministrationAuthorization authorization,
        ICurrentUser currentUser,
        UserManager<ApplicationUser> userManager)
    {
        _authorization = authorization;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    public bool CallerIsAdministrator =>
        _currentUser.IsAuthenticated
        && _currentUser.Roles.Contains(Roles.Administrator, StringComparer.Ordinal);

    public Task<Result> EnsureCanAsync(UserAdministrationOperation operation, CancellationToken cancellationToken) =>
        _authorization.EnsureCanAsync(operation, cancellationToken);

    /// <summary>Refuses a caller who is not an administrator when <paramref name="target"/> is one.</summary>
    public async Task<Result> EnsureMayChangeAsync(ApplicationUser target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (CallerIsAdministrator)
        {
            return Result.Success();
        }

        var targetIsAdministrator = await _userManager.IsInRoleAsync(target, Roles.Administrator).ConfigureAwait(false);

        return targetIsAdministrator ? Result.Failure(UserErrors.AdministratorRequired) : Result.Success();
    }

    /// <summary>Refuses a caller who is not an administrator when <paramref name="roles"/> include it.</summary>
    public Result EnsureMayAssign(IEnumerable<string> roles) =>
        CallerIsAdministrator || !roles.Contains(Roles.Administrator, StringComparer.Ordinal)
            ? Result.Success()
            : Result.Failure(UserErrors.AdministratorRequired);
}
