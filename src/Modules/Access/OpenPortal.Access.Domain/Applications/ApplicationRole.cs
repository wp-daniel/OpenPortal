using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;

namespace OpenPortal.Access.Domain.Applications;

/// <summary>
/// A role an application understands, such as <c>sales</c> or <c>billing-admin</c>.
/// <para>
/// The application decides what a role allows; the portal only decides who holds it. Roles are assigned on
/// access grants (to a user directly or to a group) and reach the application as <c>role</c> claims in its
/// tokens, so the application can check <c>User.IsInRole("sales")</c> instead of keeping its own list.
/// </para>
/// <para>
/// The <see cref="Key"/> is what the application checks and never changes; the name and description are for
/// the administrators who assign it.
/// </para>
/// </summary>
public sealed class ApplicationRole
{
    public const int KeyMaxLength = 64;
    public const int DisplayNameMaxLength = 80;
    public const int DescriptionMaxLength = 300;

    // Required by EF Core.
    private ApplicationRole()
    {
    }

    internal ApplicationRole(Guid applicationId, string key, string displayName, string? description)
    {
        ApplicationId = applicationId;
        Key = key;
        DisplayName = displayName;
        Description = description;
    }

    public Guid ApplicationId { get; private set; }

    /// <summary>Lower-case identifier sent in the <c>role</c> claim.</summary>
    public string Key { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Applies a new name and description. Returns true when anything changed.</summary>
    internal bool Rename(string displayName, string? description)
    {
        if (string.Equals(DisplayName, displayName, StringComparison.Ordinal)
            && string.Equals(Description, description, StringComparison.Ordinal))
        {
            return false;
        }

        DisplayName = displayName;
        Description = description;

        return true;
    }

    /// <summary>
    /// A role key is lower-case ASCII letters, digits, hyphens, underscores, dots and colons, starting with a
    /// letter or a digit (<c>sales</c>, <c>crm:admin</c>, <c>billing.read</c>). Upper case is folded, so
    /// "Sales" and "sales" are the same role.
    /// </summary>
    public static Result<string> ValidateKey(string? key)
    {
        var trimmed = TextRules.Normalise(key)?.ToLowerInvariant();
        if (trimmed is null)
        {
            return Result<string>.Failure(AccessErrors.RoleKeyRequired);
        }

        var valid = trimmed.Length <= KeyMaxLength
            && char.IsAsciiLetterOrDigit(trimmed[0])
            && trimmed.All(character => char.IsAsciiLetterLower(character)
                || char.IsAsciiDigit(character)
                || character is '-' or '_' or '.' or ':');

        return valid ? Result<string>.Success(trimmed) : Result<string>.Failure(AccessErrors.RoleKeyInvalid);
    }

    /// <summary>Validates one role definition and returns its normalised copy.</summary>
    public static Result<ApplicationRoleDetails> Validate(ApplicationRoleDetails role)
    {
        ArgumentNullException.ThrowIfNull(role);

        var key = ValidateKey(role.Key);
        if (key.IsFailure)
        {
            return Result<ApplicationRoleDetails>.Failure(key.Error);
        }

        // A role without a name is shown by its key.
        var displayName = TextRules.Normalise(role.DisplayName) ?? key.Value;
        if (displayName.Length > DisplayNameMaxLength)
        {
            return Result<ApplicationRoleDetails>.Failure(AccessErrors.RoleNameTooLong);
        }

        var description = TextRules.Normalise(role.Description);
        if (description?.Length > DescriptionMaxLength)
        {
            return Result<ApplicationRoleDetails>.Failure(AccessErrors.RoleDescriptionTooLong);
        }

        return Result<ApplicationRoleDetails>.Success(new ApplicationRoleDetails(key.Value, displayName, description));
    }
}

/// <summary>A role as an administrator defines it or an application announces it.</summary>
public sealed record ApplicationRoleDetails(string? Key, string? DisplayName, string? Description);

/// <summary>Which of the user's groups an application receives in the <c>groups</c> claim.</summary>
public enum GroupClaimMode
{
    /// <summary>No <c>groups</c> claim. The default: group names are the portal's business.</summary>
    None = 0,

    /// <summary>Only the user's groups that are granted this application.</summary>
    Granted = 1,

    /// <summary>Every group the user belongs to, for applications that map groups to their own teams.</summary>
    All = 2,
}
