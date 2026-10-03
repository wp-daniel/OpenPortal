namespace OpenPortal.Identity.Domain.Security;

/// <summary>
/// Well-known permission names for the future authorisation model.
/// <para>
/// v1 authorises exclusively through roles (see <see cref="Users.Roles"/>) and the
/// <c>AuthorizationPolicies</c> entries that require them. This class is the designated seam for moving to
/// policy-per-permission: declare a permission, map it to a policy in
/// <c>AuthorizationPolicies.AddAuthorizationPolicies</c>, then call <c>RequireAuthorization(Permission)</c>
/// on the endpoint or <c>[Authorize(Policy = ...)]</c> on the controller action. Role checks keep working
/// untouched, so the migration can be incremental.
/// </para>
/// <para>
/// No code enforces these constants yet; they are documentation that is compile-time checked.
/// </para>
/// </summary>
public static class Permissions
{
    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
    public const string ContentRead = "content.read";
    public const string ContentWrite = "content.write";

    public static IReadOnlyList<string> All { get; } = [UsersRead, UsersWrite, ContentRead, ContentWrite];
}