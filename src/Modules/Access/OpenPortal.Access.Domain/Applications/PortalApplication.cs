using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;

namespace OpenPortal.Access.Domain.Applications;

/// <summary>Where an application is in its lifecycle. Only <see cref="Active"/> applications can sign users in.</summary>
public enum ApplicationStatus
{
    /// <summary>Announced itself but has not been approved by an administrator yet.</summary>
    Pending = 0,

    /// <summary>Approved: users with access can sign in to it.</summary>
    Active = 1,

    /// <summary>Switched off by an administrator. Its grants are kept so it can be re-enabled.</summary>
    Disabled = 2,
}

/// <summary>How the application came to be known to the portal.</summary>
public enum ApplicationSource
{
    /// <summary>Registered by an administrator.</summary>
    Manual = 0,

    /// <summary>Announced itself through the provisioning endpoint.</summary>
    Announced = 1,
}

/// <summary>
/// An application that signs its users in through the portal.
/// <para>
/// The <see cref="ClientId"/> is the OpenID Connect client identifier and never changes once registered,
/// because every deployed copy of the application is configured with it. Redirect URIs are part of the
/// security boundary: an application announcing itself can only <em>propose</em> new ones
/// (<see cref="AnnouncedRedirectUris"/>); an administrator decides whether they apply.
/// </para>
/// </summary>
public sealed class PortalApplication
{
    public const int ClientIdMaxLength = 64;
    public const int DisplayNameMaxLength = 120;
    public const int DescriptionMaxLength = 500;
    public const int UrlMaxLength = 512;
    public const int VersionMaxLength = 64;
    public const int MaxRedirectUris = 10;

    // Required by EF Core.
    private PortalApplication()
    {
    }

    private PortalApplication(Guid id, string clientId, ApplicationSource source, ApplicationStatus status, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);

