using System.ComponentModel.DataAnnotations;
using OpenPortal.Identity.Domain.Tokens;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// Personal access tokens: the caller's own (list, create, revoke) while the portal's security setting allows
/// them, and an administrator's view of someone else's (list, revoke).
/// </summary>
public interface IPersonalAccessTokenService
{
    Task<Result<IReadOnlyList<PersonalAccessTokenDto>>> ListMineAsync(CancellationToken cancellationToken);

    /// <summary>Creates a token for the caller and returns it in clear, the only time it is ever shown.</summary>
    Task<Result<CreatedPersonalAccessTokenDto>> CreateAsync(CreatePersonalAccessTokenRequest request, CancellationToken cancellationToken);

    Task<Result> RevokeMineAsync(Guid tokenId, CancellationToken cancellationToken);

    /// <summary>Another account's tokens, for the users page.</summary>
    Task<Result<IReadOnlyList<PersonalAccessTokenDto>>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> RevokeForUserAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken);
}

/// <summary>
/// Checks a token presented to the API. No caller check: it is what establishes the caller. Answers null for an
/// unknown, revoked or expired token, an account that may not sign in, or while tokens are switched off.
/// </summary>
public interface IPersonalAccessTokenValidator
{
    Task<PersonalAccessTokenIdentity?> ValidateAsync(string token, CancellationToken cancellationToken);
}

/// <summary>Who a valid token speaks for, and what it may do.</summary>
public sealed record PersonalAccessTokenIdentity(
    Guid TokenId,
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Scopes);

/// <summary>A token as its owner (or an administrator) sees it: never the token itself.</summary>
/// <param name="Prefix">The beginning of the token, to recognise it.</param>
/// <param name="Status"><c>active</c>, <c>expired</c> or <c>revoked</c>.</param>
public sealed record PersonalAccessTokenDto(
    Guid Id,
    string Name,
    string Prefix,
    IReadOnlyList<string> Scopes,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? LastUsedAtUtc,
    DateTimeOffset? RevokedAtUtc);

/// <summary>A new token, with its secret value: shown once.</summary>
public sealed record CreatedPersonalAccessTokenDto(PersonalAccessTokenDto Token, string Value);

public sealed class CreatePersonalAccessTokenRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.required")]
    [StringLength(PersonalAccessToken.NameMaxLength, ErrorMessage = "validation.name.max")]
    public string Name { get; init; } = string.Empty;

    /// <summary><c>read</c> and/or <c>write</c>.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [PersonalAccessTokenScopes.Read];

    [Range(1, PersonalAccessToken.MaxLifetimeDays, ErrorMessage = "validation.tokenLifetime.range")]
    public int LifetimeDays { get; init; } = 30;
}
