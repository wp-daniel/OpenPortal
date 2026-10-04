namespace OpenPortal.Access.Infrastructure.Configuration;

/// <summary>Settings for the Access module, bound from the <c>Access</c> configuration section.</summary>
public sealed class AccessModuleOptions
{
    public const string SectionName = "Access";

    /// <summary>
    /// Shared secret that running applications present when they announce themselves. When blank,
    /// announcements are refused and applications can only be registered by hand. Keep it in user secrets
    /// or the environment, never in a committed settings file.
    /// </summary>
    public string? ProvisioningKey { get; set; }
}
