using Microsoft.Extensions.DependencyInjection;
using OpenPortal.Content.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// The architecture tests assert that module services are internal, which means the only way a host can
/// obtain one is by resolving its published interface. Nothing else in the suite proves those registrations
/// actually exist, so this test does: it resolves every module contract from the real composition root.
/// <para>
/// A missing registration here means a controller or middleware fails only when that specific endpoint is
/// first called in production.
/// </para>
/// </summary>
public sealed class CompositionTests(OpenPortalFactory factory) : IClassFixture<OpenPortalFactory>
{
    public static TheoryData<Type> ModuleContracts() => new()
    {
        typeof(IAuthenticationService),
        typeof(IAccountService),
        typeof(IUserAdministrationService),
        typeof(IPublicContentService),
        typeof(IContentManagementService),
        typeof(ILanguageCatalog),
    };

    [Theory]
    [MemberData(nameof(ModuleContracts))]
    public async Task Every_module_contract_resolves_from_the_composition_root(Type contract)
    {
        await using var provider = factory.Services.CreateAsyncScope();

        var instance = provider.ServiceProvider.GetService(contract);

        instance.ShouldNotBeNull($"{contract.Name} has no registration; the host cannot build without it.");
    }

    [Fact]
    public async Task Module_services_are_resolvable_for_every_request_scope()
    {
        // A registration that only works in the root scope would be scoped-capture or captive-dependency
        // trouble: it would appear to work here and then leak state between requests in production.
        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();

        var a = first.ServiceProvider.GetRequiredService<IPublicContentService>();
        var b = second.ServiceProvider.GetRequiredService<IPublicContentService>();

        a.ShouldNotBeSameAs(b);
    }
}