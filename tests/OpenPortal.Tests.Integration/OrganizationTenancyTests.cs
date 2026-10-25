using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Web.Email;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The tenant side of organizations: roles a member holds in an application within one organization, email domains an
/// organization proves it owns and lets people join by, its own two-factor rule, and its administrators inviting people
/// in. DNS is not reachable from a test, so the domain check answers from a list the test fills.
/// </summary>
public sealed class OrganizationTenancyTests : IClassFixture<OpenPortalFactory>, IAsyncDisposable
{
    private const string Scope = "openid profile email offline_access organization roles";

    private readonly OpenPortalFactory _factory;
    private readonly WebApplicationFactory<Program> _host;
    private readonly FakeDns _dns = new();
    private readonly CapturingEmailSender _mail = new();

    public OrganizationTenancyTests(OpenPortalFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IDomainOwnershipVerifier>(_dns);
            services.AddSingleton<IEmailSender>(_mail);
        }));
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Roles_given_within_an_organization_reach_the_application_only_for_that_organization()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var (applicationId, clientId, secret) = await Scenario.CreateApplicationAsync(admin, new
        {
            organizations = "required",
            roles = new[]
            {
                new { key = "sales", displayName = (string?)"Sales", description = (string?)null },
                new { key = "viewer", displayName = (string?)"Viewer", description = (string?)null },
            },
        });

        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("tenant"), Scenario.Password);
        await PutOkAsync(admin, $"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { roles = new[] { "viewer" } });

        var acme = await CreateOrganizationAsync(admin, "Acme");
        var globex = await CreateOrganizationAsync(admin, "Globex");
        await PutOkAsync(admin, $"/api/admin/organizations/{acme.Id}/members/{user.Id}", new { role = "member" });
        await PutOkAsync(admin, $"/api/admin/organizations/{globex.Id}/members/{user.Id}", new { role = "member" });

        using (var set = await admin.PutAsync($"/api/admin/organizations/{acme.Id}/members/{user.Id}/applications/{applicationId}", new { roles = new[] { "sales" } }))
        {
            var detail = await Scenario.JsonAsync(set, HttpStatusCode.OK);
            detail.GetProperty("applications").EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(applicationId);
            var member = detail.GetProperty("members").EnumerateArray().Single();
            member.GetProperty("applicationRoles")[0].GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ShouldBe(["sales"]);
        }

        using (var unknown = await admin.PutAsync($"/api/admin/organizations/{acme.Id}/members/{user.Id}/applications/{applicationId}", new { roles = new[] { "root" } }))
        {
            unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var forAcme = await Scenario.SignInToApplicationAsync(_host, user, clientId, secret, Scope, acme.Slug);
        RolesOf(forAcme).ShouldBe(["sales", "viewer"]);

        var forGlobex = await Scenario.SignInToApplicationAsync(_host, user, clientId, secret, Scope, globex.Slug);
        RolesOf(forGlobex).ShouldBe(["viewer"]);

        // Taken away, the role is gone at the next refresh.
        await PutOkAsync(admin, $"/api/admin/organizations/{acme.Id}/members/{user.Id}/applications/{applicationId}", new { roles = Array.Empty<string>() });
        using var refreshed = await Scenario.RefreshAsync(_host, clientId, secret, forAcme.GetProperty("refresh_token").GetString()!);
        RolesOf(await Scenario.JsonAsync(refreshed, HttpStatusCode.OK)).ShouldBe(["viewer"]);
    }

    [Fact]
    public async Task A_verified_domain_lets_people_with_a_confirmed_address_join_by_themselves()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var organization = await CreateOrganizationAsync(admin, "Initech");
        var domainName = $"initech-{Guid.NewGuid():N}"[..20] + ".example";

        var added = await PostJsonAsync(admin, $"/api/admin/organizations/{organization.Id}/domains", new { domain = domainName.ToUpperInvariant() });
        var domain = added.GetProperty("domains").EnumerateArray().Single();
        domain.GetProperty("domain").GetString().ShouldBe(domainName);
        domain.GetProperty("verified").GetBoolean().ShouldBeFalse();
        var domainId = domain.GetProperty("id").GetGuid();
        var record = domain.GetProperty("recordValue").GetString()!;
        record.ShouldStartWith("openportal-verification=");

        // Joining by an unproven domain would let anyone in.
        using (var early = await admin.PutAsync($"/api/admin/organizations/{organization.Id}/domains/{domainId}", new { autoJoin = true }))
        {
            await Scenario.ExpectErrorAsync(early, HttpStatusCode.Conflict, "access.organization_domain_unverified");
        }

        using (var missing = await admin.PostAsync($"/api/admin/organizations/{organization.Id}/domains/{domainId}/verify", new { manual = false }))
        {
            await Scenario.ExpectErrorAsync(missing, HttpStatusCode.Conflict, "access.organization_domain_check_failed");
        }

        _dns.Publish(domainName, record);
        var verified = await PostJsonAsync(admin, $"/api/admin/organizations/{organization.Id}/domains/{domainId}/verify", new { manual = false });
        verified.GetProperty("domains")[0].GetProperty("verifiedBy").GetString().ShouldBe("dns");

        await PutOkAsync(admin, $"/api/admin/organizations/{organization.Id}/domains/{domainId}", new { autoJoin = true, role = "member" });

        // Another organization cannot claim the same domain.
        var other = await CreateOrganizationAsync(admin, "Copycat");
        using (var taken = await admin.PostAsync($"/api/admin/organizations/{other.Id}/domains", new { domain = domainName }))
        {
            await Scenario.ExpectErrorAsync(taken, HttpStatusCode.Conflict, "access.organization_domain_in_use");
        }

        var confirmed = await CreateUserAsync($"peter@{domainName}", emailConfirmed: true);
        var unconfirmed = await CreateUserAsync($"milton@{domainName}", emailConfirmed: false);

        using (var peter = await Scenario.SignedInAsync(_host, confirmed))
        {
            var mine = await Scenario.GetJsonAsync(peter, "/api/account/organizations");
            mine.EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(organization.Id);
            mine[0].GetProperty("role").GetString().ShouldBe("member");
        }

        using var milton = await Scenario.SignedInAsync(_host, unconfirmed);
        (await Scenario.GetJsonAsync(milton, "/api/account/organizations")).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Only_a_portal_administrator_can_vouch_for_a_domain_without_dns()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var organization = await CreateOrganizationAsync(admin, "Hooli");
        var tenantAdmin = await _factory.CreateUserAsync(Scenario.UniqueEmail("hooli-admin"), Scenario.Password);
        await PutOkAsync(admin, $"/api/admin/organizations/{organization.Id}/members/{tenantAdmin.Id}", new { role = "admin" });

        using var client = await Scenario.SignedInAsync(_host, tenantAdmin);
        var added = await PostJsonAsync(client, $"/api/account/organizations/{organization.Id}/domains", new { domain = $"hooli-{Guid.NewGuid():N}"[..18] + ".example" });
        var domainId = added.GetProperty("domains")[0].GetProperty("id").GetGuid();

        // The tenant's own administrator can only use DNS (the account endpoint never vouches).
        using (var dnsOnly = await client.PostAsync($"/api/account/organizations/{organization.Id}/domains/{domainId}/verify"))
        {
            await Scenario.ExpectErrorAsync(dnsOnly, HttpStatusCode.Conflict, "access.organization_domain_check_failed");
        }

        var vouched = await PostJsonAsync(admin, $"/api/admin/organizations/{organization.Id}/domains/{domainId}/verify", new { manual = true });
        vouched.GetProperty("domains")[0].GetProperty("verifiedBy").GetString().ShouldBe("administrator");
    }

    [Fact]
    public async Task An_organization_that_requires_two_factors_refuses_a_password_only_sign_in()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var (applicationId, clientId, _) = await Scenario.CreateApplicationAsync(admin, new { organizations = "required" });
        var user = await _factory.CreateUserAsync(Scenario.UniqueEmail("strict"), Scenario.Password);
        await PutOkAsync(admin, $"/api/admin/access/applications/{applicationId}/users/{user.Id}", new { });

        var organization = await CreateOrganizationAsync(admin, "Strict");
        await PutOkAsync(admin, $"/api/admin/organizations/{organization.Id}/members/{user.Id}", new { role = "member" });

        using (var policy = await admin.PutAsync($"/api/admin/organizations/{organization.Id}", new { name = "Strict", requireMultiFactor = true }))
        {
            (await Scenario.JsonAsync(policy, HttpStatusCode.OK)).GetProperty("requireMultiFactor").GetBoolean().ShouldBeTrue();
        }

        var (location, _) = await Scenario.AuthorizeAsync(_host, user, clientId, Scope);
        location.ToString().ShouldStartWith("/access-denied?reason=mfa");
    }

    [Fact]
    public async Task An_organization_administrator_adds_an_existing_account_and_invites_a_new_address()
    {
        var (admin, _) = await Scenario.AdminAsync(_factory, _host);
        var organization = await CreateOrganizationAsync(admin, "Umbrella");
        var tenantAdmin = await _factory.CreateUserAsync(Scenario.UniqueEmail("umbrella-admin"), Scenario.Password);
        await PutOkAsync(admin, $"/api/admin/organizations/{organization.Id}/members/{tenantAdmin.Id}", new { role = "admin" });
        var existing = await _factory.CreateUserAsync(Scenario.UniqueEmail("existing"), Scenario.Password);

        using var client = await Scenario.SignedInAsync(_host, tenantAdmin);

        var added = await PostJsonAsync(client, $"/api/account/organizations/{organization.Id}/invitations", new { email = existing.Email });
        added.GetProperty("outcome").GetString().ShouldBe("added");
        added.GetProperty("organization").GetProperty("members").EnumerateArray()
            .ShouldContain(member => member.GetProperty("id").GetGuid() == existing.Id);

        var newcomer = Scenario.UniqueEmail("newcomer");
        var invited = await PostJsonAsync(client, $"/api/account/organizations/{organization.Id}/invitations", new { email = newcomer, firstName = "New", lastName = "Comer" });
        invited.GetProperty("outcome").GetString().ShouldBe("invited");
        _mail.Messages.ShouldContain(message => message.To == newcomer);

        // Someone who administers nothing invites nobody.
        var outsider = await _factory.CreateUserAsync(Scenario.UniqueEmail("outsider"), Scenario.Password);
        using var outsiderClient = await Scenario.SignedInAsync(_host, outsider);
        using var refused = await outsiderClient.PostAsync($"/api/account/organizations/{organization.Id}/invitations", new { email = Scenario.UniqueEmail("x") });
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static IReadOnlyList<string?> RolesOf(JsonElement tokens)
    {
        var claims = Scenario.ReadJwtPayload(tokens.GetProperty("id_token").GetString()!);
        if (!claims.TryGetProperty("role", out var role))
        {
            return [];
        }

        return role.ValueKind == JsonValueKind.Array
            ? role.EnumerateArray().Select(value => value.GetString()).Order(StringComparer.Ordinal).ToList()
            : [role.GetString()];
    }

    private async Task<TestUser> CreateUserAsync(string email, bool emailConfirmed)
    {
        var user = await _factory.CreateUserAsync(email, Scenario.Password);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<OpenPortal.Identity.Domain.Users.ApplicationUser>>();
        var account = await users.FindByIdAsync(user.Id.ToString());
        account!.EmailConfirmed = emailConfirmed;
        (await users.UpdateAsync(account)).Succeeded.ShouldBeTrue();

        return user;
    }

    private static async Task<(Guid Id, string Slug)> CreateOrganizationAsync(ApiClient admin, string name)
    {
        var slug = $"org-{Guid.NewGuid():N}"[..16];
        using var created = await admin.PostAsync("/api/admin/organizations", new { name, slug });
        var json = await Scenario.JsonAsync(created, HttpStatusCode.Created);

        return (json.GetProperty("id").GetGuid(), slug);
    }

    private static async Task PutOkAsync(ApiClient client, string path, object body)
    {
        using var response = await client.PutAsync(path, body);
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> PostJsonAsync(ApiClient client, string path, object body)
    {
        using var response = await client.PostAsync(path, body);

        return await Scenario.JsonAsync(response, HttpStatusCode.OK);
    }

    /// <summary>Answers TXT lookups from the records a test published.</summary>
    private sealed class FakeDns : IDomainOwnershipVerifier
    {
        private readonly HashSet<(string Domain, string Value)> _records = [];

        public void Publish(string domain, string value)
        {
            lock (_records)
            {
                _records.Add((domain, value));
            }
        }

        public Task<bool> HasTxtRecordAsync(string domain, string value, CancellationToken cancellationToken)
        {
            lock (_records)
            {
                return Task.FromResult(_records.Contains((domain, value)));
            }
        }
    }
}
