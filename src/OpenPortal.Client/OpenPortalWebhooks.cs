using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace OpenPortal.Client;

/// <summary>An event the portal posted to this application's webhook.</summary>
/// <param name="Id">The delivery's id (<c>webhook-id</c>): the same on a retry, so it can be used to ignore duplicates.</param>
/// <param name="Type">The event, for example <c>access.revoked</c> or <c>user.updated</c>.</param>
/// <param name="Data">The event's subject (<c>data.subject.id</c> is the user or group id) and details.</param>
public sealed record OpenPortalWebhookEvent(string Id, string Type, DateTimeOffset Timestamp, JsonElement Data);

/// <summary>Receives the portal's webhooks (Standard Webhooks signatures).</summary>
public static class OpenPortalWebhooks
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Checks a delivery: the HMAC-SHA256 signature over <c>id.timestamp.body</c> with the <c>whsec_</c> secret, and a
    /// timestamp within five minutes (replays of an old delivery are refused).
    /// </summary>
    public static bool Verify(string secret, string? id, string? timestamp, string? signature, string body, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(secret);

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(signature)
            || !long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            || ((now ?? DateTimeOffset.UtcNow) - DateTimeOffset.FromUnixTimeSeconds(seconds)).Duration() > Tolerance)
        {
            return false;
        }

        var key = Convert.FromBase64String(secret.StartsWith("whsec_", StringComparison.Ordinal) ? secret[6..] : secret);
        var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{body}"));

        // Several signatures may be sent while a secret is rotated: any valid one will do.
        foreach (var candidate in signature.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = candidate.Split(',', 2);
            if (parts.Length == 2 && parts[0] == "v1")
            {
                try
                {
                    if (CryptographicOperations.FixedTimeEquals(expected, Convert.FromBase64String(parts[1])))
                    {
                        return true;
                    }
                }
                catch (FormatException)
                {
                    // Not base64: not ours.
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Maps the endpoint the portal posts events to: verified with <paramref name="secret"/> (the one generated in the
    /// application's settings in the portal), then handed to <paramref name="handler"/>. Answers 401 to anything
    /// unsigned, 200 once the handler is done; an exception makes the portal retry later.
    /// </summary>
    public static IEndpointConventionBuilder MapOpenPortalWebhooks(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string secret,
        Func<OpenPortalWebhookEvent, HttpContext, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrEmpty(secret);
        ArgumentNullException.ThrowIfNull(handler);

        return endpoints.MapPost(pattern, (Delegate)(async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);

            var id = context.Request.Headers["webhook-id"].ToString();
            var timestamp = context.Request.Headers["webhook-timestamp"].ToString();

            if (!Verify(secret, id, timestamp, context.Request.Headers["webhook-signature"].ToString(), body))
            {
                return Results.Unauthorized();
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            await handler(
                    new OpenPortalWebhookEvent(
                        id,
                        root.GetProperty("type").GetString() ?? string.Empty,
                        root.GetProperty("timestamp").GetDateTimeOffset(),
                        root.GetProperty("data").Clone()),
                    context)
                .ConfigureAwait(false);

            return Results.Ok();
        })).AllowAnonymous().DisableAntiforgery();
    }
}
