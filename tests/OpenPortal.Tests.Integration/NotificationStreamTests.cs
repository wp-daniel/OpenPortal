using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Web.Notifications;

namespace OpenPortal.Tests.Integration;

/// <summary>The bell's event stream: the unread count at once, and again whenever the user's notifications change.</summary>
public sealed class NotificationStreamTests : IClassFixture<OpenPortalFactory>
{
    private readonly OpenPortalFactory _factory;

    public NotificationStreamTests(OpenPortalFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task The_stream_sends_the_count_now_and_after_each_change()
    {
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("stream"), Scenario.Password);
        using var client = await Scenario.SignedInAsync(_factory, user);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/notifications/stream");
        using var response = await client.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));

        (await NextEventAsync(reader, timeout.Token)).ShouldContain("\"unreadCount\":0");

        // Something changes for this user (on any instance): the stream speaks up without being asked.
        await _factory.Services.GetRequiredService<NotificationHub>().PublishAsync([user.Id], CancellationToken.None);

        (await NextEventAsync(reader, timeout.Token)).ShouldContain("unreadCount");
    }

    [Fact]
    public async Task The_stream_needs_a_signed_in_user()
    {
        using var anonymous = await Scenario.ClientAsync(_factory);

        using var response = await anonymous.GetAsync("/api/notifications/stream");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<string> NextEventAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        string? line;
        var data = string.Empty;
        var isCount = false;

        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (line.StartsWith("event: count", StringComparison.Ordinal))
            {
                isCount = true;
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                data = line["data: ".Length..];
            }
            else if (line.Length == 0 && isCount)
            {
                return data;
            }
        }

        throw new InvalidOperationException("The stream ended.");
    }
}
