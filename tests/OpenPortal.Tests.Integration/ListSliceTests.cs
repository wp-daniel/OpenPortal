using System.Net;

namespace OpenPortal.Tests.Integration;

/// <summary>Long lists (groups, organizations) read a page at a time: a search, a window, the total in a header.</summary>
public sealed class ListSliceTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    [Fact]
    public async Task Groups_are_searched_and_paged_on_the_server()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var marker = $"slice{Guid.NewGuid():N}"[..14];

        foreach (var index in Enumerable.Range(1, 5))
        {
            using var created = await admin.PostAsync("/api/admin/groups", new { name = $"{marker} team {index}", description = (string?)null });
            created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        }

        using var page = await admin.GetAsync($"/api/admin/groups?search={marker.ToUpperInvariant()}&skip=2&take=2");
        var items = await Scenario.JsonAsync(page, HttpStatusCode.OK);

        page.Headers.GetValues("X-Total-Count").Single().ShouldBe("5");
        items.EnumerateArray().Select(group => group.GetProperty("name").GetString()).ShouldBe([$"{marker} team 3", $"{marker} team 4"]);

        // Without parameters the whole list comes back, as the pickers expect.
        using var all = await admin.GetAsync("/api/admin/groups");
        (await Scenario.JsonAsync(all, HttpStatusCode.OK)).GetArrayLength().ShouldBeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Organizations_are_searched_by_name_or_short_name()
    {
        var (admin, _) = await Scenario.AdminAsync(factory);
        var slug = $"zz{Guid.NewGuid():N}"[..12];

        using (var created = await admin.PostAsync("/api/admin/organizations", new { name = "Findable Corp", slug }))
        {
            created.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        using var bySlug = await admin.GetAsync($"/api/admin/organizations?search={slug}&take=10");
        (await Scenario.JsonAsync(bySlug, HttpStatusCode.OK)).EnumerateArray().Single().GetProperty("slug").GetString().ShouldBe(slug);
        bySlug.Headers.GetValues("X-Total-Count").Single().ShouldBe("1");
    }
}
