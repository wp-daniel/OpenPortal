using System.Reflection;

namespace OpenPortal.Web.Operations;

/// <summary>The version embedded in the web assembly at build time.</summary>
internal static class PortalBuild
{
    private static readonly Assembly Assembly = typeof(PortalBuild).Assembly;
    private static readonly string InformationalVersion =
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public static string Version { get; } = InformationalVersion.Split('+', 2)[0];

    public static string DisplayVersion { get; } = ShortenRevision(InformationalVersion);

    private static string ShortenRevision(string version)
    {
        // The SDK appends the source revision to the informational version. Keep the footer tooltip compact.
        if (!version.Contains('+', StringComparison.Ordinal))
        {
            return version;
        }

        var separator = Math.Max(version.LastIndexOf('+'), version.LastIndexOf('.'));
        var revision = version.AsSpan(separator + 1);
        return revision.Length == 40 && revision.IndexOfAnyExcept("0123456789abcdefABCDEF") < 0
            ? version[..(separator + 8)]
            : version;
    }
}
