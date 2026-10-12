namespace OpenPortal.Access.Domain.Applications;

/// <summary>
/// What an application's own backend may do through the portal's application API, with a token it obtains for
/// itself (client credentials, no user). Each is an OAuth scope the administrator enables per application; an
/// application with none cannot obtain such a token at all.
/// </summary>
public static class ApplicationApiScopes
{
    /// <summary>Read the users who can open the application, with their roles and groups.</summary>
    public const string UsersRead = "portal.users.read";

    /// <summary>Grant the application to a user, change their roles on it, or withdraw it.</summary>
    public const string AccessManage = "portal.access.manage";

    /// <summary>Create portal accounts (never administrators), which are then granted the application.</summary>
    public const string UsersCreate = "portal.users.create";

    /// <summary>Every scope, in the order the screens and the stored list use.</summary>
    public static IReadOnlyList<string> All { get; } = [UsersRead, AccessManage, UsersCreate];

    public static bool IsKnown(string? scope) => scope is not null && All.Contains(scope, StringComparer.Ordinal);
}
