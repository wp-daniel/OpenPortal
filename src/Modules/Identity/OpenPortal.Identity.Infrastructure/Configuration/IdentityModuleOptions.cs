namespace OpenPortal.Identity.Infrastructure.Configuration;

/// <summary>
/// Bound from the <c>Identity</c> configuration section. Every value has a safe default, so an empty
/// section still produces a correctly hardened identity stack.
/// </summary>
public sealed class IdentityModuleOptions
{
    public const string SectionName = "Identity";

    public PasswordPolicy Password { get; set; } = new();

    public LockoutPolicy Lockout { get; set; } = new();

    public SignInPolicy SignIn { get; set; } = new();

    public CookiePolicy Cookie { get; set; } = new();

    public PasskeyPolicy Passkeys { get; set; } = new();

    /// <summary>
    /// How long an administrator's support session (acting as another account) lasts before it ends by itself and
    /// they must sign in again.
    /// </summary>
    public int ImpersonationMinutes { get; set; } = 60;

    public sealed class PasswordPolicy
    {
        public int RequiredLength { get; set; } = 12;

        public int RequiredUniqueChars { get; set; } = 4;

        public bool RequireLowercase { get; set; } = true;

        public bool RequireUppercase { get; set; } = true;

        public bool RequireDigit { get; set; } = true;

        public bool RequireNonAlphanumeric { get; set; } = true;
    }

    public sealed class LockoutPolicy
    {
        /// <summary>When false, lockout is disabled for every account.</summary>
        public bool Enabled { get; set; } = true;

        public int MaxFailedAccessAttempts { get; set; } = 5;

        public int DefaultLockoutMinutes { get; set; } = 15;
    }

    public sealed class SignInPolicy
    {
        /// <summary>
        /// Require a confirmed email address before sign-in. Off in v1 because no confirmation flow exists
        /// yet; flip it on together with the email-confirmation feature.
        /// </summary>
        public bool RequireConfirmedAccount { get; set; }

        public string CookieName { get; set; } = "OpenPortal.Auth";

        public int CookieLifetimeHours { get; set; } = 8;

        /// <summary>
        /// How often a session cookie is checked against the account (its security stamp). A password change, a
        /// reset, "sign out everywhere" or a deactivation ends the other sessions within this delay; 0 checks on
        /// every request.
        /// </summary>
        public int SessionValidationSeconds { get; set; } = 60;
    }

    public sealed class PasskeyPolicy
    {
        /// <summary>
        /// The relying party id passkeys are bound to: the portal's host name (<c>portal.example.com</c>) or a parent
        /// domain of it. Empty uses the host of each request. Changing it later makes existing passkeys unusable.
        /// </summary>
        public string? ServerDomain { get; set; }

        /// <summary>
        /// The origins (<c>https://portal.example.com</c>) a passkey ceremony may come from. Empty accepts only the
        /// origin of the request itself, which is right unless the browser reaches the portal under several names.
        /// </summary>
        public string[] AllowedOrigins { get; set; } = [];
    }

    public sealed class CookiePolicy
    {
        /// <summary>
        /// Forces <c>Secure</c> on the authentication cookie. Set to false only for local development,
        /// where the SPA is served over http://localhost by the Vite dev server.
        /// </summary>
        public bool RequireSecure { get; set; } = true;
    }
}