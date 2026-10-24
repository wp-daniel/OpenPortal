namespace OpenPortal.Identity.Application.Contracts;

/// <summary>
/// The public projection of a session, safe to hand to a browser.
/// <para>
/// Only the fields the UI renders are present. Password hashes, security stamps, concurrency stamps,
/// lockout counters and confirmation tokens are deliberately absent and must never be added: a client has
/// no use for them and they are an unnecessary disclosure risk.
/// </para>
/// </summary>
/// <param name="Pages">Keys of the portal pages the user may open (every page for an administrator).</param>
/// <param name="PendingLegalDocuments">
/// Versions of the privacy notice or the terms of use in force that the account must still accept; the client
/// asks for them before anything else.
/// </param>
/// <param name="DeletionScheduledAtUtc">When the account will be deleted, if its owner asked for it; they can still cancel.</param>
/// <param name="Impersonator">The administrator acting as this account for support, in a support session.</param>
/// <param name="TwoFactorSetupRequired">
/// The portal requires a second factor of this account and it has none yet: the client offers only the setup.
/// </param>
/// <param name="AuthenticationMethods">How this session was established (<c>pwd</c>, <c>mfa</c>, <c>hwk</c>…).</param>
public sealed record SessionUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles,
    string? Language,
    DateTimeOffset? AvatarUpdatedAtUtc,
    IReadOnlyList<string> Pages,
    IReadOnlyList<PendingLegalDocumentDto> PendingLegalDocuments,
    DateTimeOffset? DeletionScheduledAtUtc,
    SessionImpersonatorDto? Impersonator,
    bool TwoFactorSetupRequired = false,
    IReadOnlyList<string>? AuthenticationMethods = null);

/// <summary>The administrator behind a support session.</summary>
public sealed record SessionImpersonatorDto(Guid Id, string? Email, DateTimeOffset StartedAtUtc, DateTimeOffset EndsAtUtc);

/// <summary>Current session state as observed by the client.</summary>
/// <param name="IsAuthenticated">Whether a valid session cookie was presented.</param>
/// <param name="User">The caller, or <see langword="null"/> when anonymous.</param>
/// <param name="PasswordPolicy">
/// The policy this deployment enforces. Sent to anonymous callers too, because the sign-in and
/// change-password forms need it before anyone is signed in. It discloses configuration, not data.
/// </param>
/// <param name="Registration">Whether the sign-in page offers to create an account.</param>
/// <param name="Legal">Which legal documents are published, so the client can link them.</param>
/// <param name="Features">Optional features the portal offers.</param>
public sealed record SessionDto(
    bool IsAuthenticated,
    SessionUserDto? User,
    PasswordPolicyDto PasswordPolicy,
    RegistrationInfoDto Registration,
    LegalInfoDto Legal,
    SessionFeaturesDto Features)
{
    /// <summary>
    /// The anonymous session. Takes the policy as an argument because there is no default that is honest:
    /// the policy is deployment configuration, so a fabricated one could only ever disagree with the server.
    /// Registration, by contrast, has an honest default when it cannot be read: closed.
    /// </summary>
    public static SessionDto AnonymousFor(
        PasswordPolicyDto passwordPolicy,
        RegistrationInfoDto? registration = null,
        LegalInfoDto? legal = null) =>
        new(false, null, passwordPolicy, registration ?? RegistrationInfoDto.Closed, legal ?? LegalInfoDto.None, SessionFeaturesDto.None);
}

/// <summary>
/// The password policy the server actually enforces. Published so that client-side forms can validate
/// against the real rules instead of a hardcoded copy that drifts.
/// </summary>
public sealed record PasswordPolicyDto(
    int RequiredLength,
    int RequiredUniqueChars,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireDigit,
    bool RequireNonAlphanumeric);

/// <summary>
/// The antiforgery token for the current browser session.
/// <para>
/// The token is a per-session nonce, not a credential: it proves only that the request came from a page this
/// origin served, so it is safe to hand to script. The session cookie that gives it meaning stays HttpOnly.
/// </para>
/// </summary>
/// <param name="RequestToken">Value to echo in the <c>X-XSRF-TOKEN</c> request header.</param>
/// <param name="HeaderName">Name of the header the token must be sent in.</param>
public sealed record AntiforgeryTokenDto(string RequestToken, string HeaderName);