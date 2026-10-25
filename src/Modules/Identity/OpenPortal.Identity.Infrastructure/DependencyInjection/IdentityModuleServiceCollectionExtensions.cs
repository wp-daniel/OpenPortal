using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Bootstrap;
using OpenPortal.Identity.Infrastructure.Configuration;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.Identity.Infrastructure.Services;
using OpenPortal.SharedKernel.Privacy;
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
        services.AddScoped<AdministrationGuard>();
        services.AddScoped<IAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<IAccountService, IdentityAccountService>();
        services.AddScoped<IUserAdministrationService, IdentityUserAdministrationService>();
        services.AddScoped<IdentityAccountErasure>();
        services.AddScoped<IdentityLegalDocumentService>();
        services.AddScoped<ILegalDocumentService>(provider => provider.GetRequiredService<IdentityLegalDocumentService>());
        services.AddScoped<PrivacySettingsStore>();
        services.AddScoped<IPrivacySettingsService, IdentityPrivacySettingsService>();
        services.AddScoped<IAccountPrivacyService, IdentityAccountPrivacyService>();
        services.AddScoped<IImpersonationService, IdentityImpersonationService>();
        services.AddScoped<PersonalAccessTokenStore>();
        services.AddScoped<IdentityPersonalAccessTokenService>();
        services.AddScoped<IPersonalAccessTokenService>(provider => provider.GetRequiredService<IdentityPersonalAccessTokenService>());
        services.AddScoped<IPersonalAccessTokenValidator>(provider => provider.GetRequiredService<IdentityPersonalAccessTokenService>());
        services.AddScoped<IPersonalDataSource, IdentityPersonalData>();
        services.AddScoped<IAccountErasure>(provider => provider.GetRequiredService<IdentityAccountErasure>());
        services.AddScoped<IUserLookupService, IdentityUserLookupService>();
        services.AddScoped<IUserAvatarService, IdentityUserAvatarService>();
        services.AddScoped<ITwoFactorService, IdentityTwoFactorService>();
        services.AddScoped<ISecuritySettingsService, IdentitySecuritySettingsService>();
        services.AddScoped<SecuritySettingsStore>();
        services.AddScoped<RegistrationSettingsStore>();
        services.AddScoped<IRegistrationSettingsService, IdentityRegistrationSettingsService>();
        services.AddScoped<IRegistrationService, IdentityRegistrationService>();
        services.AddScoped<IPasswordRecoveryService, IdentityPasswordRecoveryService>();
        services.AddOptions<PasswordResetTokenProviderOptions>();
        services.AddScoped<IInvitationService, IdentityInvitationService>();
        services.AddScoped<IUserStatisticsService, UserStatisticsService>();
        services.AddSingleton<ExternalSecretProtector>();
        services.AddSingleton<ExternalLoginDirectory>();
        services.AddSingleton<IExternalLoginProviders>(provider => provider.GetRequiredService<ExternalLoginDirectory>());
        services.AddScoped<IExternalLoginSettingsService, IdentityExternalLoginSettingsService>();
        services.AddScoped<IExternalLoginService, IdentityExternalLoginService>();
        services.AddOptions<SessionStoreOptions>();
        services.AddSingleton<UserSessionTicketStore>();
        services.AddScoped<UserSessions>();
        services.AddScoped<ISessionService, IdentitySessionService>();
        services.AddHostedService<SessionRetentionService>();
        services.AddScoped<IdentityDataSeeder>();

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<OpenPortal.SharedKernel.Jobs.IJobRegistry, OpenPortal.SharedKernel.Jobs.JobRegistry>();

        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            // Asks for the second factor only while the portal's security setting has it on.
            .AddSignInManager<PortalSignInManager>()

            // Includes the authenticator (TOTP) provider: keys live in the user store, nothing external.
            .AddDefaultTokenProviders()

            // Reset links last an hour, not the day the default provider gives every token.
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProviderOptions.ProviderName);

        // Registered after AddIdentityCore so these values take precedence over its defaults.
        services.AddOptions<IdentityOptions>()
            .Configure<IOptions<IdentityModuleOptions>>((identity, module) => ApplyIdentityOptions(identity, module.Value));

        // Passkeys must be discoverable (no user name to type) and verify the person (PIN or biometrics), which is
        // what lets a passkey count as two factors on its own.
        services.AddOptions<IdentityPasskeyOptions>()
            .Configure<IOptions<IdentityModuleOptions>>((passkeys, module) =>
            {
                var policy = module.Value.Passkeys;
                passkeys.ResidentKeyRequirement = "required";
                passkeys.UserVerificationRequirement = "required";
                passkeys.ServerDomain = string.IsNullOrWhiteSpace(policy.ServerDomain) ? null : policy.ServerDomain.Trim();

                var allowed = policy.AllowedOrigins
                    .Where(origin => !string.IsNullOrWhiteSpace(origin))
                    .Select(origin => origin.Trim().TrimEnd('/'))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (allowed.Count > 0)
                {
                    passkeys.ValidateOrigin = context => ValueTask.FromResult(!context.CrossOrigin && allowed.Contains(context.Origin));
                }
            });

        services.AddScoped<IPasskeyService, IdentityPasskeyService>();
        services.AddScoped<IdentityTwoFactorPolicy>();
        services.AddScoped<ITwoFactorPolicy>(provider => provider.GetRequiredService<IdentityTwoFactorPolicy>());

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        // Configured per scheme so the values come from the same "Identity" section as everything else.
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<IdentityModuleOptions>, UserSessionTicketStore>(
                (cookie, module, sessions) =>
                {
                    ApplyCookieOptions(cookie, module.Value);

                    // The ticket lives in the database and the cookie carries only its id: sessions can be listed and ended.
                    cookie.SessionStore = sessions;
                });

        // A support session keeps who is behind it when the security stamp is re-validated: the refreshed principal
        // is rebuilt from the impersonated account alone, and losing the marker would leave the administrator
        // inside that account as if it were their own.
        services.AddOptions<SecurityStampValidatorOptions>()
            .Configure<IOptions<IdentityModuleOptions>>((options, module) =>
                options.ValidationInterval = TimeSpan.FromSeconds(Math.Max(0, module.Value.SignIn.SessionValidationSeconds)));

        services.Configure<SecurityStampValidatorOptions>(options =>
            options.OnRefreshingPrincipal = context =>
            {
                if (context.CurrentPrincipal?.Identity is not null && context.NewPrincipal?.Identity is ClaimsIdentity identity)
                {
                    // How the session was established (amr) is kept too: the rebuilt principal would otherwise lose it,
                    // and a two-factor sign-in would look like a password one to the applications after a minute.
                    foreach (var claim in context.CurrentPrincipal.Claims.Where(claim => PreservedClaims.Contains(claim.Type)))
                    {
                        identity.AddClaim(new Claim(claim.Type, claim.Value));
                    }
                }

                return Task.CompletedTask;
            });

        return services;
    }

    private static readonly HashSet<string> PreservedClaims =
    [
        PortalClaimTypes.ImpersonatorId,
        PortalClaimTypes.ImpersonatorEmail,
        PortalClaimTypes.ImpersonationStarted,
        AuthenticationMethods.ClaimType,
        ClaimTypes.AuthenticationMethod,
    ];

    private static void ApplyIdentityOptions(IdentityOptions identity, IdentityModuleOptions module)
    {
        identity.User.RequireUniqueEmail = true;
        // StoreOptions no longer exposes UserIdType/RoleType in .NET 10; the key type is fixed by the
        // ApplicationUser : IdentityUser<Guid> declaration and by AddRoles<IdentityRole<Guid>>().
        identity.Stores.MaxLengthForKeys = 450;

        // StoreOptions.ProtectPersonalData is deliberately left off. Turning it on makes the user store
        // require IPersonalDataProtector, whose implementation depends on ILookupProtectorKeyRing - a type
        // with no public implementation or registration extension. Authenticator keys and recovery codes are
        // therefore stored as the framework writes them, in the UserTokens table.

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

        identity.Tokens.PasswordResetTokenProvider = PasswordResetTokenProviderOptions.ProviderName;

        // Version 3 adds the passkeys table (WebAuthn credentials) to the Identity schema.
        identity.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
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

        // A support session ends by itself after Identity:ImpersonationMinutes: the cookie is refused and the
        // administrator signs in again as themselves. Then the usual security stamp check.
        var validateStamp = cookie.Events.OnValidatePrincipal;
        cookie.Events.OnValidatePrincipal = async context =>
        {
            var started = context.Principal?.FindFirst(PortalClaimTypes.ImpersonationStarted)?.Value;
            if (started is not null
                && (!long.TryParse(started, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                    || DateTimeOffset.FromUnixTimeSeconds(seconds).AddMinutes(module.ImpersonationMinutes) <= DateTimeOffset.UtcNow))
            {
                context.RejectPrincipal();
                await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(context.HttpContext, IdentityConstants.ApplicationScheme).ConfigureAwait(false);
                return;
            }

            await validateStamp(context).ConfigureAwait(false);
        };

        cookie.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }
}