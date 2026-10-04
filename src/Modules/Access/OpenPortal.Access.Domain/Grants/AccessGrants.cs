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
}
