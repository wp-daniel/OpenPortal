using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;

namespace OpenPortal.Identity.Domain.Invitations;

/// <summary>Where an invitation is. Expiry is a matter of time, so it is computed, never stored.</summary>
public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2,
    Expired = 3,
}

/// <summary>
/// An administrator asking someone, by email, to create their account. The invited person follows a link carrying a
/// secret token, chooses a password and is signed in; the address is confirmed by the very fact that they received
/// the link.
/// <para>
/// Only a hash of the token is stored, like a client secret: a copy of the database cannot be used to accept an
/// invitation. Sending it again issues a new token, so an older link stops working.
/// </para>
/// <para>
/// The invitation may promise group and organization memberships. They are other modules' business: this one keeps
/// their ids for the host, which applies them once the account exists.
/// </para>
/// </summary>
public sealed class UserInvitation
{
    public const int EmailMaxLength = 256;
    public const int MessageMaxLength = 500;
    public const int TokenHashMaxLength = 128;
    public const int MaxMemberships = 50;

    // Required by EF Core.
    private UserInvitation()
    {
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    /// <summary>Upper-cased email, for finding an invitation regardless of how the address was typed.</summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    /// <summary>Suggested first name, which the invited person may change.</summary>
    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    /// <summary>A personal message from the inviter, shown on the invitation page and in the email.</summary>
    public string? Message { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public Guid InvitedByUserId { get; private set; }

    /// <summary>Groups the account joins on acceptance (applied by the host).</summary>
    public IReadOnlyList<Guid> GroupIds { get; private set; } = [];

    /// <summary>Organizations the account joins as a member on acceptance (applied by the host).</summary>
    public IReadOnlyList<Guid> OrganizationIds { get; private set; } = [];

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    /// <summary>When the link was last issued (created or sent again).</summary>
    public DateTimeOffset LastSentAtUtc { get; private set; }

    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    /// <summary>The account the invitation became.</summary>
    public Guid? AcceptedUserId { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public InvitationStatus StatusAt(DateTimeOffset now) => this switch
    {
        { RevokedAtUtc: not null } => InvitationStatus.Revoked,
        { AcceptedAtUtc: not null } => InvitationStatus.Accepted,
        _ when now >= ExpiresAtUtc => InvitationStatus.Expired,
        _ => InvitationStatus.Pending,
    };

    public static Result<UserInvitation> Create(
        Guid id,
        string email,
        string? firstName,
        string? lastName,
        string? message,
        string tokenHash,
        Guid invitedByUserId,
        IEnumerable<Guid>? groupIds,
        IEnumerable<Guid>? organizationIds,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, invitedByUserId);

        var trimmedEmail = email.Trim();
        if (trimmedEmail.Length > EmailMaxLength)
        {
            return Result<UserInvitation>.Failure(UserErrors.InvitationEmailInvalid);
        }

        var first = TextRules.Normalise(firstName);
        var last = TextRules.Normalise(lastName);
        if (first?.Length > ApplicationUser.NameMaxLength || last?.Length > ApplicationUser.NameMaxLength)
        {
            return Result<UserInvitation>.Failure(UserErrors.NameTooLong);
        }

        var trimmedMessage = TextRules.Normalise(message);
        if (trimmedMessage?.Length > MessageMaxLength)
        {
            return Result<UserInvitation>.Failure(UserErrors.InvitationMessageTooLong);
        }

        var groups = (groupIds ?? []).Where(groupId => groupId != Guid.Empty).Distinct().ToList();
        var organizations = (organizationIds ?? []).Where(organizationId => organizationId != Guid.Empty).Distinct().ToList();
        if (groups.Count > MaxMemberships || organizations.Count > MaxMemberships)
        {
            return Result<UserInvitation>.Failure(UserErrors.InvitationTooManyMemberships);
        }

        return Result<UserInvitation>.Success(new UserInvitation
        {
            Id = id,
            Email = trimmedEmail,
            NormalizedEmail = trimmedEmail.ToUpperInvariant(),
            FirstName = first,
            LastName = last,
            Message = trimmedMessage,
            TokenHash = tokenHash,
            InvitedByUserId = invitedByUserId,
            GroupIds = groups,
            OrganizationIds = organizations,
            CreatedAtUtc = now,
            LastSentAtUtc = now,
            ExpiresAtUtc = now + lifetime,
        });
    }

    /// <summary>Issues a new link (the old one stops working) and restarts the validity period.</summary>
    public Result Renew(string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (StatusAt(now) is not (InvitationStatus.Pending or InvitationStatus.Expired))
        {
            return Result.Failure(UserErrors.InvitationClosed);
        }

        TokenHash = tokenHash;
        LastSentAtUtc = now;
        ExpiresAtUtc = now + lifetime;

        return Result.Success();
    }

    public Result Revoke(DateTimeOffset now)
    {
        if (StatusAt(now) is not (InvitationStatus.Pending or InvitationStatus.Expired))
        {
            return Result.Failure(UserErrors.InvitationClosed);
        }

        RevokedAtUtc = now;

        return Result.Success();
    }

    /// <summary>Records that the invitation became <paramref name="userId"/>'s account.</summary>
    public Result Accept(Guid userId, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, userId);

        return StatusAt(now) switch
        {
            InvitationStatus.Pending => AcceptNow(userId, now),
            InvitationStatus.Expired => Result.Failure(UserErrors.InvitationExpired),
            _ => Result.Failure(UserErrors.InvitationInvalid),
        };
    }

    private Result AcceptNow(Guid userId, DateTimeOffset now)
    {
        AcceptedAtUtc = now;
        AcceptedUserId = userId;

        return Result.Success();
    }
}
