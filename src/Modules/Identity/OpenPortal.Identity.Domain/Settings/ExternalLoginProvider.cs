using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Domain.Settings;

/// <summary>What happens when someone signs in with an external account the portal does not know yet.</summary>
public enum ExternalSignUpMode
{
    /// <summary>Only accounts that already exist can sign in this way (linked by their owner, or by a verified address).</summary>
    Disabled = 0,

    /// <summary>A new account is created and signed in at once.</summary>
    Open = 1,

    /// <summary>A new account is created and waits for an administrator's approval.</summary>
    Approval = 2,
}

/// <summary>The identity providers the portal can sign people in with, besides its own passwords and passkeys.</summary>
public static class ExternalLoginProviders
{
    public const string Google = "google";

    public const string Microsoft = "microsoft";

    /// <summary>Every provider the portal knows, in the order the screens list them.</summary>
    public static IReadOnlyList<string> All { get; } = [Google, Microsoft];

    public static bool IsKnown(string? key) => key is not null && All.Contains(key, StringComparer.Ordinal);

    /// <summary>The path the portal listens on for the provider's answer (register it at the provider as redirect URI).</summary>
    public static string CallbackPath(string key) => "/signin-" + key;

    /// <summary>The name shown on the sign-in button and next to a linked account.</summary>
    public static string DisplayName(string key) => key switch
    {
        Google => "Google",
        Microsoft => "Microsoft",
        _ => key,
    };
}

/// <summary>
/// One external identity provider (Google, Microsoft Entra ID / Microsoft account) as an administrator configured it
/// from the portal: the application registration it signs in with, and who may get an account through it. One row per
/// provider; a provider without a row is off.
/// </summary>
public sealed class ExternalLoginProvider
{
    public const int ClientIdMaxLength = 200;
    public const int ProtectedSecretMaxLength = 4000;
    public const int TenantMaxLength = 100;
    public const int MaxDomains = 20;

    /// <summary>Microsoft's multi-tenant authorities: any work or school account, any account, personal accounts only.</summary>
    public const string MicrosoftOrganizations = "organizations";
    public const string MicrosoftCommon = "common";
    public const string MicrosoftConsumers = "consumers";

    /// <summary>Required by Entity Framework Core materialisation.</summary>
    private ExternalLoginProvider()
    {
    }

    private ExternalLoginProvider(string key)
    {
        Key = key;
    }

    /// <summary>The provider (<see cref="ExternalLoginProviders"/>); also the name of its authentication scheme.</summary>
    public string Key { get; private set; } = string.Empty;

    public bool Enabled { get; private set; }

    /// <summary>The client (application) id the provider issued for the portal.</summary>
    public string? ClientId { get; private set; }

    /// <summary>The client secret, protected with the portal's Data Protection keys; never stored or returned in clear.</summary>
    public string? ProtectedClientSecret { get; private set; }

    /// <summary>
    /// Microsoft only: <c>organizations</c>, <c>common</c>, <c>consumers</c>, or one directory (its id or a verified
    /// domain). One directory is what lets the portal trust the addresses it asserts.
    /// </summary>
    public string? Tenant { get; private set; }

    public ExternalSignUpMode SignUp { get; private set; }

    /// <summary>Email domains that may get an account through this provider (<c>example.com</c>); empty allows any.</summary>
    public IReadOnlyList<string> AllowedDomains { get; private set; } = [];

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    /// <summary>Whether a client secret is stored.</summary>
    public bool HasClientSecret => !string.IsNullOrEmpty(ProtectedClientSecret);

    /// <summary>A provider that is off and unconfigured, as one without a row reads.</summary>
    public static ExternalLoginProvider Unconfigured(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new ExternalLoginProvider(key);
    }

    /// <summary>
    /// Microsoft's tenant restricted to one directory (not one of the shared authorities), so the addresses its
    /// accounts carry were set by that directory's administrators.
    /// </summary>
    public bool IsSingleTenant =>
        Key == ExternalLoginProviders.Microsoft
        && !string.IsNullOrEmpty(Tenant)
        && Tenant is not (MicrosoftOrganizations or MicrosoftCommon or MicrosoftConsumers);

    /// <summary>
    /// Applies new values; the result says whether anything changed. <paramref name="protectedClientSecret"/> is
    /// <see langword="null"/> to keep the stored secret. A provider can only be on with a client id and a secret.
    /// </summary>
    public Result<bool> Update(
        bool enabled,
        string? clientId,
        string? protectedClientSecret,
        string? tenant,
        ExternalSignUpMode signUp,
        IEnumerable<string>? allowedDomains,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(signUp))
        {
            return Result<bool>.Failure(UserErrors.ExternalSignUpModeInvalid);
        }

