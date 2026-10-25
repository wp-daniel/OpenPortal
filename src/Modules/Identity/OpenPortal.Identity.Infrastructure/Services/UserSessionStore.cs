using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Notifications;
using OpenPortal.Identity.Domain.Settings;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Configuration;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>
/// Keeps the portal's authentication tickets in the database (<see cref="UserSession"/>) instead of in the cookie,
/// which then carries only the session's id. That is what makes a session visible to its owner and endable at once:
/// a row that ended (or went idle) is refused on the next request, without waiting for the security stamp check.
/// </summary>
internal sealed partial class UserSessionTicketStore : ITicketStore
{
    private const string Purpose = "OpenPortal.Identity.SessionTicket.v1";

    /// <summary>The session a sign-out in this request ended, in case a new ticket for the same account follows.</summary>
    private const string RemovedItem = "openportal.session_removed";

    /// <summary>How long a device stays "known" after it was last used to sign in.</summary>
    private static readonly TimeSpan KnownDeviceWindow = TimeSpan.FromDays(90);

    private static readonly TimeSpan IdleSettingFreshness = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly IDataProtector _protector;
    private readonly IClock _clock;
    private readonly IOptions<SessionStoreOptions> _options;
    private readonly ILogger<UserSessionTicketStore> _logger;

    private IdleSetting _idle = new(0, DateTimeOffset.MinValue);

    public UserSessionTicketStore(
        IServiceScopeFactory scopes,
        IDataProtectionProvider dataProtection,
        IClock clock,
        IOptions<SessionStoreOptions> options,
        ILogger<UserSessionTicketStore> logger)
    {
        _scopes = scopes;
        _protector = dataProtection.CreateProtector(Purpose);
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    public Task<string> StoreAsync(AuthenticationTicket ticket) => StoreAsync(ticket, null!, CancellationToken.None);

    public Task<string> StoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken) =>
        StoreAsync(ticket, null!, cancellationToken);

    public async Task<string> StoreAsync(AuthenticationTicket ticket, HttpContext httpContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(UserSession.IdLength / 2));
        if (!Guid.TryParse(ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            throw new InvalidOperationException("A portal session needs the account's id.");
        }

        var now = _clock.UtcNow;
        var impersonator = Guid.TryParse(ticket.Principal.FindFirstValue(PortalClaimTypes.ImpersonatorId), out var admin) ? admin : (Guid?)null;
        var methods = string.Join(' ', ticket.Principal.FindAll(AuthenticationMethods.ClaimType)
            .Concat(ticket.Principal.FindAll(ClaimTypes.AuthenticationMethod))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal));

        var session = UserSession.Open(
            id,
            userId,
            now,
            Protect(ticket),
            ticket.Properties.ExpiresUtc,
            ticket.Properties.IsPersistent,
            httpContext?.Connection.RemoteIpAddress?.ToString(),
            httpContext?.Request.Headers.UserAgent.ToString(),
            methods,
            impersonator);

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        // The same account signed in again within this request (a refreshed sign-in after a password change, a new
        // second factor): the session goes on under the new id.
        var continued = false;
        if (httpContext?.Items[RemovedItem] is (string removedId, Guid removedUser) && removedUser == userId)
        {
            var previous = await db.UserSessions.FirstOrDefaultAsync(row => row.Id == removedId, cancellationToken).ConfigureAwait(false);
            if (previous is not null)
            {
                previous.MarkReplaced();
                session.ContinueFrom(previous);
                continued = true;
            }
        }

        var firstOnDevice = !continued && impersonator is null && await IsNewDeviceAsync(db, userId, session, now, cancellationToken).ConfigureAwait(false);

        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (httpContext is not null)
        {
            httpContext.Items[SessionItems.CurrentSessionId] = id;
        }

        if (firstOnDevice)
        {
            await scope.ServiceProvider.GetRequiredService<INotifier>().NotifyAsync(
                    new Notice
                    {
                        Type = IdentityNotificationTypes.NewSignIn,
                        Audience = NoticeAudience.Users(userId),
                        Parameters = new Dictionary<string, string?>
                        {
                            ["device"] = session.Device ?? session.UserAgent,
                            ["ip"] = session.IpAddress,
                        },
                        Link = "/account?tab=security",
                        IncludeCaller = true,
                        Security = true,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return id;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) => RenewAsync(key, ticket, null!, CancellationToken.None);

    public Task RenewAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken) =>
        RenewAsync(key, ticket, null!, cancellationToken);

    public async Task RenewAsync(string key, AuthenticationTicket ticket, HttpContext httpContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var session = await db.UserSessions.FirstOrDefaultAsync(row => row.Id == key, cancellationToken).ConfigureAwait(false);

        if (session is null || session.EndedAtUtc is not null)
        {
            return;
        }

        session.Renew(Protect(ticket), ticket.Properties.ExpiresUtc);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) => RetrieveAsync(key, null!, CancellationToken.None);

    public Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken) =>
        RetrieveAsync(key, null!, cancellationToken);

