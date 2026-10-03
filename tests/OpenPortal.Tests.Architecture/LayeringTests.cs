using System.Reflection;
using NetArchTest.Rules;

namespace OpenPortal.Tests.Architecture;

/// <summary>
/// Enforces the dependency rules the design depends on.
/// <para>
/// These rules are the reason the layering exists, and they are cheap to break by accident: adding
/// <c>using Microsoft.EntityFrameworkCore;</c> to a domain file compiles, passes review and quietly lets a
/// domain entity become a data-access concern. A test fails faster and says why.
/// </para>
/// </summary>
public static class Architecture
{
    public static Assembly SharedKernel { get; } = typeof(SharedKernel.Results.Result).Assembly;

    public static Assembly IdentityDomain { get; } = typeof(Identity.Domain.Users.ApplicationUser).Assembly;

    public static Assembly IdentityApplication { get; } = typeof(Identity.Application.Abstractions.IAuthenticationService).Assembly;

    public static Assembly IdentityInfrastructure { get; } = typeof(Identity.Infrastructure.Persistence.IdentityDbContext).Assembly;

    public static Assembly ContentDomain { get; } = typeof(Content.Domain.Profiles.Profile).Assembly;

    public static Assembly ContentApplication { get; } = typeof(Content.Application.Abstractions.IPublicContentService).Assembly;

    public static Assembly ContentInfrastructure { get; } = typeof(Content.Infrastructure.Persistence.ContentDbContext).Assembly;

    public static Assembly Web { get; } = typeof(OpenPortal.Web.AntiforgeryDefaults).Assembly;
}

public sealed class LayeringTests
{
    // -----------------------------------------------------------------------
    // The domain must not know that a database exists.
    // -----------------------------------------------------------------------

