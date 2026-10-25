using System.Security.Claims;
using System.Text.Json;
using OpenIddict.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenPortal.Web.Oidc;

/// <summary>
/// The standard claims (OpenID Connect Core, section 5.1) the portal knows about an account, each with the scope that
/// releases it. The identity token and the userinfo endpoint read them from here, so they never disagree.
/// <para>
/// Not released: <c>picture</c> (the portal's avatar URL needs a portal session, which an application's browser
/// requests do not carry), <c>zoneinfo</c>, <c>birthdate</c>, <c>gender</c> and the other fields the portal does not keep.
/// </para>
/// </summary>
public static class StandardClaims
{
    /// <summary>One claim: its value is a string, a bool, a long (seconds) or a JSON object.</summary>
    public sealed record Entry(string Type, object Value, string Scope);

    /// <summary>Every claim type below, for the discovery document.</summary>
    public static readonly string[] Types =
    [
        Claims.GivenName,
        Claims.FamilyName,
        Claims.Locale,
        Claims.UpdatedAt,
        Claims.EmailVerified,
        Claims.PhoneNumber,
        Claims.PhoneNumberVerified,
        Claims.Address,
    ];

    /// <summary>The claims of <paramref name="user"/> whose scope <paramref name="hasScope"/> grants.</summary>
    public static IEnumerable<Entry> For(UserReferenceDto user, UserClaimsProfileDto? profile, Func<string, bool> hasScope)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(hasScope);

        if (hasScope(Scopes.Email))
        {
            yield return new Entry(Claims.EmailVerified, user.EmailConfirmed, Scopes.Email);
        }

        if (profile is null)
        {
            yield break;
        }

        if (hasScope(Scopes.Profile))
        {
            yield return new Entry(Claims.GivenName, profile.FirstName, Scopes.Profile);
            yield return new Entry(Claims.FamilyName, profile.LastName, Scopes.Profile);

            if (profile.Language is { } language)
            {
                yield return new Entry(Claims.Locale, language, Scopes.Profile);
            }

            yield return new Entry(Claims.UpdatedAt, profile.UpdatedAtUtc.ToUnixTimeSeconds(), Scopes.Profile);
        }

        if (hasScope(Scopes.Phone) && profile.PhoneNumber is { } phone)
        {
            yield return new Entry(Claims.PhoneNumber, phone, Scopes.Phone);
            yield return new Entry(Claims.PhoneNumberVerified, profile.PhoneNumberConfirmed, Scopes.Phone);
        }

        if (hasScope(Scopes.Address) && Address(profile) is { } address)
        {
            yield return new Entry(Claims.Address, address, Scopes.Address);
        }
    }

    /// <summary>Adds <paramref name="entry"/> to <paramref name="identity"/> with its JSON type (string, boolean, number, object).</summary>
    public static void AddTo(ClaimsIdentity identity, Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _ = entry.Value switch
        {
            bool flag => identity.SetClaim(entry.Type, flag),
            long number => identity.SetClaim(entry.Type, number),
            JsonElement json => identity.SetClaim(entry.Type, json),
            _ => identity.SetClaim(entry.Type, entry.Value.ToString()),
        };
    }

    /// <summary>The address claim (section 5.1.1), with only the parts the account has; null when it has none.</summary>
    private static JsonElement? Address(UserClaimsProfileDto profile)
    {
        var parts = new Dictionary<string, string>(StringComparer.Ordinal);

        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts[name] = value;
            }
        }

        Add("street_address", profile.AddressLine);
        Add("locality", profile.City);
        Add("postal_code", profile.PostalCode);
        Add("country", profile.Country);

        if (parts.Count == 0)
        {
            return null;
        }

        // The same parts on lines, as the specification's "formatted" member expects.
        var cityLine = string.Join(' ', new[] { profile.PostalCode, profile.City }.Where(value => !string.IsNullOrWhiteSpace(value)));
        parts["formatted"] = string.Join('\n', new[] { profile.AddressLine, cityLine, profile.Country }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return JsonSerializer.SerializeToElement(parts);
    }
}
