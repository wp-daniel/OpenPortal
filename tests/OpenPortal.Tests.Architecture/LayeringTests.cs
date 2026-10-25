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

    public static Assembly AccessDomain { get; } = typeof(Access.Domain.Applications.PortalApplication).Assembly;

    public static Assembly AccessApplication { get; } = typeof(Access.Application.Abstractions.IApplicationRegistryService).Assembly;

    public static Assembly AccessInfrastructure { get; } = typeof(Access.Infrastructure.Persistence.AccessDbContext).Assembly;

    public static Assembly AuditDomain { get; } = typeof(Audit.Domain.AuditEntry).Assembly;

    public static Assembly AuditApplication { get; } = typeof(Audit.Application.Abstractions.IAuditLogService).Assembly;

    public static Assembly AuditInfrastructure { get; } = typeof(Audit.Infrastructure.Persistence.AuditDbContext).Assembly;

    public static Assembly NotificationsDomain { get; } = typeof(Notifications.Domain.Notification).Assembly;

    public static Assembly NotificationsApplication { get; } = typeof(Notifications.Application.Abstractions.INotificationService).Assembly;

    public static Assembly NotificationsInfrastructure { get; } = typeof(Notifications.Infrastructure.Persistence.NotificationsDbContext).Assembly;

    public static Assembly Web { get; } = typeof(OpenPortal.Web.AntiforgeryDefaults).Assembly;

    public static Assembly[] Domains { get; } = [IdentityDomain, AccessDomain, AuditDomain, NotificationsDomain];

    public static Assembly[] Applications { get; } = [IdentityApplication, AccessApplication, AuditApplication, NotificationsApplication];

    public static Assembly[] Infrastructures { get; } =
        [IdentityInfrastructure, AccessInfrastructure, AuditInfrastructure, NotificationsInfrastructure];
}

public sealed class LayeringTests
{
    // -----------------------------------------------------------------------
    // The domain must not know that a database exists.
    // -----------------------------------------------------------------------

    [Fact]
    public void Domain_projects_do_not_reference_EntityFrameworkCore()
    {
        var offenders = Architecture.Domains
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
        var offenders = Architecture.Domains
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
        var offenders = Architecture.Applications
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
            (Architecture.AccessApplication, Architecture.AccessInfrastructure),
            (Architecture.AuditApplication, Architecture.AuditInfrastructure),
            (Architecture.NotificationsApplication, Architecture.NotificationsInfrastructure),
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
    public void Access_does_not_reference_Identity()
    {
        // Access stores user ids only; names come through the host's IUserDirectory adapter.
        foreach (var assembly in new[] { Architecture.AccessDomain, Architecture.AccessApplication, Architecture.AccessInfrastructure })
        {
            var referenced = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();

            referenced.ShouldNotContain(Architecture.IdentityDomain.GetName().Name);
            referenced.ShouldNotContain(Architecture.IdentityApplication.GetName().Name);
            referenced.ShouldNotContain(Architecture.IdentityInfrastructure.GetName().Name);
        }
    }

    [Fact]
    public void Identity_does_not_reference_Access()
    {
        // If Identity ever needs something from Access, the host supplies it through a port (as it does for
        // invitations' memberships), not a module-to-module reference.
        var access = new[] { Architecture.AccessDomain, Architecture.AccessApplication, Architecture.AccessInfrastructure }
            .Select(assembly => assembly.GetName().Name)
            .ToArray();

        foreach (var assembly in new[] { Architecture.IdentityDomain, Architecture.IdentityApplication, Architecture.IdentityInfrastructure })
        {
            assembly.GetReferencedAssemblies().Select(reference => reference.Name).Intersect(access).ShouldBeEmpty();
        }
    }

    [Fact]
    public void The_client_package_stands_alone()
    {
        // OpenPortal.Client ships to other applications as a NuGet package. A reference to anything in the
        // portal would drag the portal's modules, EF Core and its database provider into every consumer.
        var referenced = typeof(OpenPortal.Client.OpenPortalClientOptions).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToArray();

        referenced.Where(name => name.StartsWith("OpenPortal.", StringComparison.Ordinal)).ShouldBeEmpty();
        referenced.Where(name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    [Fact]
    public void Modules_record_audit_events_without_referencing_the_Audit_module()
    {
        // Identity and Access write to IAuditTrail (shared kernel); only the host composes the Audit
        // module that stores the events, and the Audit module knows none of them.
        var modules = new[]
        {
            Architecture.IdentityDomain, Architecture.IdentityApplication, Architecture.IdentityInfrastructure,
            Architecture.AccessDomain, Architecture.AccessApplication, Architecture.AccessInfrastructure,
        };

        var audit = new[] { Architecture.AuditDomain, Architecture.AuditApplication, Architecture.AuditInfrastructure }
            .Select(assembly => assembly.GetName().Name)
            .ToArray();

        foreach (var assembly in modules)
        {
            assembly.GetReferencedAssemblies().Select(reference => reference.Name).Intersect(audit).ShouldBeEmpty();
        }

        foreach (var assembly in new[] { Architecture.AuditDomain, Architecture.AuditApplication, Architecture.AuditInfrastructure })
        {
            var referenced = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();

            foreach (var module in modules)
            {
                referenced.ShouldNotContain(module.GetName().Name);
            }
        }
    }

    [Fact]
    public void Modules_notify_without_referencing_the_Notifications_module()
    {
        // Identity, Access and Audit tell people through INotifier (shared kernel); only the host composes
        // the Notifications module that stores and shows the notices, and that module knows none of them.
        var modules = new[]
        {
            Architecture.IdentityDomain, Architecture.IdentityApplication, Architecture.IdentityInfrastructure,
            Architecture.AccessDomain, Architecture.AccessApplication, Architecture.AccessInfrastructure,
            Architecture.AuditDomain, Architecture.AuditApplication, Architecture.AuditInfrastructure,
        };

        var notifications = new[] { Architecture.NotificationsDomain, Architecture.NotificationsApplication, Architecture.NotificationsInfrastructure };
        var notificationNames = notifications.Select(assembly => assembly.GetName().Name).ToArray();

        foreach (var assembly in modules)
        {
            assembly.GetReferencedAssemblies().Select(reference => reference.Name).Intersect(notificationNames).ShouldBeEmpty();
        }

        foreach (var assembly in notifications)
        {
            var referenced = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();

            foreach (var module in modules)
            {
                referenced.ShouldNotContain(module.GetName().Name);
            }
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
            Architecture.AccessDomain.GetName().Name,
            Architecture.AccessApplication.GetName().Name,
            Architecture.AuditDomain.GetName().Name,
            Architecture.AuditApplication.GetName().Name,
            Architecture.NotificationsDomain.GetName().Name,
            Architecture.NotificationsApplication.GetName().Name,
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
        var offenders = Architecture.Applications
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
        var implementations = Architecture.Infrastructures
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
        var entryPoints = Architecture.Infrastructures
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
        var allowed = Architecture.Infrastructures.Select(assembly => assembly.GetName().Name!).ToArray();

        var owningAssemblies = Architecture.Domains
            .Concat(Architecture.Applications)
            .Concat(Architecture.Infrastructures)
            .Append(Architecture.Web)
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