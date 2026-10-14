using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>
/// The framework's sign-in manager, except that an account's second factor is only asked for while the portal
/// setting is on (turning the setting off therefore never strands an enrolled account, and turning it back on
/// restores every enrolment as it was), and that a self-registered account waiting for approval or for its
/// address to be confirmed cannot sign in.
/// </summary>
internal sealed class PortalSignInManager : SignInManager<ApplicationUser>
{
    private readonly SecuritySettingsStore _settings;
    private readonly RegistrationSettingsStore _registration;

    public PortalSignInManager(
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation,
        SecuritySettingsStore settings,
        RegistrationSettingsStore registration)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
        _settings = settings;
        _registration = registration;
    }

    public override async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.PendingApproval
            || await _registration.AwaitsEmailConfirmationAsync(user, CancellationToken.None).ConfigureAwait(false))
        {
            return false;
        }

        return await base.CanSignInAsync(user).ConfigureAwait(false);
    }

    public override async Task<bool> IsTwoFactorEnabledAsync(ApplicationUser user) =>
        await base.IsTwoFactorEnabledAsync(user).ConfigureAwait(false)
        && await _settings.IsTwoFactorAvailableAsync(CancellationToken.None).ConfigureAwait(false);
}