        Id = id;
        ClientId = clientId;
        Source = source;
        Status = status;
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }

    /// <summary>OpenID Connect client identifier: lower-case slug, unique, immutable.</summary>
    public string ClientId { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Where users open the application, shown on their launchpad.</summary>
    public string BaseUrl { get; private set; } = string.Empty;

    public IReadOnlyList<string> RedirectUris { get; private set; } = [];

    public IReadOnlyList<string> PostLogoutRedirectUris { get; private set; } = [];

    public ApplicationStatus Status { get; private set; }

    public ApplicationSource Source { get; private set; }

    /// <summary>Version string the application last reported, if it announces itself.</summary>
    public string? Version { get; private set; }

    /// <summary>Redirect URIs the application last proposed. Applied only by an administrator.</summary>
    public IReadOnlyList<string> AnnouncedRedirectUris { get; private set; } = [];

    /// <summary>Post-logout redirect URIs the application last proposed.</summary>
    public IReadOnlyList<string> AnnouncedPostLogoutRedirectUris { get; private set; } = [];

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    /// <summary>When the application last announced itself; null for an application that never does.</summary>
    public DateTimeOffset? LastSeenAtUtc { get; private set; }

    /// <summary>
    /// True when the application has proposed redirect URIs that differ from the ones in force, so an
    /// administrator should review them.
    /// </summary>
    public bool HasManifestChanges =>
        Status != ApplicationStatus.Pending
        && (AnnouncedRedirectUris.Count > 0 || AnnouncedPostLogoutRedirectUris.Count > 0)
        && (!AnnouncedRedirectUris.SequenceEqual(RedirectUris, StringComparer.Ordinal)
            || !AnnouncedPostLogoutRedirectUris.SequenceEqual(PostLogoutRedirectUris, StringComparer.Ordinal));

    /// <summary>Registers an application on an administrator's behalf. It is active immediately.</summary>
    public static Result<PortalApplication> CreateManual(
        Guid id,
        string? clientId,
        ApplicationDetails details,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        var validatedClientId = ValidateClientId(clientId);
        if (validatedClientId.IsFailure)
        {
            return Result<PortalApplication>.Failure(validatedClientId.Error);
        }

        var validated = ValidateDetails(details);
        if (validated.IsFailure)
        {
            return Result<PortalApplication>.Failure(validated.Error);
        }

        var application = new PortalApplication(id, validatedClientId.Value, ApplicationSource.Manual, ApplicationStatus.Active, now);
        application.Apply(validated.Value);

        return Result<PortalApplication>.Success(application);
    }

    /// <summary>Records an application that announced itself. It waits for an administrator's approval.</summary>
    public static Result<PortalApplication> CreateAnnounced(
        Guid id,
        string? clientId,
        ApplicationDetails details,
        string? version,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        var validatedClientId = ValidateClientId(clientId);
        if (validatedClientId.IsFailure)
        {
            return Result<PortalApplication>.Failure(validatedClientId.Error);
        }

        var validated = ValidateDetails(details);
        if (validated.IsFailure)
        {
            return Result<PortalApplication>.Failure(validated.Error);
        }

        var validatedVersion = ValidateVersion(version);
        if (validatedVersion.IsFailure)
        {
            return Result<PortalApplication>.Failure(validatedVersion.Error);
        }

        var application = new PortalApplication(id, validatedClientId.Value, ApplicationSource.Announced, ApplicationStatus.Pending, now)
        {
            Version = validatedVersion.Value,
            LastSeenAtUtc = now,
            AnnouncedRedirectUris = validated.Value.RedirectUris,
            AnnouncedPostLogoutRedirectUris = validated.Value.PostLogoutRedirectUris,
        };
        application.Apply(validated.Value);

        return Result<PortalApplication>.Success(application);
    }

    /// <summary>Applies an administrator's edit. The client id cannot change.</summary>
    public Result Update(ApplicationDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        var validated = ValidateDetails(details);
        if (validated.IsFailure)
        {
            return Result.Failure(validated.Error);
        }

        Apply(validated.Value);
        UpdatedAtUtc = now;

        return Result.Success();
    }

    /// <summary>
    /// Records an announcement from a running copy of the application.
    /// <para>
    /// A pending application takes the announced details as they are, since nothing can sign in to it yet.
    /// An approved one only refreshes its heartbeat and version and keeps the proposal aside, because
    /// accepting new redirect URIs on the application's word would let whoever holds the provisioning key
    /// redirect sign-ins anywhere.
    /// </para>
    /// </summary>
    public Result RecordAnnouncement(ApplicationDetails details, string? version, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        var validated = ValidateDetails(details);
        if (validated.IsFailure)
        {
            return Result.Failure(validated.Error);
        }

        var validatedVersion = ValidateVersion(version);
        if (validatedVersion.IsFailure)
        {
            return Result.Failure(validatedVersion.Error);
        }

        if (Status == ApplicationStatus.Pending)
        {
            Apply(validated.Value);
        }

        AnnouncedRedirectUris = validated.Value.RedirectUris;
        AnnouncedPostLogoutRedirectUris = validated.Value.PostLogoutRedirectUris;
        Version = validatedVersion.Value;
        LastSeenAtUtc = now;

        return Result.Success();
    }

    /// <summary>Adopts the redirect URIs the application last proposed.</summary>
    public Result ApplyAnnouncedManifest(DateTimeOffset now)
    {
        if (!HasManifestChanges)
        {
            return Result.Failure(AccessErrors.NoManifestChanges);
        }

        RedirectUris = AnnouncedRedirectUris;
        PostLogoutRedirectUris = AnnouncedPostLogoutRedirectUris;
        UpdatedAtUtc = now;

        return Result.Success();
    }

    public Result Approve(DateTimeOffset now)
    {
        if (Status != ApplicationStatus.Pending)
        {
            return Result.Failure(AccessErrors.ApplicationNotPending);
        }

        Status = ApplicationStatus.Active;
        UpdatedAtUtc = now;

        return Result.Success();
    }

    public Result Disable(DateTimeOffset now)
    {
        if (Status != ApplicationStatus.Active)
        {
            return Result.Failure(AccessErrors.ApplicationNotActive);
        }

        Status = ApplicationStatus.Disabled;
        UpdatedAtUtc = now;

        return Result.Success();
    }

    public Result Enable(DateTimeOffset now)
    {
        if (Status != ApplicationStatus.Disabled)
        {
            return Result.Failure(AccessErrors.ApplicationNotDisabled);
        }

        Status = ApplicationStatus.Active;
        UpdatedAtUtc = now;

        return Result.Success();
    }

    /// <summary>Stores a field set that <see cref="ValidateDetails"/> has already normalised.</summary>
    private void Apply(ApplicationDetails details)
    {
        DisplayName = details.DisplayName!;
        Description = details.Description;
        BaseUrl = details.BaseUrl!;
        RedirectUris = details.RedirectUris;
        PostLogoutRedirectUris = details.PostLogoutRedirectUris;
    }

    /// <summary>Validates and normalises a client id without needing an instance.</summary>
    public static Result<string> ValidateClientId(string? clientId)
    {
        var trimmed = TextRules.Normalise(clientId);
        if (trimmed is null)
        {
            return Result<string>.Failure(AccessErrors.ClientIdRequired);
        }

        if (trimmed.Length > ClientIdMaxLength)
        {
            return Result<string>.Failure(AccessErrors.ClientIdTooLong);
        }

        return TextRules.TryNormaliseSlug(trimmed, out var normalised)
            ? Result<string>.Success(normalised)
            : Result<string>.Failure(AccessErrors.ClientIdInvalid);
    }

    /// <summary>
    /// Validates the editable field set as a whole, returning the normalised copy that will be stored.
    /// </summary>
    public static Result<ApplicationDetails> ValidateDetails(ApplicationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var displayName = TextRules.Normalise(details.DisplayName);
        if (displayName is null)
        {
            return Result<ApplicationDetails>.Failure(AccessErrors.ApplicationNameRequired);
        }

        if (displayName.Length > DisplayNameMaxLength)
        {
            return Result<ApplicationDetails>.Failure(AccessErrors.ApplicationNameTooLong);
        }

        var description = TextRules.Normalise(details.Description);
        if (description?.Length > DescriptionMaxLength)
        {
            return Result<ApplicationDetails>.Failure(AccessErrors.ApplicationDescriptionTooLong);
        }

        var baseUrl = TextRules.Normalise(details.BaseUrl);
        if (baseUrl is null || baseUrl.Length > UrlMaxLength || !TextRules.IsHttpUrl(baseUrl))
        {
            return Result<ApplicationDetails>.Failure(AccessErrors.BaseUrlInvalid);
        }

        var redirectUris = ValidateRedirectUris(details.RedirectUris);
        if (redirectUris.IsFailure)
        {
            return Result<ApplicationDetails>.Failure(redirectUris.Error);
        }

        if (redirectUris.Value.Count == 0)
        {
            return Result<ApplicationDetails>.Failure(AccessErrors.RedirectUriRequired);
        }

        var postLogoutRedirectUris = ValidateRedirectUris(details.PostLogoutRedirectUris);
        if (postLogoutRedirectUris.IsFailure)
        {
            return Result<ApplicationDetails>.Failure(postLogoutRedirectUris.Error);
        }

        return Result<ApplicationDetails>.Success(new ApplicationDetails(
            displayName,
            description,
            baseUrl,
            redirectUris.Value,
            postLogoutRedirectUris.Value));
    }

    /// <summary>
    /// Absolute https URIs without a fragment, de-duplicated in order. Plain http is accepted for loopback
    /// hosts only, so an application can be developed locally without a certificate.
    /// </summary>
    private static Result<IReadOnlyList<string>> ValidateRedirectUris(IEnumerable<string>? uris)
    {
        var normalised = new List<string>();

        foreach (var value in uris ?? [])
        {
            var trimmed = TextRules.Normalise(value);
            if (trimmed is null)
            {
                continue;
            }

            if (trimmed.Length > UrlMaxLength
                || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                || !string.IsNullOrEmpty(uri.Fragment)
                || !(uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            {
                return Result<IReadOnlyList<string>>.Failure(AccessErrors.RedirectUriInvalid);
            }

            if (!normalised.Contains(trimmed, StringComparer.Ordinal))
            {
                normalised.Add(trimmed);
            }
        }

        return normalised.Count > MaxRedirectUris
            ? Result<IReadOnlyList<string>>.Failure(AccessErrors.TooManyRedirectUris)
            : Result<IReadOnlyList<string>>.Success(normalised);
    }

    private static Result<string?> ValidateVersion(string? version)
    {
        var trimmed = TextRules.Normalise(version);

        return trimmed?.Length > VersionMaxLength
            ? Result<string?>.Failure(AccessErrors.VersionTooLong)
            : Result<string?>.Success(trimmed);
    }
}

/// <summary>The fields of an application that an administrator edits and an announcement proposes.</summary>
/// <param name="DisplayName">Name shown to users and administrators.</param>
/// <param name="Description">Optional one-line description for the launchpad.</param>
/// <param name="BaseUrl">Where users open the application.</param>
/// <param name="RedirectUris">Sign-in callback URIs. At least one is required.</param>
/// <param name="PostLogoutRedirectUris">Where the portal may send users after signing out.</param>
public sealed record ApplicationDetails(
    string? DisplayName,
    string? Description,
    string? BaseUrl,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris);
