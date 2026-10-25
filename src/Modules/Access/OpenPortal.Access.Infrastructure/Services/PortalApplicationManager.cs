using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using OpenIddict.EntityFrameworkCore.Models;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// OpenIddict's application manager, except that a client secret replaced with an overlap keeps working until the
/// overlap ends: an application's servers can be given the new secret one by one, without a window where some of them
/// are refused. Only the previous secret's hash is kept (in the client's properties), like the current one's.
/// </summary>
internal sealed class PortalApplicationManager : OpenIddictApplicationManager<OpenIddictEntityFrameworkCoreApplication<Guid>>
{
    public const string PreviousSecretProperty = "openportal_previous_secret";
    public const string PreviousSecretExpiresProperty = "openportal_previous_secret_expires";

    public PortalApplicationManager(
        IOpenIddictApplicationCache<OpenIddictEntityFrameworkCoreApplication<Guid>> cache,
        ILogger<OpenIddictApplicationManager<OpenIddictEntityFrameworkCoreApplication<Guid>>> logger,
        IOptionsMonitor<OpenIddictCoreOptions> options,
        IOpenIddictApplicationStore<OpenIddictEntityFrameworkCoreApplication<Guid>> store)
        : base(cache, logger, options, store)
    {
    }

    public override async ValueTask<bool> ValidateClientSecretAsync(
        OpenIddictEntityFrameworkCoreApplication<Guid> application,
        string secret,
        CancellationToken cancellationToken = default)
    {
        if (await base.ValidateClientSecretAsync(application, secret, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        var properties = await Store.GetPropertiesAsync(application, cancellationToken).ConfigureAwait(false);
        if (!properties.TryGetValue(PreviousSecretProperty, out var hash)
            || !properties.TryGetValue(PreviousSecretExpiresProperty, out var expires)
            || !DateTimeOffset.TryParse(expires.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var until)
            || until <= DateTimeOffset.UtcNow
            || hash.GetString() is not { Length: > 0 } comparand)
        {
            return false;
        }

        return await ValidateClientSecretAsync(secret, comparand, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Replaces the client secret. With an overlap, the secret being replaced keeps working until <paramref name="until"/>;
    /// without one (or for a client that had none) it stops at once, as does any earlier overlap.
    /// </summary>
    public async ValueTask ReplaceSecretAsync(
        OpenIddictEntityFrameworkCoreApplication<Guid> application,
        string secret,
        DateTimeOffset? until,
        CancellationToken cancellationToken)
    {
        var previous = await Store.GetClientSecretAsync(application, cancellationToken).ConfigureAwait(false);
        var properties = (await Store.GetPropertiesAsync(application, cancellationToken).ConfigureAwait(false))
            .Remove(PreviousSecretProperty)
            .Remove(PreviousSecretExpiresProperty);

        if (until is { } end && !string.IsNullOrEmpty(previous))
        {
            properties = properties
                .SetItem(PreviousSecretProperty, JsonSerializer.SerializeToElement(previous))
                .SetItem(PreviousSecretExpiresProperty, JsonSerializer.SerializeToElement(end.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)));
        }

        await Store.SetPropertiesAsync(application, properties, cancellationToken).ConfigureAwait(false);

        // Hashes the new secret and saves the client with its properties.
        await UpdateAsync(application, secret, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Until when the previous secret is still accepted; null when there is none (or it already stopped).</summary>
    public async ValueTask<DateTimeOffset?> PreviousSecretValidUntilAsync(
        OpenIddictEntityFrameworkCoreApplication<Guid> application,
        CancellationToken cancellationToken)
    {
        var properties = await Store.GetPropertiesAsync(application, cancellationToken).ConfigureAwait(false);

        return properties.TryGetValue(PreviousSecretExpiresProperty, out var expires)
            && DateTimeOffset.TryParse(expires.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var until)
            && until > DateTimeOffset.UtcNow
                ? until
                : null;
    }
}
