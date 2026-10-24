using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Infrastructure.Persistence;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// Encrypts webhook signing secrets with the portal's Data Protection keys: the portal must read them back to sign
/// each delivery, unlike client secrets, which it only verifies.
/// </summary>
internal sealed class WebhookSecretProtector
{
    private const string Purpose = "OpenPortal.Access.WebhookSecret.v1";

    /// <summary>The prefix of a secret in the Standard Webhooks format (<c>whsec_</c> + base64 key).</summary>
    public const string Prefix = "whsec_";

    private readonly IDataProtector _protector;

    public WebhookSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    /// <summary>A new 256-bit secret, in clear and protected.</summary>
    public (string Secret, string Protected) Generate()
    {
        var secret = Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        return (secret, _protector.Protect(secret));
    }

    public string? TryUnprotect(string? protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

/// <inheritdoc />
internal sealed class WebhookDirectory : IWebhookDirectory
{
    private readonly AccessDbContext _db;
    private readonly AccessQueries _queries;
    private readonly WebhookSecretProtector _protector;

    public WebhookDirectory(AccessDbContext db, AccessQueries queries, WebhookSecretProtector protector)
    {
        _db = db;
        _queries = queries;
        _protector = protector;
    }

    public async Task<IReadOnlyList<WebhookTarget>> ForUserAsync(Guid userId, string eventType, CancellationToken cancellationToken)
    {
        var applicationIds = await _queries.GrantedApplicationIdsAsync(userId, cancellationToken).ConfigureAwait(false);

        return await TargetsAsync(application => applicationIds.Contains(application.Id), eventType, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WebhookTarget>> ForGroupAsync(Guid groupId, string eventType, CancellationToken cancellationToken)
    {
        var applicationIds = await _db.GroupGrants
            .Where(grant => grant.GroupId == groupId)
            .Select(grant => grant.ApplicationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await TargetsAsync(application => applicationIds.Contains(application.Id), eventType, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<WebhookTarget>> ForApplicationAsync(string clientId, string eventType, CancellationToken cancellationToken) =>
        TargetsAsync(application => application.ClientId == clientId, eventType, cancellationToken);

    public async Task<WebhookTarget?> FindAsync(string clientId, CancellationToken cancellationToken)
    {
        var application = await _db.Applications
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.ClientId == clientId, cancellationToken)
            .ConfigureAwait(false);

        return application is not null && application.ReceivesWebhook(WebhookEventTypes.Test) ? ToTarget(application) : null;
    }

    private async Task<IReadOnlyList<WebhookTarget>> TargetsAsync(
        System.Linq.Expressions.Expression<Func<PortalApplication, bool>> filter,
        string eventType,
        CancellationToken cancellationToken)
    {
        // Few applications have a webhook: filter on what the database can see, the subscription in memory.
        var candidates = await _db.Applications
            .AsNoTracking()
            .Where(filter)
            .Where(application => application.Status == ApplicationStatus.Active
                && application.WebhookUrl != null
                && application.ProtectedWebhookSecret != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates
            .Where(application => application.ReceivesWebhook(eventType))
            .Select(ToTarget)
            .OfType<WebhookTarget>()
            .ToList();
    }

    private WebhookTarget? ToTarget(PortalApplication application) =>
        _protector.TryUnprotect(application.ProtectedWebhookSecret) is { } secret
            ? new WebhookTarget(application.ClientId, application.WebhookUrl!, secret)
            : null;
}
