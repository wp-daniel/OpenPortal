using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;

namespace OpenPortal.Access.Domain.Groups;

/// <summary>
/// A named set of users. Granting a group access to an application grants it to every member.
/// <para>
/// A group may contain other groups (<see cref="GroupNesting"/>): the members of a subgroup are members of every
/// group above it, so "Sales Italy" inside "Sales" gets what Sales is given. The services refuse a nesting that
/// would make a cycle.
/// </para>
/// </summary>
public sealed class Group
{
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 500;

    private readonly List<GroupMember> _members = [];

    // Required by EF Core.
    private Group()
    {
    }

    private Group(Guid id, string name, string? description, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);

        Id = id;
        Name = name;
        NormalisedName = name.ToUpperInvariant();
        Description = description;
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Upper-cased name, unique, so "Sales" and "sales" cannot both exist.</summary>
    public string NormalisedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public IReadOnlyList<GroupMember> Members => _members;

    public static Result<Group> Create(Guid id, string? name, string? description, DateTimeOffset now)
    {
        var validatedName = ValidateName(name);
        if (validatedName.IsFailure)
        {
            return Result<Group>.Failure(validatedName.Error);
        }

        var validatedDescription = ValidateDescription(description);
        if (validatedDescription.IsFailure)
        {
            return Result<Group>.Failure(validatedDescription.Error);
        }

        return Result<Group>.Success(new Group(id, validatedName.Value, validatedDescription.Value, now));
    }

    public Result Update(string? name, string? description, DateTimeOffset now)
    {
        var validatedName = ValidateName(name);
        if (validatedName.IsFailure)
        {
            return Result.Failure(validatedName.Error);
        }

        var validatedDescription = ValidateDescription(description);
        if (validatedDescription.IsFailure)
        {
            return Result.Failure(validatedDescription.Error);
        }

        Name = validatedName.Value;
        NormalisedName = validatedName.Value.ToUpperInvariant();
        Description = validatedDescription.Value;
        UpdatedAtUtc = now;

        return Result.Success();
    }

    /// <summary>
    /// Adds a member, until <paramref name="expiresAtUtc"/> when given. Adding someone who is already a member only
    /// updates the end of the membership; the result says whether anything changed.
    /// </summary>
    public bool AddMember(Guid userId, DateTimeOffset now, DateTimeOffset? expiresAtUtc = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, userId);

        if (_members.FirstOrDefault(member => member.UserId == userId) is { } existing)
        {
            return existing.SetExpiry(expiresAtUtc);
        }

        var member = new GroupMember(Id, userId, now);
        member.SetExpiry(expiresAtUtc);
        _members.Add(member);

        return true;
    }

    /// <summary>Removes a member. Returns false when the user was not a member.</summary>
    public bool RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(candidate => candidate.UserId == userId);

        return member is not null && _members.Remove(member);
    }

    /// <summary>Validates and trims a group name without needing an instance.</summary>
    public static Result<string> ValidateName(string? name)
    {
        var trimmed = TextRules.Normalise(name);
        if (trimmed is null)
        {
            return Result<string>.Failure(AccessErrors.GroupNameRequired);
        }

        return trimmed.Length > NameMaxLength
            ? Result<string>.Failure(AccessErrors.GroupNameTooLong)
            : Result<string>.Success(trimmed);
    }

    private static Result<string?> ValidateDescription(string? description)
    {
        var trimmed = TextRules.Normalise(description);

        return trimmed?.Length > DescriptionMaxLength
            ? Result<string?>.Failure(AccessErrors.GroupDescriptionTooLong)
            : Result<string?>.Success(trimmed);
    }
}

/// <summary>Membership of one user in one group. The user is referenced by id only.</summary>
public sealed class GroupMember
{
    // Required by EF Core.
    private GroupMember()
    {
    }

    internal GroupMember(Guid groupId, Guid userId, DateTimeOffset addedAtUtc)
    {
        GroupId = groupId;
        UserId = userId;
        AddedAtUtc = addedAtUtc;
    }

    public Guid GroupId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset AddedAtUtc { get; private set; }

    /// <summary>When the membership ends by itself; null: until it is removed.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public bool IsActiveAt(DateTimeOffset now) => ExpiresAtUtc is null || ExpiresAtUtc > now;

    public bool SetExpiry(DateTimeOffset? expiresAtUtc)
    {
        if (ExpiresAtUtc == expiresAtUtc)
        {
            return false;
        }

        ExpiresAtUtc = expiresAtUtc;
        return true;
    }
}

/// <summary>
/// A group inside another: every member of <see cref="ChildGroupId"/> is a member of <see cref="ParentGroupId"/> too,
/// and of every group above it.
/// </summary>
public sealed class GroupNesting
{
    // Required by EF Core.
    private GroupNesting()
    {
    }

    public GroupNesting(Guid parentGroupId, Guid childGroupId, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, parentGroupId);
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, childGroupId);

        ParentGroupId = parentGroupId;
        ChildGroupId = childGroupId;
        AddedAtUtc = now;
    }

    public Guid ParentGroupId { get; private set; }

    public Guid ChildGroupId { get; private set; }

    public DateTimeOffset AddedAtUtc { get; private set; }
}
