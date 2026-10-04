namespace OpenPortal.Client;

/// <summary>
/// How an application connects to its OpenPortal instance. Bound from the <c>OpenPortal</c> configuration
/// section. Keep <see cref="ClientSecret"/> and <see cref="ProvisioningKey"/> in user secrets or the
/// environment, never in a committed settings file.
/// </summary>
public sealed class OpenPortalClientOptions
{
    public const string SectionName = "OpenPortal";

    /// <summary>The portal's address as the browser reaches it, for example <c>https://portal.example.com</c>.</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>The client id registered in the portal (lower-case letters, digits, hyphens, underscores).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Issued by the portal when an administrator approves or registers the application. Until it is set the
    /// application announces itself but cannot sign anyone in.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>The portal's shared provisioning key. When blank, the application does not announce itself.</summary>
    public string? ProvisioningKey { get; set; }

    /// <summary>Name shown to administrators and on users' launchpads. Defaults to the client id.</summary>
    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// This application's public address, for example <c>https://crm.example.com</c>. The announced
    /// redirect URIs are built from it, so it is required for announcing.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>Reported to the portal; defaults to the entry assembly's informational version.</summary>
    public string? Version { get; set; }

    public string CallbackPath { get; set; } = "/signin-oidc";

    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    /// <summary>How often the application re-announces itself, which the portal shows as "last seen".</summary>
    public int AnnounceIntervalMinutes { get; set; } = 5;

    /// <summary>Set to false only for a portal running on plain http during development.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}
