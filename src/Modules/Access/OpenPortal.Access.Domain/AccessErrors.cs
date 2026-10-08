using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Groups;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Access.Domain;

/// <summary>
/// Stable, machine-readable failure codes for the Access module. Clients branch on
/// <see cref="Error.Code"/>; <see cref="Error.Description"/> is for logs and human-facing copy.
/// </summary>
public static class AccessErrors
{
    private const string Prefix = "access.";

    public static Error AdministrationForbidden { get; } =
        Error.Forbidden(Prefix + "administration_forbidden", "Managing application access requires an administrator account.");

    public static Error ApplicationNotFound { get; } =
        Error.NotFound(Prefix + "application_not_found", "The requested application does not exist.");

    public static Error GroupNotFound { get; } =
        Error.NotFound(Prefix + "group_not_found", "The requested group does not exist.");

    public static Error UserNotFound { get; } =
        Error.NotFound(Prefix + "user_not_found", "The requested user does not exist.");

    public static Error PageNotFound { get; } =
        Error.NotFound(Prefix + "page_not_found", "The requested page does not exist.");

    public static Error PageKeyInvalid { get; } =
        Error.Validation(
            Prefix + "page_key_invalid",
            "A page key may only contain lower-case letters, digits and hyphens, in dot-separated segments.");

    public static Error ClientIdRequired { get; } =
        Error.Validation(Prefix + "client_id_required", "A client id is required.");

    public static Error ClientIdTooLong { get; } =
        Error.Validation(
            Prefix + "client_id_too_long",
            $"The client id must not exceed {PortalApplication.ClientIdMaxLength} characters.");

    public static Error ClientIdInvalid { get; } =
        Error.Validation(
            Prefix + "client_id_invalid",
            "A client id may only contain ASCII letters, digits, hyphens and underscores.");

    public static Error ClientIdInUse { get; } =
        Error.Conflict(Prefix + "client_id_in_use", "Another application already uses this client id.");

    public static Error ApplicationNameRequired { get; } =
        Error.Validation(Prefix + "application_name_required", "An application name is required.");

    public static Error ApplicationNameTooLong { get; } =
        Error.Validation(
            Prefix + "application_name_too_long",
            $"The application name must not exceed {PortalApplication.DisplayNameMaxLength} characters.");

    public static Error ApplicationDescriptionTooLong { get; } =
        Error.Validation(
            Prefix + "application_description_too_long",
            $"The description must not exceed {PortalApplication.DescriptionMaxLength} characters.");

    public static Error BaseUrlInvalid { get; } =
        Error.Validation(Prefix + "base_url_invalid", "The application URL must be an absolute http or https URL.");

    public static Error RedirectUriRequired { get; } =
        Error.Validation(Prefix + "redirect_uri_required", "At least one sign-in redirect URI is required.");

    public static Error RedirectUriInvalid { get; } =
        Error.Validation(
            Prefix + "redirect_uri_invalid",
            "Redirect URIs must be absolute https URIs without a fragment (http is allowed for localhost only).");

    public static Error TooManyRedirectUris { get; } =
        Error.Validation(
            Prefix + "too_many_redirect_uris",
            $"An application may register at most {PortalApplication.MaxRedirectUris} redirect URIs of each kind.");

    public static Error VersionTooLong { get; } =
        Error.Validation(
            Prefix + "version_too_long",
            $"The version must not exceed {PortalApplication.VersionMaxLength} characters.");

    public static Error ApplicationNotPending { get; } =
        Error.Conflict(Prefix + "application_not_pending", "Only an application waiting for approval can be approved.");

    public static Error ApplicationNotActive { get; } =
        Error.Conflict(Prefix + "application_not_active", "Only an active application can be disabled or given a new secret.");

    public static Error ApplicationNotDisabled { get; } =
        Error.Conflict(Prefix + "application_not_disabled", "Only a disabled application can be enabled.");

    public static Error NoManifestChanges { get; } =
        Error.Conflict(Prefix + "no_manifest_changes", "The application has not proposed any changes.");

    public static Error GroupNameRequired { get; } =
        Error.Validation(Prefix + "group_name_required", "A group name is required.");

    public static Error GroupNameTooLong { get; } =
        Error.Validation(
            Prefix + "group_name_too_long",
            $"The group name must not exceed {Group.NameMaxLength} characters.");

    public static Error GroupDescriptionTooLong { get; } =
        Error.Validation(
            Prefix + "group_description_too_long",
            $"The description must not exceed {Group.DescriptionMaxLength} characters.");

    public static Error GroupNameInUse { get; } =
        Error.Conflict(Prefix + "group_name_in_use", "Another group already uses this name.");

    public static Error RoleKeyRequired { get; } =
        Error.Validation(Prefix + "role_key_required", "Every role needs a key.");

    public static Error RoleKeyInvalid { get; } =
        Error.Validation(
            Prefix + "role_key_invalid",
            $"A role key may only contain lower-case letters, digits, hyphens, underscores, dots and colons, start with a letter or a digit, and be at most {ApplicationRole.KeyMaxLength} characters long.");

    public static Error RoleKeyDuplicate { get; } =
        Error.Validation(Prefix + "role_key_duplicate", "Two roles have the same key.");

    public static Error RoleNameTooLong { get; } =
        Error.Validation(
            Prefix + "role_name_too_long",
            $"A role name must not exceed {ApplicationRole.DisplayNameMaxLength} characters.");

    public static Error RoleDescriptionTooLong { get; } =
        Error.Validation(
            Prefix + "role_description_too_long",
            $"A role description must not exceed {ApplicationRole.DescriptionMaxLength} characters.");

    public static Error TooManyRoles { get; } =
        Error.Validation(
            Prefix + "too_many_roles",
            $"An application may define at most {PortalApplication.MaxRoles} roles.");

    public static Error RoleNotFound { get; } =
        Error.Validation(Prefix + "role_not_found", "The application does not define one of the roles given.");

    public static Error GroupClaimsInvalid { get; } =
        Error.Validation(Prefix + "group_claims_invalid", "The groups claim setting must be none, granted or all.");

    public static Error ProvisioningKeyInvalid { get; } =
        Error.Unauthorized(Prefix + "provisioning_key_invalid", "The provisioning key is missing or not valid.");

    public static Error AnnouncementsDisabled { get; } =
        Error.Forbidden(
            Prefix + "announcements_disabled",
            "This portal does not accept application announcements (Access:ProvisioningKey is not configured).");
}