    [Fact]
    public void Domain_projects_do_not_reference_EntityFrameworkCore()
    {
        var offenders = new[] { Architecture.IdentityDomain, Architecture.ContentDomain }
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Select(reference => reference.Name)
            .Where(name => name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            .Distinct()
            .ToArray();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void Domain_projects_do_not_reference_AspNetCore()
    {
        var offenders = new[] { Architecture.IdentityDomain, Architecture.ContentDomain }
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .Distinct()
            .ToArray();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void Application_projects_do_not_reference_EntityFrameworkCore()
    {
        var offenders = new[] { Architecture.IdentityApplication, Architecture.ContentApplication }
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Select(reference => reference.Name)
            .Where(name => name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            .Distinct()
            .ToArray();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void Application_projects_do_not_reference_their_own_infrastructure()
    {
        var pairs = new (Assembly Application, Assembly Infrastructure)[]
        {
            (Architecture.IdentityApplication, Architecture.IdentityInfrastructure),
            (Architecture.ContentApplication, Architecture.ContentInfrastructure),
        };

        foreach (var (application, infrastructure) in pairs)
        {
            var referenced = application.GetReferencedAssemblies().Select(reference => reference.Name);

            referenced.ShouldNotContain(infrastructure.GetName().Name);
        }
    }

    // -----------------------------------------------------------------------
    // The modules must not know about each other.
    // -----------------------------------------------------------------------

    [Fact]
    public void Identity_does_not_reference_Content()
    {
        // If Identity ever needs something from Content, that is a sign the shared concept belongs in the
        // shared kernel, not that a module-to-module reference is acceptable.
        foreach (var assembly in new[] { Architecture.IdentityDomain, Architecture.IdentityApplication, Architecture.IdentityInfrastructure })
        {
            assembly.GetReferencedAssemblies().Select(reference => reference.Name)
                .ShouldNotContain(Architecture.ContentDomain.GetName().Name);
        }
    }

    [Fact]
    public void Content_does_not_reference_Identity()
    {
        foreach (var assembly in new[] { Architecture.ContentDomain, Architecture.ContentApplication, Architecture.ContentInfrastructure })
        {
            assembly.GetReferencedAssemblies().Select(reference => reference.Name)
                .ShouldNotContain(Architecture.IdentityDomain.GetName().Name);
        }
    }

    // -----------------------------------------------------------------------
    // The shared kernel stays small and dependency-free.
    // -----------------------------------------------------------------------

    [Fact]
    public void SharedKernel_references_no_framework_or_module_assembly()
    {
        var forbidden = new[]
        {
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore.Identity",
            "Microsoft.AspNetCore.Mvc",
            Architecture.IdentityDomain.GetName().Name,
            Architecture.IdentityApplication.GetName().Name,
            Architecture.ContentDomain.GetName().Name,
            Architecture.ContentApplication.GetName().Name,
        };

        var referenced = Architecture.SharedKernel
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToArray();

        referenced.Where(name => forbidden.Contains(name, StringComparer.Ordinal)).ShouldBeEmpty();
    }

    [Fact]
    public void Only_the_web_host_and_infrastructure_projects_use_EntityFrameworkCore()
    {
        // Naming rule rather than a reference check: it catches a stray DbContext placed in an Application
        // project even when that project has not yet taken a package reference to make it compile.
        var offenders = new[] { Architecture.IdentityApplication, Architecture.ContentApplication }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("DbContext", StringComparison.Ordinal)
                || type.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Select(type => type.FullName!)
            .ToArray();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void Module_services_are_hidden_behind_their_application_interfaces()
    {
        // Every service implementation is internal, so nothing outside its own module can name the concrete
        // type. The only way to obtain one is through the interface the Application layer published, which is
        // what makes the module substitutable and what stops the host taking a shortcut to the concrete type.
        var implementations = new[] { Architecture.ContentInfrastructure, Architecture.IdentityInfrastructure }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("Service", StringComparison.Ordinal))
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .ToArray();

        implementations.ShouldNotBeEmpty();

        foreach (var implementation in implementations)
        {
            implementation.IsPublic.ShouldBeFalse(
                $"{implementation.FullName} is public; the host should only ever see its interface.");

            implementation.GetInterfaces().ShouldNotBeEmpty(
                $"{implementation.FullName} implements no contract, so there is nothing for the host to resolve.");
        }
    }

    [Fact]
    public void Modules_are_composed_through_a_single_public_extension_entry_point()
    {
        // The host references the Infrastructure projects, so their registration surface is the seam the host
        // actually binds to. One public AddXModule per module keeps that seam discoverable.
        var entryPoints = new[] { Architecture.ContentInfrastructure, Architecture.IdentityInfrastructure }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsPublic: true, IsAbstract: true, IsSealed: true })
            .Where(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(method => method.Name.StartsWith("Add", StringComparison.Ordinal)
                    && method.Name.EndsWith("Module", StringComparison.Ordinal)))
            .Select(type => type.FullName!)
            .ToArray();

        entryPoints.ShouldNotBeEmpty();
    }
}

public sealed class NamingConventionTests
{
    [Fact]
    public void DbContexts_live_only_in_infrastructure_projects()
    {
        var allowed = new[]
        {
            Architecture.IdentityInfrastructure.GetName().Name!,
            Architecture.ContentInfrastructure.GetName().Name!,
        };

        var owningAssemblies = new[]
            {
                Architecture.IdentityDomain, Architecture.IdentityApplication, Architecture.IdentityInfrastructure,
                Architecture.ContentDomain, Architecture.ContentApplication, Architecture.ContentInfrastructure,
                Architecture.Web,
            }
            .Where(assembly => assembly.GetTypes().Any(type => type.Name.EndsWith("DbContext", StringComparison.Ordinal)))
            .Select(assembly => assembly.GetName().Name!)
            .ToArray();

        foreach (var assembly in owningAssemblies)
        {
            allowed.ShouldContain(assembly);
        }
    }

    [Fact]
    public void Controllers_are_sealed()
    {
        // A sealed controller cannot be subclassed to add behaviour, which is how a controller quietly grows
        // a second, differently-authenticated entry point.
        var offenders = Architecture.Web
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Where(type => !type.IsSealed)
            .Select(type => type.FullName!)
            .ToArray();

        offenders.ShouldBeEmpty();
    }
}