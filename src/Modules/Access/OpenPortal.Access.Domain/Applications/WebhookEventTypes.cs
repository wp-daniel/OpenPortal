namespace OpenPortal.Access.Domain.Applications;

/// <summary>
/// The events an application can subscribe to with its webhook. Each is posted to the application only when it
/// concerns one of its users (someone who can open it) or its own access settings.
/// </summary>
public static class WebhookEventTypes
{
    /// <summary>A user's profile changed (name, email, details), by an administrator or the user.</summary>
    public const string UserUpdated = "user.updated";

    /// <summary>A user's account was deleted.</summary>
    public const string UserDeleted = "user.deleted";

    /// <summary>A user or a group was given the application.</summary>
    public const string AccessGranted = "access.granted";

    /// <summary>A user or a group lost the application.</summary>
    public const string AccessRevoked = "access.revoked";

    /// <summary>The application roles of a user's or a group's grant changed.</summary>
    public const string AccessRolesChanged = "access.roles_changed";

    /// <summary>Someone joined a group that has the application.</summary>
    public const string GroupMemberAdded = "group.member_added";

    /// <summary>Someone left a group that has the application.</summary>
    public const string GroupMemberRemoved = "group.member_removed";

    public const string OrganizationMemberAdded = "organization.member_added";

    public const string OrganizationMemberRemoved = "organization.member_removed";

    public const string OrganizationMemberRoleChanged = "organization.member_role_changed";

    /// <summary>Sent by an administrator from the application's settings, whatever the subscription.</summary>
    public const string Test = "webhook.test";

    private static readonly string[] Subscribable =
    [
        UserUpdated,
        UserDeleted,
        AccessGranted,
        AccessRevoked,
        AccessRolesChanged,
        GroupMemberAdded,
        GroupMemberRemoved,
        OrganizationMemberAdded,
        OrganizationMemberRemoved,
        OrganizationMemberRoleChanged,
    ];

    /// <summary>Every event an application can subscribe to, in a stable order.</summary>
    public static IReadOnlyList<string> All => Subscribable;

    public static bool IsKnown(string? type) => type is not null && Subscribable.Contains(type, StringComparer.Ordinal);
}
