using OpenPortal.Content.Domain.Profiles;
using OpenPortal.Content.Domain.Projects;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Content.Domain;

/// <summary>
/// Stable, machine-readable failure codes for the Content module. Clients branch on
/// <see cref="Error.Code"/>; <see cref="Error.Description"/> is for logs and human-facing copy.
/// </summary>
public static class ContentErrors
{
    private const string Prefix = "content.";

    public static Error ProfileNotFound { get; } =
        Error.NotFound(Prefix + "profile_not_found", "The requested profile does not exist.");

    public static Error ProjectNotFound { get; } =
        Error.NotFound(Prefix + "project_not_found", "The requested project does not exist.");

    public static Error DisplayNameRequired { get; } =
        Error.Validation(Prefix + "display_name_required", "A display name is required.");

    public static Error DisplayNameTooShort { get; } =
        Error.Validation(
            Prefix + "display_name_too_short",
            $"The display name must be at least {Profile.DisplayNameMinLength} characters.");

    public static Error DisplayNameTooLong { get; } =
        Error.Validation(
            Prefix + "display_name_too_long",
            $"The display name must not exceed {Profile.DisplayNameMaxLength} characters.");

    public static Error HeadlineTooLong { get; } =
        Error.Validation(
            Prefix + "headline_too_long",
            $"The headline must not exceed {Profile.HeadlineMaxLength} characters.");

    public static Error SummaryTooLong { get; } =
        Error.Validation(
            Prefix + "summary_too_long",
            $"The summary must not exceed {Profile.SummaryMaxLength} characters.");

    public static Error LocationTooLong { get; } =
        Error.Validation(
            Prefix + "location_too_long",
            $"The location must not exceed {Profile.LocationMaxLength} characters.");

    public static Error EmailInvalid { get; } =
        Error.Validation(Prefix + "email_invalid", "The email address is not a valid address.");

    public static Error UrlInvalid { get; } =
        Error.Validation(Prefix + "url_invalid", "The value must be an absolute http or https URL.");

    public static Error SlugRequired { get; } =
        Error.Validation(Prefix + "slug_required", "A slug is required.");

    public static Error SlugTooLong { get; } =
        Error.Validation(Prefix + "slug_too_long", $"The slug must not exceed {Project.SlugMaxLength} characters.");

    public static Error SlugInvalid { get; } =
        Error.Validation(
            Prefix + "slug_invalid",
            "A slug may only contain ASCII letters, digits, hyphens and underscores.");

    public static Error SlugAlreadyInUse { get; } =
        Error.Conflict(Prefix + "slug_already_in_use", "Another project already uses this slug.");

    public static Error ProjectSummaryTooLong { get; } =
        Error.Validation(Prefix + "project_summary_too_long", $"The summary must not exceed {Project.SummaryMaxLength} characters.");

    public static Error DescriptionTooLong { get; } =
        Error.Validation(
            Prefix + "description_too_long",
            $"The description must not exceed {Project.DescriptionMaxLength} characters.");

    public static Error TechnologyNameRequired { get; } =
        Error.Validation(Prefix + "technology_name_required", "A technology name cannot be blank.");

    public static Error TechnologyNameTooLong { get; } =
        Error.Validation(
            Prefix + "technology_name_too_long",
            $"A technology name must not exceed {Technology.NameMaxLength} characters.");

    public static Error ProjectNameRequired { get; } =
        Error.Validation(Prefix + "project_name_required", "A project name is required.");

    public static Error ProjectNameTooLong { get; } =
        Error.Validation(
            Prefix + "project_name_too_long",
            $"The project name must not exceed {Project.NameMaxLength} characters.");

    public static Error CompletionBeforeStart { get; } =
        Error.Validation(
            Prefix + "completion_before_start",
            "A project cannot be completed before it started.");

    public static Error SocialLinkPlatformRequired { get; } =
        Error.Validation(Prefix + "social_link_platform_required", "A social link requires a platform.");

    public static Error SocialLinkPlatformTooLong { get; } =
        Error.Validation(
            Prefix + "social_link_platform_too_long",
            $"The platform must not exceed {SocialLink.PlatformMaxLength} characters.");

    public static Error SocialLinkLabelTooLong { get; } =
        Error.Validation(
            Prefix + "social_link_label_too_long",
            $"The label must not exceed {SocialLink.LabelMaxLength} characters.");

    public static Error DuplicateTechnology { get; } =
        Error.Validation(Prefix + "duplicate_technology", "The same technology cannot be listed twice.");
}