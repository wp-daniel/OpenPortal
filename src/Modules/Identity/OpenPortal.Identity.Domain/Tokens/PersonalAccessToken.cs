using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Domain.Tokens;

/// <summary>What a personal access token may do.</summary>
public static class PersonalAccessTokenScopes
{
    /// <summary>Read only: GET requests.</summary>
    public const string Read = "read";

    /// <summary>Everything the account may do through the API (reading included), except its sensitive settings.</summary>
    public const string Write = "write";

    public static IReadOnlyList<string> All { get; } = [Read, Write];
}

/// <summary>
/// A secret an account holder creates to call the portal's API from a script or a command-line tool, as
/// themselves. Only a SHA-256 hash is stored; the token is shown once, when it is created. It always expires, can be
/// revoked at any time, and never reaches the account's sensitive settings (password, second factor, tokens, data
/// download, deletion).
/// </summary>
public sealed class PersonalAccessToken
{
    public const string Prefix = "opp_";
    public const int NameMaxLength = 80;
    public const int HashLength = 64;

    /// <summary>How much of the token is kept in clear to recognise it in a list (<c>opp_</c> plus 8 characters).</summary>
    public const int DisplayPrefixLength = 12;

    public const int MaxLifetimeDays = 365;

    /// <summary>The most tokens an account holds at once (revoked and expired ones do not count).</summary>
    public const int MaxActivePerUser = 20;

    /// <summary>The last use is written at most this often, so a busy script does not write on every request.</summary>
    public static readonly TimeSpan LastUseResolution = TimeSpan.FromMinutes(5);

    private PersonalAccessToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The beginning of the token, safe to show, e.g. <c>opp_3fK9aZ1q</c>.</summary>
    public string DisplayPrefix { get; private set; } = string.Empty;

    /// <summary>The token's SHA-256 hash, as lower-case hex.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public IReadOnlyList<string> Scopes { get; private set; } = [];

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public static Result<PersonalAccessToken> Create(
        Guid id,
        Guid userId,
        string? name,
        IReadOnlyCollection<string>? scopes,
        int lifetimeDays,
        string token,
        string tokenHash,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentException.ThrowIfNullOrEmpty(tokenHash);

        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > NameMaxLength)
        {
            return Result<PersonalAccessToken>.Failure(TokenErrors.NameInvalid);
        }

        var normalised = (scopes ?? []).Select(scope => scope?.Trim().ToLowerInvariant()).Distinct().ToList();
        if (normalised.Count == 0 || normalised.Any(scope => scope is null || !PersonalAccessTokenScopes.All.Contains(scope)))
        {
            return Result<PersonalAccessToken>.Failure(TokenErrors.ScopesInvalid);
        }

        if (lifetimeDays is < 1 or > MaxLifetimeDays)
        {
            return Result<PersonalAccessToken>.Failure(TokenErrors.LifetimeInvalid);
        }

        return Result<PersonalAccessToken>.Success(new PersonalAccessToken
        {
            Id = id,
            UserId = userId,
            Name = trimmed,
            DisplayPrefix = token[..Math.Min(DisplayPrefixLength, token.Length)],
            TokenHash = tokenHash,

            // Writing implies reading.
            Scopes = normalised.Contains(PersonalAccessTokenScopes.Write)
                ? [PersonalAccessTokenScopes.Read, PersonalAccessTokenScopes.Write]
                : [PersonalAccessTokenScopes.Read],
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(lifetimeDays),
        });
    }

    public bool IsActiveAt(DateTimeOffset now) => RevokedAtUtc is null && ExpiresAtUtc > now;

    public bool Allows(string scope) => Scopes.Contains(scope, StringComparer.Ordinal);

    /// <summary>Revokes the token. Returns false when it already was.</summary>
    public bool Revoke(DateTimeOffset now)
    {
        if (RevokedAtUtc is not null)
        {
            return false;
        }

        RevokedAtUtc = now;
        return true;
    }

    /// <summary>Records a use; returns whether the stored time moved (at most every <see cref="LastUseResolution"/>).</summary>
    public bool RecordUse(DateTimeOffset now)
    {
        if (LastUsedAtUtc is { } last && now - last < LastUseResolution)
        {
            return false;
        }

        LastUsedAtUtc = now;
        return true;
    }
}

/// <summary>Stable, machine-readable failure codes for personal access tokens.</summary>
public static class TokenErrors
{
    private const string Prefix = "tokens.";

    public static Error NotFound { get; } =
        Error.NotFound(Prefix + "not_found", "The token does not exist.");

    public static Error Disabled { get; } =
        Error.Forbidden(Prefix + "disabled", "Personal access tokens are not enabled on this portal.");

    public static Error NameInvalid { get; } =
        Error.Validation(Prefix + "name_invalid", $"A token needs a name of at most {PersonalAccessToken.NameMaxLength} characters.");

    public static Error ScopesInvalid { get; } =
        Error.Validation(Prefix + "scopes_invalid", "A token's scope is read or write.");

    public static Error LifetimeInvalid { get; } =
        Error.Validation(Prefix + "lifetime_invalid", $"A token lasts between 1 and {PersonalAccessToken.MaxLifetimeDays} days.");

    public static Error TooMany { get; } =
        Error.Conflict(Prefix + "too_many", $"An account holds at most {PersonalAccessToken.MaxActivePerUser} active tokens; revoke one first.");
}
