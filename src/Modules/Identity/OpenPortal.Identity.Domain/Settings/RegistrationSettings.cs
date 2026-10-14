using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Domain.Settings;

/// <summary>Whether people may create their own account.</summary>
public enum RegistrationMode
{
    /// <summary>Only administrators create accounts (directly or by invitation).</summary>
    Disabled = 0,

    /// <summary>Anyone allowed by the domain list may register and sign in.</summary>
    Open = 1,

    /// <summary>Anyone allowed by the domain list may register; the account waits for an administrator's approval.</summary>
    Approval = 2,
}

/// <summary>
/// How accounts come to exist besides an administrator creating them: self-registration and invitations. A single
/// row, edited by administrators from the portal; until one is saved the defaults below apply (registration off).
/// </summary>
public sealed class RegistrationSettings
{
    public const int SingletonId = 1;
    public const int MaxDomains = 20;
    public const int DomainMaxLength = 100;
    public const int MinInvitationDays = 1;
    public const int MaxInvitationDays = 90;
    public const int DefaultInvitationDays = 7;

    /// <summary>Required by Entity Framework Core materialisation, and the defaults when no row exists.</summary>
    private RegistrationSettings()
    {
    }

    public int Id { get; private set; } = SingletonId;

    public RegistrationMode Mode { get; private set; }

    /// <summary>
    /// Email domains that may register (<c>example.com</c>); empty allows any. Matched exactly, so a domain does not
    /// let its subdomains in.
    /// </summary>
    public IReadOnlyList<string> AllowedDomains { get; private set; } = [];

    /// <summary>
    /// Whether a self-registered account must confirm its address (a link sent by email) before it signs in. Without
    /// it, anyone can register an address that is not theirs.
    /// </summary>
    public bool RequireEmailConfirmation { get; private set; } = true;

    /// <summary>How long an invitation link stays valid.</summary>
    public int InvitationLifetimeDays { get; private set; } = DefaultInvitationDays;

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public bool IsOpen => Mode != RegistrationMode.Disabled;

    public static RegistrationSettings Defaults() => new();

    /// <summary>Applies new values; the result says whether anything changed.</summary>
    public Result<bool> Update(
        RegistrationMode mode,
        IEnumerable<string>? allowedDomains,
        bool requireEmailConfirmation,
        int invitationLifetimeDays,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(mode))
        {
            return Result<bool>.Failure(UserErrors.RegistrationModeInvalid);
        }

        if (invitationLifetimeDays is < MinInvitationDays or > MaxInvitationDays)
        {
            return Result<bool>.Failure(UserErrors.InvitationLifetimeInvalid);
        }

        var domains = new List<string>();

        foreach (var value in allowedDomains ?? [])
        {
            var domain = NormaliseDomain(value);
            if (domain is null)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return Result<bool>.Failure(UserErrors.RegistrationDomainInvalid);
                }

                continue;
            }

            if (!domains.Contains(domain, StringComparer.Ordinal))
            {
                domains.Add(domain);
            }
        }

        if (domains.Count > MaxDomains)
        {
            return Result<bool>.Failure(UserErrors.RegistrationDomainInvalid);
        }

        domains.Sort(StringComparer.Ordinal);

        if (Mode == mode
            && AllowedDomains.SequenceEqual(domains, StringComparer.Ordinal)
            && RequireEmailConfirmation == requireEmailConfirmation
            && InvitationLifetimeDays == invitationLifetimeDays)
        {
            return Result<bool>.Success(false);
        }

        Mode = mode;
        AllowedDomains = domains;
        RequireEmailConfirmation = requireEmailConfirmation;
        InvitationLifetimeDays = invitationLifetimeDays;
        UpdatedAtUtc = now;

        return Result<bool>.Success(true);
    }

    /// <summary>Whether <paramref name="email"/> may register under the domain list.</summary>
    public bool AllowsEmail(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (AllowedDomains.Count == 0)
        {
            return true;
        }

        var at = email.LastIndexOf('@');

        return at > 0 && AllowedDomains.Contains(email[(at + 1)..].Trim().ToLowerInvariant(), StringComparer.Ordinal);
    }

    /// <summary>A host name in lower case (<c>example.com</c>), or null when <paramref name="value"/> is not one.</summary>
    private static string? NormaliseDomain(string? value)
    {
        var domain = value?.Trim().TrimStart('@').ToLowerInvariant();

        if (string.IsNullOrEmpty(domain) || domain.Length > DomainMaxLength || Uri.CheckHostName(domain) != UriHostNameType.Dns)
        {
            return null;
        }

        return domain.Contains('.', StringComparison.Ordinal) ? domain : null;
    }
}
