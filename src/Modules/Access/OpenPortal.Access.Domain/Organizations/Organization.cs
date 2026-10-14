using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;

namespace OpenPortal.Access.Domain.Organizations;

/// <summary>What a member is in their organization; applications receive it as <c>org_role</c>.</summary>
public enum OrganizationRole
{
    /// <summary>An ordinary member.</summary>
    Member = 0,

    /// <summary>Administers the organization inside the applications that serve it.</summary>
    Admin = 1,
}

/// <summary>
/// A tenant: a customer company, a branch, a team that an application keeps apart from the others. The portal
/// holds who belongs to which organization and tells an application which one the user signs in for
/// (<c>org_id</c>, <c>org_name</c>, <c>org_role</c>); the application isolates its own data by it.
/// <para>
/// The <see cref="Slug"/> is how a sign-in names the organization (<c>organization=acme</c>) and never changes, so
/// an application may rely on it as much as on the id. A user may belong to several organizations and chooses one
/// when signing in. Organizations are flat, like groups.
/// </para>
/// </summary>
public sealed class Organization
{
    public const int NameMaxLength = 120;
    public const int SlugMaxLength = 64;
    public const int DescriptionMaxLength = 500;

    private readonly List<OrganizationMember> _members = [];

    // Required by EF Core.
    private Organization()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Lower-case, unique, immutable short name used in sign-in requests.</summary>
    public string Slug { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public IReadOnlyList<OrganizationMember> Members => _members;

    public static Result<Organization> Create(Guid id, string? name, string? slug, string? description, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);

        var validatedSlug = ValidateSlug(slug);
        if (validatedSlug.IsFailure)
        {
            return Result<Organization>.Failure(validatedSlug.Error);
        }

        var organization = new Organization { Id = id, Slug = validatedSlug.Value, CreatedAtUtc = now };

        var named = organization.Update(name, description, now);
        if (named.IsFailure)
        {
            return Result<Organization>.Failure(named.Error);
        }

        // Filling in a new organization is not an edit of it.
        organization.UpdatedAtUtc = null;

        return Result<Organization>.Success(organization);
    }

    /// <summary>Renames the organization. The slug cannot change.</summary>
    public Result Update(string? name, string? description, DateTimeOffset now)
    {
        var trimmedName = TextRules.Normalise(name);
        if (trimmedName is null)
        {
            return Result.Failure(AccessErrors.OrganizationNameRequired);
        }

        if (trimmedName.Length > NameMaxLength)
        {
            return Result.Failure(AccessErrors.OrganizationNameTooLong);
        }

        var trimmedDescription = TextRules.Normalise(description);
        if (trimmedDescription?.Length > DescriptionMaxLength)
        {
            return Result.Failure(AccessErrors.OrganizationDescriptionTooLong);
        }

        Name = trimmedName;
        Description = trimmedDescription;
        UpdatedAtUtc = now;

        return Result.Success();
    }

    /// <summary>
    /// Adds a member with <paramref name="role"/>, or gives an existing member that role. Returns what happened, so
    /// the caller audits and notifies only real changes.
    /// </summary>
    public MembershipChange SetMember(Guid userId, OrganizationRole role, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, userId);

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, null);
        }

        var existing = _members.FirstOrDefault(member => member.UserId == userId);
        if (existing is null)
        {
            _members.Add(new OrganizationMember(Id, userId, role, now));
            return MembershipChange.Added;
        }

        return existing.ChangeRole(role) ? MembershipChange.RoleChanged : MembershipChange.None;
    }

    /// <summary>Removes a member. Returns false when the user was not a member.</summary>
    public bool RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(candidate => candidate.UserId == userId);

        return member is not null && _members.Remove(member);
    }

    /// <summary>
    /// A slug is what a sign-in request names the organization by: lower-case ASCII letters, digits and hyphens,
    /// starting with a letter or a digit.
    /// </summary>
    public static Result<string> ValidateSlug(string? slug)
    {
        var trimmed = TextRules.Normalise(slug)?.ToLowerInvariant();
        if (trimmed is null)
        {
            return Result<string>.Failure(AccessErrors.OrganizationSlugRequired);
        }

        var valid = trimmed.Length <= SlugMaxLength
            && char.IsAsciiLetterOrDigit(trimmed[0])
            && trimmed.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');

        return valid ? Result<string>.Success(trimmed) : Result<string>.Failure(AccessErrors.OrganizationSlugInvalid);
    }
}

/// <summary>What <see cref="Organization.SetMember"/> changed.</summary>
public enum MembershipChange
{
    None = 0,
    Added = 1,
    RoleChanged = 2,
}

/// <summary>Membership of one user in one organization. The user is referenced by id only.</summary>
public sealed class OrganizationMember
{
    // Required by EF Core.
    private OrganizationMember()
    {
    }

    internal OrganizationMember(Guid organizationId, Guid userId, OrganizationRole role, DateTimeOffset addedAtUtc)
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        AddedAtUtc = addedAtUtc;
    }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public OrganizationRole Role { get; private set; }

    public DateTimeOffset AddedAtUtc { get; private set; }

    internal bool ChangeRole(OrganizationRole role)
    {
        if (Role == role)
        {
            return false;
        }

        Role = role;
        return true;
    }
}
