using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>
/// The framework's sign-in manager, except that an account's second factor is only asked for while the portal
/// setting is on (turning the setting off therefore never strands an enrolled account, and turning it back on
/// restores every enrolment as it was), that a self-registered account waiting for approval or for its
/// address to be confirmed cannot sign in, and that every session records when the person authenticated
/// (<see cref="PortalClaimTypes.AuthenticationTime"/>).
/// </summary>
internal sealed class PortalSignInManager : SignInManager<ApplicationUser>
{
    private readonly SecuritySettingsStore _settings;
    private readonly RegistrationSettingsStore _registration;
    private readonly IClock _clock;

    /// <summary>The authentication time of the session a refreshed sign-in re-issues; null for a real sign-in.</summary>
    private string? _refreshedAuthenticationTime;

    public PortalSignInManager(
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation,
        SecuritySettingsStore settings,
        RegistrationSettingsStore registration,
        IClock clock)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
        _settings = settings;
        _registration = registration;
        _clock = clock;
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

    /// <summary>
    /// Re-issues the session after the security stamp changed (a new password, a second factor). That is not a new
    /// authentication, so the session keeps the time the person last proved who they are.
    /// </summary>
    public override async Task RefreshSignInAsync(ApplicationUser user)
    {
        var current = await Context.AuthenticateAsync(AuthenticationScheme).ConfigureAwait(false);
        _refreshedAuthenticationTime = current.Principal?.FindFirstValue(PortalClaimTypes.AuthenticationTime);

        try
        {
            await base.RefreshSignInAsync(user).ConfigureAwait(false);
        }
        finally
        {
            _refreshedAuthenticationTime = null;
        }
    }

    /// <summary>
    /// Every sign-in (password, second factor, passkey, external provider, invitation, registration) ends here: the
    /// session gets its authentication time, unless the caller carries one over (a refreshed sign-in, a support
    /// session).
    /// </summary>
    public override Task SignInWithClaimsAsync(
        ApplicationUser user,
        AuthenticationProperties? authenticationProperties,
        IEnumerable<Claim> additionalClaims)
    {
        ArgumentNullException.ThrowIfNull(additionalClaims);

        var claims = additionalClaims.ToList();
        if (!claims.Exists(claim => claim.Type == PortalClaimTypes.AuthenticationTime))
        {
            var time = _refreshedAuthenticationTime
                ?? _clock.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

            claims.Add(new Claim(PortalClaimTypes.AuthenticationTime, time));
        }

        return base.SignInWithClaimsAsync(user, authenticationProperties, claims);
    }
}
