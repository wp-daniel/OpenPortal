using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Identity.Domain.Legal;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Privacy;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>
/// The account section of a personal data export: profile, roles, sign-in facts, the privacy documents accepted and
/// the invitation that created the account. Never the password hash, the security stamp, the authenticator key or
/// the recovery codes; the picture travels as a file of its own (the host adds it).
/// </summary>
internal sealed class IdentityPersonalData : IPersonalDataSource
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IdentityDbContext _db;

    public IdentityPersonalData(UserManager<ApplicationUser> userManager, IdentityDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public string Section => "account";

    public async Task<object> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return new { };
        }

        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var logins = await _userManager.GetLoginsAsync(user).ConfigureAwait(false);

        var acceptances = await _db.LegalAcceptances
            .AsNoTracking()
            .Where(acceptance => acceptance.UserId == userId)
            .Join(
                _db.LegalDocuments.AsNoTracking(),
                acceptance => acceptance.DocumentId,
                document => document.Id,
                (acceptance, document) => new { document.Kind, document.Version, acceptance.AcceptedAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var invitation = await _db.Invitations
            .AsNoTracking()
            .Where(candidate => candidate.AcceptedUserId == userId)
            .Select(candidate => new { candidate.Email, candidate.CreatedAtUtc, candidate.AcceptedAtUtc })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var tokens = await _db.PersonalAccessTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new
        {
            profile = new
            {
                user.Id,
                user.Email,
                user.EmailConfirmed,
                user.FirstName,
                user.LastName,
                user.DisplayName,
                user.PhoneNumber,
                user.JobTitle,
                user.Company,
                user.Department,
                user.AddressLine,
                user.PostalCode,
                user.City,
                user.Country,
                user.Language,
                hasProfilePicture = user.AvatarUpdatedAtUtc is not null,
            },
            account = new
            {
                origin = user.Origin.ToString().ToLowerInvariant(),
                user.CreatedAtUtc,
                user.UpdatedAtUtc,
                user.LastSignInAtUtc,
                user.PendingApproval,
                twoFactorEnabled = user.TwoFactorEnabled,
                lockedOutUntil = user.LockoutEnd,
                user.DeletionRequestedAtUtc,
                user.DeletionScheduledAtUtc,
                roles = roles.Order(StringComparer.Ordinal).ToList(),
                externalLogins = logins.Select(login => login.LoginProvider).ToList(),
            },
            invitation,
            personalAccessTokens = tokens
                .Select(token => new { token.Name, prefix = token.DisplayPrefix, token.Scopes, token.CreatedAtUtc, token.ExpiresAtUtc, token.LastUsedAtUtc, token.RevokedAtUtc })
                .ToList(),
            legalAcceptances = acceptances
                .OrderBy(acceptance => acceptance.AcceptedAtUtc)
                .Select(acceptance => new
                {
                    document = acceptance.Kind == LegalDocumentKind.Privacy ? "privacy" : "terms",
                    acceptance.Version,
                    acceptance.AcceptedAtUtc,
                })
                .ToList(),
        };
    }
}
