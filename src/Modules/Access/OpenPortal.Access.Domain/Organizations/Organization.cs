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

    /// <summary>
    /// Members sign in to the applications that serve this organization with two factors only (an authenticator code
    /// or a passkey), whatever the application asks for.
    /// </summary>
    public bool RequireMultiFactor { get; private set; }

    public IReadOnlyList<OrganizationMember> Members => _members;

    /// <summary>Sets the organization's sign-in policy. Returns whether anything changed.</summary>
    public bool SetPolicy(bool requireMultiFactor, DateTimeOffset now)
    {
        if (RequireMultiFactor == requireMultiFactor)
        {
            return false;
        }

        RequireMultiFactor = requireMultiFactor;
        UpdatedAtUtc = now;

        return true;
    }

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

/// <summary>
/// The roles one member holds in one application within this organization (a sales manager here, a viewer in another
/// tenant). They add to the roles the user's grants give, and reach the application only when the user signs in for
/// this organization.
/// </summary>
public sealed class OrganizationMemberRole
{
    // Required by EF Core.
    private OrganizationMemberRole()
    {
    }

    public OrganizationMemberRole(Guid organizationId, Guid userId, Guid applicationId, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        OrganizationId = organizationId;
        UserId = userId;
        ApplicationId = applicationId;
        Role = role;
    }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid ApplicationId { get; private set; }

    /// <summary>The application role's key (<see cref="Applications.ApplicationRole.Key"/>).</summary>
    public string Role { get; private set; } = string.Empty;
}

/// <summary>
/// An email domain an organization owns. Once verified (a DNS TXT record, or a portal administrator vouching for it),
/// people with a confirmed address at that domain can join the organization by themselves when they sign in.
/// </summary>
public sealed class OrganizationDomain
{
    public const int DomainMaxLength = 100;
    public const int TokenLength = 32;

    /// <summary>The TXT record value that proves ownership is this prefix followed by the token.</summary>
    public const string RecordPrefix = "openportal-verification=";

    // Required by EF Core.
    private OrganizationDomain()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    /// <summary>Lower-case host name (<c>acme.com</c>), unique across organizations.</summary>
    public string Domain { get; private set; } = string.Empty;

    public string VerificationToken { get; private set; } = string.Empty;

    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    /// <summary>How the domain was verified: <c>dns</c>, or <c>administrator</c> when a portal administrator vouched for it.</summary>
    public string? VerifiedBy { get; private set; }

    /// <summary>Whether people with a confirmed address at this domain join the organization when they sign in.</summary>
    public bool AutoJoin { get; private set; }

    public OrganizationRole AutoJoinRole { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public bool IsVerified => VerifiedAtUtc is not null;

    /// <summary>The TXT record to publish at the domain to prove it is the organization's.</summary>
    public string RecordValue => RecordPrefix + VerificationToken;

    public static Result<OrganizationDomain> Create(Guid id, Guid organizationId, string? domain, string token, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var normalised = NormaliseDomain(domain);
        if (normalised is null)
        {
            return Result<OrganizationDomain>.Failure(AccessErrors.OrganizationDomainInvalid);
        }

        return Result<OrganizationDomain>.Success(new OrganizationDomain
        {
            Id = id,
            OrganizationId = organizationId,
            Domain = normalised,
            VerificationToken = token,
            CreatedAtUtc = now,
        });
    }

    public void MarkVerified(DateTimeOffset now, string by)
    {
        VerifiedAtUtc ??= now;
        VerifiedBy ??= by;
    }

    /// <summary>Sets automatic joining. Refused until the domain is verified: an unproven domain lets in no one.</summary>
    public Result<bool> SetAutoJoin(bool autoJoin, OrganizationRole role)
    {
        if (!Enum.IsDefined(role))
        {
            return Result<bool>.Failure(AccessErrors.OrganizationRoleInvalid);
        }

        if (autoJoin && !IsVerified)
        {
            return Result<bool>.Failure(AccessErrors.OrganizationDomainUnverified);
        }

        if (AutoJoin == autoJoin && AutoJoinRole == role)
        {
            return Result<bool>.Success(false);
        }

        AutoJoin = autoJoin;
        AutoJoinRole = role;

        return Result<bool>.Success(true);
    }

    /// <summary>Whether <paramref name="email"/> is an address at this domain (exactly: subdomains are not included).</summary>
    public bool Covers(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var at = email.LastIndexOf('@');

        return at > 0 && string.Equals(email[(at + 1)..].Trim(), Domain, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A host name with at least one dot, in lower case, or null.</summary>
    public static string? NormaliseDomain(string? value)
    {
        var domain = value?.Trim().TrimStart('@').TrimEnd('.').ToLowerInvariant();

        if (string.IsNullOrEmpty(domain)
            || domain.Length > DomainMaxLength
            || Uri.CheckHostName(domain) != UriHostNameType.Dns
            || !domain.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        return domain;
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