        var id = string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim();
        if (id is { Length: > ClientIdMaxLength })
        {
            return Result<bool>.Failure(UserErrors.ExternalClientIdInvalid);
        }

        var tenantValue = NormaliseTenant(tenant);
        if (Key == ExternalLoginProviders.Microsoft)
        {
            if (tenantValue is null && !string.IsNullOrWhiteSpace(tenant))
            {
                return Result<bool>.Failure(UserErrors.ExternalTenantInvalid);
            }

            tenantValue ??= MicrosoftOrganizations;
        }
        else
        {
            tenantValue = null;
        }

        var domains = new List<string>();
        foreach (var value in allowedDomains ?? [])
        {
            var domain = EmailDomains.Normalise(value);
            if (domain is null)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return Result<bool>.Failure(UserErrors.RegistrationDomainInvalid);
                }

                continue;
            }

            if (!domains.Contains(domain, StringComparer.Ordinal))
            {
                domains.Add(domain);
            }
        }

        if (domains.Count > MaxDomains)
        {
            return Result<bool>.Failure(UserErrors.RegistrationDomainInvalid);
        }

        domains.Sort(StringComparer.Ordinal);

        var secret = protectedClientSecret ?? ProtectedClientSecret;
        if (secret is { Length: > ProtectedSecretMaxLength })
        {
            return Result<bool>.Failure(UserErrors.ExternalClientSecretInvalid);
        }

        // Turning a provider on without its registration would put a button on the sign-in page that cannot work.
        if (enabled && (id is null || string.IsNullOrEmpty(secret)))
        {
            return Result<bool>.Failure(UserErrors.ExternalProviderIncomplete);
        }

        if (Enabled == enabled
            && string.Equals(ClientId, id, StringComparison.Ordinal)
            && protectedClientSecret is null
            && string.Equals(Tenant, tenantValue, StringComparison.Ordinal)
            && SignUp == signUp
            && AllowedDomains.SequenceEqual(domains, StringComparer.Ordinal))
        {
            return Result<bool>.Success(false);
        }

        Enabled = enabled;
        ClientId = id;
        ProtectedClientSecret = secret;
        Tenant = tenantValue;
        SignUp = signUp;
        AllowedDomains = domains;
        UpdatedAtUtc = now;

        return Result<bool>.Success(true);
    }

    /// <summary>Whether <paramref name="email"/> may get an account through this provider.</summary>
    public bool AllowsEmail(string email) => EmailDomains.Allows(AllowedDomains, email);

    /// <summary>A tenant id (a Guid), a domain name or one of the shared authorities, in lower case; null when it is none.</summary>
    private static string? NormaliseTenant(string? value)
    {
        var tenant = value?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(tenant) || tenant.Length > TenantMaxLength)
        {
            return null;
        }

        if (tenant is MicrosoftOrganizations or MicrosoftCommon or MicrosoftConsumers || Guid.TryParse(tenant, out _))
        {
            return tenant;
        }

        return EmailDomains.Normalise(tenant);
    }
}

/// <summary>Email domain lists (<c>example.com</c>) matched exactly, so a domain does not let its subdomains in.</summary>
public static class EmailDomains
{
    public const int DomainMaxLength = 100;

    /// <summary>A host name in lower case (<c>example.com</c>), or null when <paramref name="value"/> is not one.</summary>
    public static string? Normalise(string? value)
    {
        var domain = value?.Trim().TrimStart('@').ToLowerInvariant();

        if (string.IsNullOrEmpty(domain) || domain.Length > DomainMaxLength || Uri.CheckHostName(domain) != UriHostNameType.Dns)
        {
            return null;
        }

        return domain.Contains('.', StringComparison.Ordinal) ? domain : null;
    }

    /// <summary>Whether <paramref name="email"/> is in one of <paramref name="domains"/>; an empty list allows any.</summary>
    public static bool Allows(IReadOnlyList<string> domains, string email)
    {
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(email);

        if (domains.Count == 0)
        {
            return true;
        }

        var at = email.LastIndexOf('@');

        return at > 0 && domains.Contains(email[(at + 1)..].Trim().ToLowerInvariant(), StringComparer.Ordinal);
    }
}
