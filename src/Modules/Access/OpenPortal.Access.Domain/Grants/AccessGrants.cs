using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Access.Domain.Grants;

/// <summary>
/// Access to one application granted to one user directly.
/// <para>
/// User and group grants are two tables rather than one polymorphic row, so each can carry a real foreign
/// key: deleting a group or an application removes its grants in the database, not in application code.
/// </para>
/// </summary>
public sealed class ApplicationUserGrant
{
    // Required by EF Core.
    private ApplicationUserGrant()
    {
    }

    public ApplicationUserGrant(Guid applicationId, Guid userId, DateTimeOffset grantedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, applicationId);
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, userId);

        ApplicationId = applicationId;
        UserId = userId;
        GrantedAtUtc = grantedAtUtc;
    }

    public Guid ApplicationId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset GrantedAtUtc { get; private set; }

    /// <summary>Keys of the application roles that come with this grant.</summary>
    public IReadOnlyList<string> Roles { get; private set; } = [];

    /// <summary>
    /// Replaces the roles that come with this grant. The caller checks the keys against the application's
    /// roles. Returns true when anything changed.
    /// </summary>
    public bool SetRoles(IEnumerable<string> roleKeys)
    {
        ArgumentNullException.ThrowIfNull(roleKeys);

        var normalised = roleKeys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (normalised.SequenceEqual(Roles, StringComparer.Ordinal))
        {
            return false;
        }

        Roles = normalised;
        return true;
    }
}

/// <summary>Access to one application granted to every member of one group.</summary>
public sealed class ApplicationGroupGrant
{
    // Required by EF Core.
    private ApplicationGroupGrant()
    {
    }

    public ApplicationGroupGrant(Guid applicationId, Guid groupId, DateTimeOffset grantedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, applicationId);
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, groupId);

        ApplicationId = applicationId;
        GroupId = groupId;
        GrantedAtUtc = grantedAtUtc;
    }

    public Guid ApplicationId { get; private set; }

    public Guid GroupId { get; private set; }

    public DateTimeOffset GrantedAtUtc { get; private set; }

    /// <summary>Keys of the application roles that come with this grant.</summary>
    public IReadOnlyList<string> Roles { get; private set; } = [];

    /// <summary>
    /// Replaces the roles that come with this grant. The caller checks the keys against the application's
    /// roles. Returns true when anything changed.
    /// </summary>
    public bool SetRoles(IEnumerable<string> roleKeys)
    {
        ArgumentNullException.ThrowIfNull(roleKeys);

        var normalised = roleKeys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (normalised.SequenceEqual(Roles, StringComparer.Ordinal))
        {
            return false;
        }

        Roles = normalised;
        return true;
    }
}

/// <summary>
/// Access to one page of the portal granted to every member of one group.
/// <para>
/// The page is referenced by its key only: the pages belong to the host, which validates the key against its
/// catalog before granting. A key the host later stops declaring is simply ignored when access is evaluated.
/// </para>
/// </summary>
public sealed class PageGroupGrant
{
    public const int PageKeyMaxLength = 64;

    // Required by EF Core.
    private PageGroupGrant()
    {
    }

    private PageGroupGrant(string pageKey, Guid groupId, DateTimeOffset grantedAtUtc)
    {
        PageKey = pageKey;
        GroupId = groupId;
        GrantedAtUtc = grantedAtUtc;
    }

    public string PageKey { get; private set; } = string.Empty;

    public Guid GroupId { get; private set; }

    public DateTimeOffset GrantedAtUtc { get; private set; }

    public static Result<PageGroupGrant> Create(string? pageKey, Guid groupId, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, groupId);

        var validated = ValidatePageKey(pageKey);

        return validated.IsFailure
            ? Result<PageGroupGrant>.Failure(validated.Error)
            : Result<PageGroupGrant>.Success(new PageGroupGrant(validated.Value, groupId, now));
    }

    /// <summary>
    /// A page key is lower-case ASCII letters, digits and hyphens, in dot-separated segments
    /// (<c>users</c>, <c>content.projects</c>).
    /// </summary>
    public static Result<string> ValidatePageKey(string? pageKey)
    {
        if (string.IsNullOrEmpty(pageKey) || pageKey.Length > PageKeyMaxLength)
        {
            return Result<string>.Failure(AccessErrors.PageKeyInvalid);
        }

        var segments = pageKey.Split('.');
        var valid = segments.All(segment =>
            segment.Length > 0 && segment.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'));

        return valid ? Result<string>.Success(pageKey) : Result<string>.Failure(AccessErrors.PageKeyInvalid);
    }
}