    public async Task<AuthenticationTicket?> RetrieveAsync(string key, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(key) || key.Length != UserSession.IdLength)
        {
            return null;
        }

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var session = await db.UserSessions.FirstOrDefaultAsync(row => row.Id == key, cancellationToken).ConfigureAwait(false);
        var now = _clock.UtcNow;

        if (session is null || !session.IsActiveAt(now))
        {
            return null;
        }

        var idle = await IdleTimeoutAsync(db, now, cancellationToken).ConfigureAwait(false);
        if (idle > TimeSpan.Zero && now - session.LastSeenAtUtc > idle)
        {
            session.End(now, SessionEndReason.Idle);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return null;
        }

        var ticket = Unprotect(session.Ticket);
        if (ticket is null)
        {
            return null;
        }

        // Background calls (the notification bell polling) do not keep an idle session alive.
        var background = httpContext is not null && _options.Value.IsBackgroundRequest(httpContext);
        if (!background
            && session.Touch(now, httpContext?.Connection.RemoteIpAddress?.ToString(), httpContext?.Request.Headers.UserAgent.ToString()))
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (httpContext is not null)
        {
            httpContext.Items[SessionItems.CurrentSessionId] = key;
        }

        return ticket;
    }

    public Task RemoveAsync(string key) => RemoveAsync(key, null!, CancellationToken.None);

    public Task RemoveAsync(string key, CancellationToken cancellationToken) => RemoveAsync(key, null!, cancellationToken);

    public async Task RemoveAsync(string key, HttpContext httpContext, CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var session = await db.UserSessions.FirstOrDefaultAsync(row => row.Id == key, cancellationToken).ConfigureAwait(false);

        if (session is null)
        {
            return;
        }

        if (session.End(_clock.UtcNow, SessionEndReason.SignedOut))
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (httpContext is not null)
        {
            httpContext.Items[RemovedItem] = (session.Id, session.UserId);
        }
    }

    private async Task<bool> IsNewDeviceAsync(IdentityDbContext db, Guid userId, UserSession session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var since = now - KnownDeviceWindow;
        var earlier = await db.UserSessions
            .AsNoTracking()
            .Where(row => row.UserId == userId && row.CreatedAtUtc >= since && row.ImpersonatorId == null)
            .Select(row => row.UserAgent)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // The very first sign-in is not news; a browser seen before is not either.
        return earlier.Count > 0 && !earlier.Contains(session.UserAgent, StringComparer.Ordinal);
    }

    private async Task<TimeSpan> IdleTimeoutAsync(IdentityDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cached = Volatile.Read(ref _idle);
        if (now - cached.ReadAtUtc < IdleSettingFreshness)
        {
            return TimeSpan.FromMinutes(cached.Minutes);
        }

        var minutes = await db.SecuritySettings
            .AsNoTracking()
            .Where(settings => settings.Id == SecuritySettings.SingletonId)
            .Select(settings => settings.IdleTimeoutMinutes)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        Volatile.Write(ref _idle, new IdleSetting(minutes, now));

        return TimeSpan.FromMinutes(minutes);
    }

    /// <summary>Reads the idle timeout again at the next request (the instance that saved a new one).</summary>
    public void ForgetIdleSetting() => Volatile.Write(ref _idle, new IdleSetting(0, DateTimeOffset.MinValue));

    private byte[] Protect(AuthenticationTicket ticket) => _protector.Protect(TicketSerializer.Default.Serialize(ticket));

    private AuthenticationTicket? Unprotect(byte[] protectedTicket)
    {
        if (protectedTicket.Length == 0)
        {
            return null;
        }

        try
        {
            return TicketSerializer.Default.Deserialize(_protector.Unprotect(protectedTicket));
        }
        catch (CryptographicException exception)
        {
            // The keys that protected it are gone (a new key ring): the session cannot be read, so it is refused.
            LogTicketUnreadable(exception);
            return null;
        }
    }

    private sealed record IdleSetting(int Minutes, DateTimeOffset ReadAtUtc);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A stored session could not be decrypted; it is refused.")]
    private partial void LogTicketUnreadable(Exception exception);
}
