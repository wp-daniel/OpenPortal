namespace OpenPortal.Identity.Domain.Users;

/// <summary>
/// The role names the platform recognises. Stored as constants so that authorisation checks, seed data
/// and API contracts can never drift apart through string literals.
/// </para>
/// <para>
/// To add a role: declare it here, include it in <see cref="All"/> so the seeder provisions it, and add an
/// <c>AuthorizationPolicies</c> entry that requires it. Nothing else needs to change.
/// </para>
/// </summary>
public static class Roles
{
    /// <summary>Full access to administration endpoints.</summary>
    public const string Administrator = "Administrator";

    /// <summary>Default role granted to every registered account.</summary>
    public const string User = "User";

    private static readonly string[] AllRoles = [Administrator, User];

    /// <summary>Every role the platform recognises. Seeded on startup.</summary>
    public static IReadOnlyList<string> All => AllRoles;

    /// <summary>
    /// Validates a role name submitted by a client against the server-side allowlist. Authorisation must
    /// never trust a role name supplied by the caller.
    /// </summary>
    public static bool IsKnown(string? role) =>
        role is not null && AllRoles.Contains(role, StringComparer.Ordinal);
}