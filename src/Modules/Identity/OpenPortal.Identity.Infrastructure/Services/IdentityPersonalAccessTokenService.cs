using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Domain.Tokens;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>Reads and revokes personal access tokens for the other Identity services.</summary>
internal sealed class PersonalAccessTokenStore
{
    private readonly IdentityDbContext _db;
    private readonly IClock _clock;

    public PersonalAccessTokenStore(IdentityDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>The SHA-256 of a token, as stored.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>A new token: the prefix and 32 random bytes, base64url.</summary>
    public static string NewToken() => PersonalAccessToken.Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>Revokes every token of the account still active and returns how many.</summary>
    public async Task<int> RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var tokens = await _db.PersonalAccessTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var revoked = tokens.Count(token => token.Revoke(now));
        if (revoked > 0)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return revoked;
    }
}

/// <inheritdoc cref="IPersonalAccessTokenService" />
internal sealed class IdentityPersonalAccessTokenService : IPersonalAccessTokenService, IPersonalAccessTokenValidator
{
    private readonly IdentityDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly SecuritySettingsStore _settings;
    private readonly UserLookup _userLookup;
    private readonly AdministrationGuard _guard;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;

    public IdentityPersonalAccessTokenService(
        IdentityDbContext db,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        SecuritySettingsStore settings,
        UserLookup userLookup,
        AdministrationGuard guard,
        ICurrentUser currentUser,
        IClock clock,
        IAuditTrail audit)
    {
        _db = db;
        _userManager = userManager;
        _signInManager = signInManager;
        _settings = settings;
        _userLookup = userLookup;
        _guard = guard;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<PersonalAccessTokenDto>>> ListMineAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Result<IReadOnlyList<PersonalAccessTokenDto>>.Failure(UserErrors.NotAuthenticated);
        }

        return Result<IReadOnlyList<PersonalAccessTokenDto>>.Success(await ListAsync(userId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<CreatedPersonalAccessTokenDto>> CreateAsync(
        CreatePersonalAccessTokenRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_currentUser.UserId is not Guid userId)
        {
            return Result<CreatedPersonalAccessTokenDto>.Failure(UserErrors.NotAuthenticated);
        }

        if (!(await _settings.ReadAsync(cancellationToken).ConfigureAwait(false)).PersonalAccessTokensEnabled)
        {
            return Result<CreatedPersonalAccessTokenDto>.Failure(TokenErrors.Disabled);
        }

        var now = _clock.UtcNow;
        var active = await _db.PersonalAccessTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .Select(token => token.ExpiresAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (active.Count(expires => expires > now) >= PersonalAccessToken.MaxActivePerUser)
        {
            return Result<CreatedPersonalAccessTokenDto>.Failure(TokenErrors.TooMany);
        }

        var value = PersonalAccessTokenStore.NewToken();
        var created = PersonalAccessToken.Create(
            Guid.NewGuid(),
            userId,
            request.Name,
            request.Scopes,
            request.LifetimeDays,
            value,
            PersonalAccessTokenStore.Hash(value),
            now);

        if (created.IsFailure)
        {
            return Result<CreatedPersonalAccessTokenDto>.Failure(created.Error);
        }

        _db.PersonalAccessTokens.Add(created.Value);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    IdentityAuditActions.TokenCreated,
                    Subject(created.Value),
                    new Dictionary<string, string?>
                    {
                        ["scopes"] = string.Join(", ", created.Value.Scopes),
                        ["expires"] = created.Value.ExpiresAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<CreatedPersonalAccessTokenDto>.Success(new CreatedPersonalAccessTokenDto(ToDto(created.Value, now), value));
    }

    public async Task<Result> RevokeMineAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Result.Failure(UserErrors.NotAuthenticated);
        }

        return await RevokeAsync(userId, tokenId, IdentityAuditActions.TokenRevoked, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<PersonalAccessTokenDto>>> ListForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<IReadOnlyList<PersonalAccessTokenDto>>.Failure(guard.Error);
        }

        return Result<IReadOnlyList<PersonalAccessTokenDto>>.Success(await ListAsync(userId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> RevokeForUserAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
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

        return await RevokeAsync(userId, tokenId, IdentityAuditActions.UserTokenRevoked, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PersonalAccessTokenIdentity?> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token) || !token.StartsWith(PersonalAccessToken.Prefix, StringComparison.Ordinal) || token.Length > 128)
        {
            return null;
        }

        if (!(await _settings.ReadAsync(cancellationToken).ConfigureAwait(false)).PersonalAccessTokensEnabled)
        {
            return null;
        }

        var hash = PersonalAccessTokenStore.Hash(token);
        var stored = await _db.PersonalAccessTokens
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        var now = _clock.UtcNow;
        if (stored is null || !stored.IsActiveAt(now))
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(stored.UserId.ToString()).ConfigureAwait(false);

        // The account must still be one that may sign in: not locked out, approved, confirmed, not leaving.
        if (user is null
            || user.DeletionScheduledAtUtc is not null
            || await _userManager.IsLockedOutAsync(user).ConfigureAwait(false)
            || !await _signInManager.CanSignInAsync(user).ConfigureAwait(false))
        {
            return null;
        }

        if (stored.RecordUse(now))
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);

        return new PersonalAccessTokenIdentity(
            stored.Id,
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            roles.ToList(),
            stored.Scopes);
    }

    private async Task<Result> RevokeAsync(Guid userId, Guid tokenId, string action, CancellationToken cancellationToken)
    {
        var token = await _db.PersonalAccessTokens
            .FirstOrDefaultAsync(candidate => candidate.Id == tokenId && candidate.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (token is null)
        {
            return Result.Failure(TokenErrors.NotFound);
        }

        if (token.Revoke(_clock.UtcNow))
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _audit.RecordAsync(AuditEvent.Succeeded(action, Subject(token)), cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    private async Task<IReadOnlyList<PersonalAccessTokenDto>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await _db.PersonalAccessTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = _clock.UtcNow;

        return tokens
            .OrderByDescending(token => token.CreatedAtUtc)
            .Select(token => ToDto(token, now))
            .ToList();
    }

    private static AuditSubject Subject(PersonalAccessToken token) =>
        new(AuditSubjectTypes.PersonalAccessToken, token.Id.ToString(), $"{token.Name} ({token.DisplayPrefix}…)");

    private static PersonalAccessTokenDto ToDto(PersonalAccessToken token, DateTimeOffset now) => new(
        token.Id,
        token.Name,
        token.DisplayPrefix,
        token.Scopes,
        token.RevokedAtUtc is not null ? "revoked" : token.ExpiresAtUtc <= now ? "expired" : "active",
        token.CreatedAtUtc,
        token.ExpiresAtUtc,
        token.LastUsedAtUtc,
        token.RevokedAtUtc);
}
