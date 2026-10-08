namespace OpenPortal.Audit.Infrastructure.Configuration;

/// <summary>Settings for the Audit module, bound from the <c>Audit</c> configuration section.</summary>
public sealed class AuditModuleOptions
{
    public const string SectionName = "Audit";

    /// <summary>
    /// How long entries are kept. Older ones are deleted once a day; 0 keeps everything. A year covers the
    /// usual review cycle; raise it where a regulation asks for longer.
    /// </summary>
    public int RetentionDays { get; set; } = 365;
}
