using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Bootstrap;
using OpenPortal.Identity.Infrastructure.Configuration;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.Identity.Infrastructure.Services;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the Identity module. A host that wants authentication calls this once and gets the whole
/// capability: user store, sign-in manager, cookie scheme, role provisioning and the application services.
/// </summary>
public static class IdentityModuleServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Identity module: user store, sign-in manager, cookie scheme, role provisioning and the
    /// application services.
    /// <para>
    /// The context is registered without a provider and the host supplies one, exactly as for every other
    /// module. That is what keeps the SQLite-to-PostgreSQL switch confined to the composition root.
    /// </para>
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">Supplies the <c>Identity</c> section.</param>
    /// <param name="configureContext">
    /// Callback that configures <see cref="IdentityDbContext"/> against the provider the host selected, for
    /// example <c>options =&gt; options.UseSqlite(connectionString)</c>.
    /// </param>
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder> configureContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configureContext);

        services.AddOptions<IdentityModuleOptions>()
            .Bind(configuration.GetSection(IdentityModuleOptions.SectionName));

        services.AddDbContext<IdentityDbContext>(configureContext);

        // SignInManager and the ICurrentUser implementation both read the current HttpContext.
        services.AddHttpContextAccessor();

        services.AddScoped<UserLookup>();
        services.AddScoped<IAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<IAccountService, IdentityAccountService>();
        services.AddScoped<IUserAdministrationService, IdentityUserAdministrationService>();
        services.AddScoped<IUserLookupService, IdentityUserLookupService>();
        services.AddScoped<IdentityDataSeeder>();

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IClock)))
        {
            services.AddSingleton<IClock, SystemClock>();
        }

        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // Registered after AddIdentityCore so these values take precedence over its defaults.
        services.AddOptions<IdentityOptions>()
            .Configure<IOptions<IdentityModuleOptions>>((identity, module) => ApplyIdentityOptions(identity, module.Value));

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        // Configured per scheme so the values come from the same "Identity" section as everything else.
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<IdentityModuleOptions>>(
                (cookie, module) => ApplyCookieOptions(cookie, module.Value));

        return services;
    }

    private static void ApplyIdentityOptions(IdentityOptions identity, IdentityModuleOptions module)
    {
        identity.User.RequireUniqueEmail = true;
        // StoreOptions no longer exposes UserIdType/RoleType in .NET 10; the key type is fixed by the
        // ApplicationUser : IdentityUser<Guid> declaration and by AddRoles<IdentityRole<Guid>>().
        identity.Stores.MaxLengthForKeys = 450;

        // StoreOptions.ProtectPersonalData is deliberately left off. Turning it on makes the user store
        // require IPersonalDataProtector, whose implementation depends on ILookupProtectorKeyRing - a type
        // with no public implementation or registration extension. The protection it adds covers the
        // security stamp inside authenticator-app and email-change tokens, neither of which this host uses.

        identity.Password.RequiredLength = module.Password.RequiredLength;
        identity.Password.RequiredUniqueChars = module.Password.RequiredUniqueChars;
        identity.Password.RequireLowercase = module.Password.RequireLowercase;
        identity.Password.RequireUppercase = module.Password.RequireUppercase;
        identity.Password.RequireDigit = module.Password.RequireDigit;
        identity.Password.RequireNonAlphanumeric = module.Password.RequireNonAlphanumeric;

        identity.Lockout.AllowedForNewUsers = module.Lockout.Enabled;
        identity.Lockout.MaxFailedAccessAttempts = module.Lockout.MaxFailedAccessAttempts;
        identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(module.Lockout.DefaultLockoutMinutes);

        identity.SignIn.RequireConfirmedAccount = module.SignIn.RequireConfirmedAccount;
    }

    private static void ApplyCookieOptions(CookieAuthenticationOptions cookie, IdentityModuleOptions module)
    {
        ArgumentNullException.ThrowIfNull(module);

        cookie.Cookie.Name = module.SignIn.CookieName;
        cookie.Cookie.HttpOnly = true;
        cookie.Cookie.SameSite = SameSiteMode.Lax;
        cookie.Cookie.SecurePolicy = module.Cookie.RequireSecure
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        cookie.ExpireTimeSpan = TimeSpan.FromHours(module.SignIn.CookieLifetimeHours);
        cookie.SlidingExpiration = true;

        // The SPA's sign-in route, used only by the OpenID Connect endpoints below.
        cookie.LoginPath = "/sign-in";
        cookie.ReturnUrlParameter = "returnUrl";

        // This host serves a JSON API consumed by a SPA. A missing or insufficient session must produce a
        // status code, not a redirect to an HTML login page.
        //
        // The exception is the OpenID Connect authorization endpoint: another application sent the browser
        // there, so a real page has to be shown. It is redirected to the SPA's sign-in route, which returns
        // to the endpoint once the user has signed in.
        cookie.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/connect", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        cookie.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }
}