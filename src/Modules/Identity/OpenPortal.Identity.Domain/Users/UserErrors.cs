using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Domain.Users;

/// <summary>
/// Stable, machine-readable failure codes for the Identity module. Clients branch on
/// <see cref="Error.Code"/>; <see cref="Error.Description"/> is for logs only.
/// </summary>
public static class UserErrors
{
    private const string Prefix = "identity.";

    public static Error InvalidCredentials { get; } =
        Error.Unauthorized(Prefix + "invalid_credentials", "The supplied email or password is not valid.");

    public static Error AccountLockedOut { get; } =
        Error.Unauthorized(Prefix + "account_locked_out", "The account is temporarily locked after too many failed attempts.");

    public static Error AccountNotAllowed { get; } =
        Error.Unauthorized(Prefix + "account_not_allowed", "The account is not allowed to sign in.");

    public static Error TwoFactorRequired { get; } =
        Error.Unauthorized(Prefix + "two_factor_required", "The account requires an additional verification step.");

    public static Error NotAuthenticated { get; } =
        Error.Unauthorized(Prefix + "not_authenticated", "No authenticated session was found.");

    public static Error Forbidden { get; } =
        Error.Forbidden(Prefix + "forbidden", "The caller is not allowed to perform this operation.");

    public static Error UserNotFound { get; } =
        Error.NotFound(Prefix + "user_not_found", "The requested user does not exist.");

    public static Error DuplicateEmail { get; } =
        Error.Conflict(Prefix + "duplicate_email", "An account with this email address already exists.");

    public static Error UnsupportedLanguage { get; } =
        Error.Validation(Prefix + "unsupported_language", "The requested language is not supported.");

    public static Error UnknownRole { get; } =
        Error.Validation(Prefix + "unknown_role", "One or more supplied roles are not recognised by this platform.");

    public static Error CannotModifyOwnRoles { get; } =
        Error.Conflict(Prefix + "cannot_modify_own_roles", "An administrator cannot change their own roles.");

    public static Error PasswordComplexity { get; } =
        Error.Validation(Prefix + "password_complexity", "The password does not satisfy the configured complexity policy.");

    public static Error CurrentPasswordIncorrect { get; } =
        Error.Validation(Prefix + "current_password_incorrect", "The current password is not correct.");

    public static Error DisplayNameRequired { get; } =
        Error.Validation(Prefix + "display_name_required", "A display name is required.");

    public static Error DisplayNameTooShort { get; } =
        Error.Validation(Prefix + "display_name_too_short", $"The display name must be at least {ApplicationUser.DisplayNameMinLength} characters.");

    public static Error DisplayNameTooLong { get; } =
        Error.Validation(Prefix + "display_name_too_long", $"The display name must not exceed {ApplicationUser.DisplayNameMaxLength} characters.");

    public static Error FirstNameRequired { get; } =
        Error.Validation(Prefix + "first_name_required", "A first name is required.");

    public static Error LastNameRequired { get; } =
        Error.Validation(Prefix + "last_name_required", "A last name is required.");

    public static Error NameTooLong { get; } =
        Error.Validation(Prefix + "name_too_long", $"First and last names must not exceed {ApplicationUser.NameMaxLength} characters.");

    public static Error InvalidPhoneNumber { get; } =
        Error.Validation(Prefix + "invalid_phone_number", "The phone number is not valid.");

    public static Error DetailTooLong { get; } =
        Error.Validation(Prefix + "detail_too_long", $"Work and address fields must not exceed {ApplicationUser.DetailMaxLength} characters.");

    public static Error AvatarRequired { get; } =
        Error.Validation(Prefix + "avatar_required", "An image file is required.");

    public static Error AvatarTooLarge { get; } =
        Error.Validation(Prefix + "avatar_too_large", $"The image must not exceed {UserAvatar.MaxBytes / 1024} KB.");

    public static Error AvatarUnsupportedType { get; } =
        Error.Validation(Prefix + "avatar_unsupported_type", "The image must be a PNG, JPEG or WebP file.");

    public static Error AvatarNotFound { get; } =
        Error.NotFound(Prefix + "avatar_not_found", "The user has no profile picture.");

    public static Error EmailRequired { get; } =
        Error.Validation(Prefix + "email_required", "An email address is required.");
}