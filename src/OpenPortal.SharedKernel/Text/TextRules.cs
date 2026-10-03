namespace OpenPortal.SharedKernel.Text;

/// <summary>
/// Small, dependency-free text rules shared by every module's domain entities.
/// <para>
/// These live in the shared kernel rather than being duplicated per entity so that "what counts as a
/// present string" or "what counts as an absolute web URL" has exactly one definition in the system.
/// </para>
/// </summary>
public static class TextRules
{
    /// <summary>
    /// Trims the value and collapses whitespace-only input to <see langword="null"/>, so that an entity can
    /// treat "absent" and "empty" identically without each caller remembering to check both.
    /// </summary>
    public static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// A conservative address check: exactly one '@' with non-empty, dot-free-segments on both sides and a
    /// dotted domain. Stricter rules reject addresses that are genuinely deliverable, and a full RFC 5322
    /// grammar is not something a hand-rolled regular expression gets right.
    /// <para>
    /// This guards the obvious mistakes only. Real deliverability is decided by sending a confirmation
    /// message, which this platform does not yet do.
    /// </para>
    /// </summary>
    public static bool IsPlausibleEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
        {
            return false;
        }

        var local = value[..at];
        var domain = value[(at + 1)..];

        return !local.StartsWith('.')
            && !local.EndsWith('.')
            && !local.Contains("..", StringComparison.Ordinal)
            && !local.Any(char.IsWhiteSpace)
            && domain.Contains('.', StringComparison.Ordinal)
            && !domain.StartsWith('.')
            && !domain.EndsWith('.')
            && !domain.Contains("..", StringComparison.Ordinal)
            && !domain.Any(char.IsWhiteSpace)
            && !HasEdgeHyphenInDomainLabels(domain);
    }

    /// <summary>
    /// True when any dot-separated domain label begins or ends with a hyphen. "user@-example.com" is
    /// rejected while "user@my-host.example.com" is not: only the labels' edges are restricted.
    /// </summary>
    private static bool HasEdgeHyphenInDomainLabels(string domain)
    {
        foreach (var label in domain.Split('.'))
        {
            if (label.Length == 0 || label[0] == '-' || label[^1] == '-')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// URL-safe identifier: ASCII letters, digits, hyphens and underscores, returned lower-cased.
    /// Comparison and uniqueness are always performed on the normalised form so that two requests differing
    /// only in case cannot produce two rows.
    /// </summary>
    public static bool TryNormaliseSlug(string? value, out string slug)
    {
        slug = string.Empty;

        var trimmed = Normalise(value);
        if (trimmed is null)
        {
            return false;
        }

        foreach (var character in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' && character is not '_')
            {
                return false;
            }
        }

        slug = trimmed.ToLowerInvariant();

        return true;
    }
}