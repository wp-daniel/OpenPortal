using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenPortal.SharedKernel.Messaging;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenPortal.Web.Oidc;

/// <summary>
/// OpenID Connect Back-Channel Logout 1.0: when a user's portal session ends, every application they signed in to
/// that registered a back-channel logout URI receives a signed logout token, so it ends its own session too (single
/// logout). Delivery goes through the outbox, so an application that is briefly down still hears about it.
/// </summary>
public sealed class BackChannelLogoutNotifier
{
    private readonly IOpenIddictAuthorizationManager _authorizations;
    private readonly IOpenIddictApplicationManager _applications;
    private readonly IOutbox _outbox;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IOptionsMonitor<OpenIddictServerOptions> _server;

    public BackChannelLogoutNotifier(
        IOpenIddictAuthorizationManager authorizations,
        IOpenIddictApplicationManager applications,
        IOutbox outbox,
        IHttpContextAccessor httpContext,
        IOptionsMonitor<OpenIddictServerOptions> server)
    {
        _authorizations = authorizations;
        _applications = applications;
        _outbox = outbox;
        _httpContext = httpContext;
        _server = server;
    }

    /// <summary>Queues a logout token for each application the user holds a valid authorization for.</summary>
    public async Task NotifyAsync(Guid userId, CancellationToken cancellationToken)
    {
        var issuer = Issuer();
        if (issuer is null)
        {
            return;
        }

        var notified = new HashSet<string>(StringComparer.Ordinal);
        var messages = new List<OutboxMessageDraft>();

        await foreach (var authorization in _authorizations.FindBySubjectAsync(userId.ToString(), cancellationToken).ConfigureAwait(false))
        {
            if (!await _authorizations.HasStatusAsync(authorization, Statuses.Valid, cancellationToken).ConfigureAwait(false)
                || await _authorizations.GetApplicationIdAsync(authorization, cancellationToken).ConfigureAwait(false) is not { } applicationId
                || await _applications.FindByIdAsync(applicationId, cancellationToken).ConfigureAwait(false) is not { } application)
            {
                continue;
            }

            var clientId = await _applications.GetClientIdAsync(application, cancellationToken).ConfigureAwait(false);
            var properties = await _applications.GetPropertiesAsync(application, cancellationToken).ConfigureAwait(false);

            if (clientId is null
                || !notified.Add(clientId)
                || !properties.TryGetValue(BackChannelLogoutHandler.LogoutUriProperty, out var uri)
                || uri.ValueKind != JsonValueKind.String
                || uri.GetString() is not { Length: > 0 } logoutUri)
            {
                continue;
            }

            messages.Add(new OutboxMessageDraft(
                BackChannelLogoutHandler.MessageType,
                JsonSerializer.Serialize(new BackChannelLogoutPayload(issuer, clientId, userId.ToString(), logoutUri)),
                $"{clientId}: {logoutUri}"));
        }

        await _outbox.EnqueueAsync(messages, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The issuer the tokens name: the configured one, else this request's base address (as OpenIddict does).</summary>
    private string? Issuer()
    {
        if (_server.CurrentValue.Issuer is { } configured)
        {
            return configured.AbsoluteUri;
        }

        var request = _httpContext.HttpContext?.Request;

        return request is null ? null : $"{request.Scheme}://{request.Host}{request.PathBase}/";
    }
}

public sealed record BackChannelLogoutPayload(string Issuer, string ClientId, string Subject, string LogoutUri);

/// <summary>Signs a fresh logout token for each attempt and posts it to the application (outbox type <c>backchannel_logout</c>).</summary>
public sealed partial class BackChannelLogoutHandler : IOutboxHandler
{
    public const string MessageType = "backchannel_logout";

    public const string HttpClientName = "OpenPortal.BackChannelLogout";

    /// <summary>Kept in step with <c>OidcClientRegistry.BackChannelLogoutProperty</c> in the Access module.</summary>
    public const string LogoutUriProperty = "backchannel_logout_uri";

    private const string LogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    private readonly IHttpClientFactory _http;
    private readonly IOptionsMonitor<OpenIddictServerOptions> _server;
    private readonly TimeProvider _time;

    public BackChannelLogoutHandler(IHttpClientFactory http, IOptionsMonitor<OpenIddictServerOptions> server, TimeProvider time)
    {
        _http = http;
        _server = server;
        _time = time;
    }

    public string Type => MessageType;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<BackChannelLogoutPayload>(payload)
            ?? throw new OutboxPermanentFailureException("The queued logout notice is empty.");

        var token = CreateLogoutToken(message);

        using var client = _http.CreateClient(HttpClientName);
        using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("logout_token", token)]);
        using var response = await client.PostAsync(new Uri(message.LogoutUri), content, cancellationToken).ConfigureAwait(false);

        // 200 is what the specification asks for; any 2xx means the application took it.
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"The application answered {(int)response.StatusCode} to the logout token.", null, response.StatusCode);
        }
    }

    /// <summary>
    /// The logout token (section 2.4): iss, aud, iat, exp, jti, sub and the logout event, never a nonce, signed with
    /// the portal's token signing key and typed <c>logout+jwt</c>.
    /// </summary>
    public string CreateLogoutToken(BackChannelLogoutPayload message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var credentials = _server.CurrentValue.SigningCredentials.FirstOrDefault()
            ?? throw new InvalidOperationException("The OpenID Connect server has no signing key.");

        var now = _time.GetUtcNow();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = message.Issuer,
            Audience = message.ClientId,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(2).UtcDateTime,
            SigningCredentials = credentials,
            TokenType = "logout+jwt",
            Subject = new ClaimsIdentity([new Claim(Claims.Subject, message.Subject)]),
            Claims = new Dictionary<string, object>
            {
                [Claims.JwtId] = Guid.NewGuid().ToString("N"),
                ["events"] = new Dictionary<string, object> { [LogoutEvent] = new Dictionary<string, object>() },
            },
        };

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }
}
