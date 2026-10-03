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