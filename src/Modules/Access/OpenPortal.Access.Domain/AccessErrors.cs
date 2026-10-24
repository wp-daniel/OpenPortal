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

    public static Error ApiScopeInvalid { get; } =
        Error.Validation(
            Prefix + "api_scope_invalid",
            "Unknown application API scope; the scopes are portal.users.read, portal.access.manage and portal.users.create.");

    public static Error GroupNestingCycle { get; } =
        Error.Validation(Prefix + "group_nesting_cycle", "A group cannot contain itself, directly or through its subgroups.");

    public static Error ExpiryInPast { get; } =
        Error.Validation(Prefix + "expiry_in_past", "The end of temporary access must be in the future.");

    public static Error WebhookUrlInvalid { get; } =
        Error.Validation(Prefix + "webhook_url_invalid", "The webhook URL must be an absolute https address (http only for localhost).");

    public static Error WebhookEventInvalid { get; } =
        Error.Validation(Prefix + "webhook_event_invalid", "The webhook subscribes to an event that does not exist.");

    public static Error WebhookNotConfigured { get; } =
        Error.Conflict(Prefix + "webhook_not_configured", "Set the webhook URL and generate its secret first.");

    public static Error ClientTypeInvalid { get; } =
        Error.Validation(Prefix + "client_type_invalid", "The client type must be confidential or public.");

    public static Error ApiScopesNeedConfidentialClient { get; } =
        Error.Validation(Prefix + "api_scopes_need_confidential_client", "A public client has no secret, so it cannot be given application API scopes.");

    public static Error PublicClientHasNoSecret { get; } =
        Error.Conflict(Prefix + "public_client_has_no_secret", "A public client has no secret to regenerate.");

    public static Error BackChannelLogoutUriInvalid { get; } =
        Error.Validation(Prefix + "back_channel_logout_uri_invalid", "The back-channel logout URI must be an absolute https address (http only for localhost).");

    public static Error ApiScopeRequiresAccessManage { get; } =
        Error.Validation(
            Prefix + "api_scope_requires_access_manage",
            "An application that may create users must also be allowed to manage access (portal.access.manage).");

    public static Error ApiScopeMissing { get; } =
        Error.Forbidden(
            Prefix + "api_scope_missing",
            "The application's token does not carry the scope this operation needs, or the scope is no longer enabled.");

    public static Error ApiCallerUnknown { get; } =
        Error.Forbidden(
            Prefix + "api_caller_unknown",
            "Only an active application with a token of its own (client credentials) may call the application API.");

    public static Error UserHasNoAccess { get; } =
        Error.NotFound(Prefix + "user_has_no_access", "The user does not exist or cannot open this application.");

    public static Error RequestNotFound { get; } =
        Error.NotFound(Prefix + "request_not_found", "The access request does not exist.");

    public static Error RequestsNotAccepted { get; } =
        Error.Conflict(
            Prefix + "requests_not_accepted",
            "This application does not accept access requests; ask an administrator to grant it.");

    public static Error AlreadyHasAccess { get; } =
        Error.Conflict(Prefix + "already_has_access", "You can already open this application.");

    public static Error RequestAlreadyPending { get; } =
        Error.Conflict(Prefix + "request_already_pending", "A request for this application is already waiting for a decision.");

    public static Error RequestNotPending { get; } =
        Error.Conflict(Prefix + "request_not_pending", "The request has already been decided or withdrawn.");

    public static Error RequestReasonTooLong { get; } =
        Error.Validation(
            Prefix + "request_reason_too_long",
            $"The reason must not exceed {Requests.AccessRequest.ReasonMaxLength} characters.");

    public static Error RequestNoteTooLong { get; } =
        Error.Validation(
            Prefix + "request_note_too_long",
            $"The note must not exceed {Requests.AccessRequest.NoteMaxLength} characters.");

    public static Error OrganizationManageForbidden { get; } =
        Error.Forbidden(Prefix + "organization_manage_forbidden", "Managing this organization's members takes a portal administrator or one of its own administrators.");

    public static Error OrganizationLastAdmin { get; } =
        Error.Conflict(Prefix + "organization_last_admin", "An organization keeps at least one administrator: make someone else administrator first.");

    public static Error OrganizationNotFound { get; } =
        Error.NotFound(Prefix + "organization_not_found", "The requested organization does not exist.");

    public static Error OrganizationNameRequired { get; } =
        Error.Validation(Prefix + "organization_name_required", "An organization name is required.");

    public static Error OrganizationNameTooLong { get; } =
        Error.Validation(
            Prefix + "organization_name_too_long",
            $"The organization name must not exceed {Organizations.Organization.NameMaxLength} characters.");

    public static Error OrganizationDescriptionTooLong { get; } =
        Error.Validation(
            Prefix + "organization_description_too_long",
            $"The description must not exceed {Organizations.Organization.DescriptionMaxLength} characters.");

    public static Error OrganizationSlugRequired { get; } =
        Error.Validation(Prefix + "organization_slug_required", "An organization needs a short name (slug).");

    public static Error OrganizationSlugInvalid { get; } =
        Error.Validation(
            Prefix + "organization_slug_invalid",
            $"A short name may only contain lower-case letters, digits and hyphens, start with a letter or a digit, and be at most {Organizations.Organization.SlugMaxLength} characters long.");

    public static Error OrganizationSlugInUse { get; } =
        Error.Conflict(Prefix + "organization_slug_in_use", "Another organization already uses this short name.");

    public static Error OrganizationRoleInvalid { get; } =
        Error.Validation(Prefix + "organization_role_invalid", "The organization role must be member or admin.");

    public static Error OrganizationModeInvalid { get; } =
        Error.Validation(Prefix + "organization_mode_invalid", "The organization setting must be none, optional or required.");

    public static Error SignInRequired { get; } =
        Error.Unauthorized(Prefix + "sign_in_required", "This operation needs a signed-in user.");

    public static Error ProvisioningKeyInvalid { get; } =
        Error.Unauthorized(Prefix + "provisioning_key_invalid", "The provisioning key is missing or not valid.");

    public static Error AnnouncementsDisabled { get; } =
        Error.Forbidden(
            Prefix + "announcements_disabled",
            "This portal does not accept application announcements (Access:ProvisioningKey is not configured).");
}
