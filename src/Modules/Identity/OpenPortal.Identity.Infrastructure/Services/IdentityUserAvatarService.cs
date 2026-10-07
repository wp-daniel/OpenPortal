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
internal sealed class IdentityUserAvatarService : IUserAvatarService
{
    private readonly IdentityDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;
    private readonly UserLookup _userLookup;
    private readonly AdministrationGuard _guard;
    private readonly IClock _clock;

    public IdentityUserAvatarService(
        IdentityDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ICurrentUser currentUser,
        UserLookup userLookup,
        AdministrationGuard guard,
        IClock clock)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _currentUser = currentUser;
        _userLookup = userLookup;
        _guard = guard;
        _clock = clock;
    }

    public async Task<Result<AvatarImageDto>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result<AvatarImageDto>.Failure(UserErrors.NotAuthenticated);
        }

        var avatar = await _dbContext.UserAvatars
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        return avatar is null
            ? Result<AvatarImageDto>.Failure(UserErrors.AvatarNotFound)
            : Result<AvatarImageDto>.Success(new AvatarImageDto(avatar.Content, avatar.ContentType, avatar.UpdatedAtUtc));
    }

    public async Task<Result> SetOwnAsync(byte[] content, CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

        return lookup.IsFailure
            ? Result.Failure(lookup.Error)
            : await SetAsync(lookup.Value, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> RemoveOwnAsync(CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

        return lookup.IsFailure
            ? Result.Failure(lookup.Error)
            : await RemoveAsync(lookup.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> SetForUserAsync(Guid userId, byte[] content, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var target = await FindChangeableAsync(userId, cancellationToken).ConfigureAwait(false);

        return target.IsFailure
            ? Result.Failure(target.Error)
            : await SetAsync(target.Value, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> RemoveForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var target = await FindChangeableAsync(userId, cancellationToken).ConfigureAwait(false);

        return target.IsFailure
            ? Result.Failure(target.Error)
            : await RemoveAsync(target.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ApplicationUser>> FindChangeableAsync(Guid userId, CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return lookup;
        }

        var allowed = await _guard.EnsureMayChangeAsync(lookup.Value).ConfigureAwait(false);

        return allowed.IsFailure ? Result<ApplicationUser>.Failure(allowed.Error) : lookup;
    }

    private async Task<Result> SetAsync(ApplicationUser user, byte[] content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var now = _clock.UtcNow;
        var existing = await _dbContext.UserAvatars
            .FirstOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var created = UserAvatar.Create(user.Id, content, now);
            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            _dbContext.UserAvatars.Add(created.Value);
        }
        else
        {
            var replaced = existing.Replace(content, now);
            if (replaced.IsFailure)
            {
                return replaced;
            }
        }

        return await SaveStampAsync(user, now, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> RemoveAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        // Removing a picture that is not there is not an error: the caller's intent ("no picture") holds.
        await _dbContext.UserAvatars
            .Where(candidate => candidate.UserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return user.AvatarUpdatedAtUtc is null
            ? Result.Success()
            : await SaveStampAsync(user, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves the user's stamp through <see cref="UserManager{TUser}"/>, which also persists any pending avatar
    /// row: both share the scoped <see cref="IdentityDbContext"/>, so the image and its stamp commit together.
    /// </summary>
    private async Task<Result> SaveStampAsync(ApplicationUser user, DateTimeOffset? stamp, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        user.MarkAvatarChanged(stamp);
        var persisted = await _userManager.UpdateAsync(user).ConfigureAwait(false);

        return persisted.Succeeded
            ? Result.Success()
            : Result.Failure(persisted.ToError(UserErrors.SaveFailed));
    }
}
