using System.Globalization;
using System.Net.Mail;
using System.Text;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Users;

/// <summary>One row of an import, and what became of it.</summary>
/// <param name="Row">The line in the file (the header is line 1).</param>
/// <param name="Outcome"><c>invited</c>, <c>updated</c>, <c>skipped</c> or <c>error</c> (in a dry run: what would happen).</param>
/// <param name="ErrorCode">Why the row was refused, as an <c>errorCode</c> the client translates.</param>
public sealed record UserImportRowDto(int Row, string Email, string Outcome, string? ErrorCode);

/// <summary>An import's outcome, row by row.</summary>
public sealed record UserImportResultDto(
    bool DryRun,
    int Invited,
    int Updated,
    int Skipped,
    int Failed,
    IReadOnlyList<UserImportRowDto> Rows);

/// <summary>Stable failure codes of the user file import.</summary>
public static class UserImportErrors
{
    public static readonly Error NoEmailColumn =
        Error.Validation("users.import_no_email_column", "The file needs a header line with an email column.");

    public static readonly Error TooManyRows =
        Error.Validation("users.import_too_many_rows", $"A file holds at most {UserCsv.MaxImportRows} accounts.");

    public static readonly Error FileInvalid =
        Error.Validation("users.import_file_invalid", $"Send one CSV file (UTF-8, at most {UserCsv.MaxImportBytes / 1024} KB) as the file part.");

    public static readonly Error EmailInvalid = Error.Validation("users.import_email_invalid", "The address is not valid.");

    public static readonly Error Duplicate = Error.Validation("users.import_duplicate", "The address appears earlier in the file.");

    public static readonly Error GroupUnknown = Error.Validation("users.import_group_unknown", "A group in the row does not exist.");

    public static readonly Error OrganizationUnknown =
        Error.Validation("users.import_organization_unknown", "An organization in the row does not exist.");
}

/// <summary>
/// The user list as a spreadsheet and back: the export the users page downloads, and the import that invites the
/// addresses not known yet (with their names, groups and organizations) and, when asked, updates the details of the
/// accounts that exist. Every row goes through the same services as the screens, so the same checks apply.
/// </summary>
public sealed class UserCsv
{
    public const int MaxImportRows = 1000;
    public const int MaxImportBytes = 1024 * 1024;

    /// <summary>The columns the export writes and the import reads (besides email, all optional).</summary>
    private static readonly string[] DetailColumns =
        ["firstName", "lastName", "phoneNumber", "jobTitle", "company", "department", "addressLine", "postalCode", "city", "country"];

    private readonly IUserAdministrationService _users;
    private readonly IUserLookupService _lookup;
    private readonly IInvitationService _invitations;
    private readonly IAccessProvisioningService _provisioning;
    private readonly IGroupService _groups;
    private readonly IOrganizationService _organizations;

    public UserCsv(
        IUserAdministrationService users,
        IUserLookupService lookup,
        IInvitationService invitations,
        IAccessProvisioningService provisioning,
        IGroupService groups,
        IOrganizationService organizations)
    {
        _users = users;
        _lookup = lookup;
        _invitations = invitations;
        _provisioning = provisioning;
        _groups = groups;
        _organizations = organizations;
    }

    /// <summary>The CSV of the given accounts; the administrator column only for an administrator.</summary>
    public static byte[] Export(IReadOnlyList<UserSummaryDto> users, bool includeAdministratorFlag)
    {
        var header = new List<string> { "email" };
        header.AddRange(DetailColumns);
        header.AddRange(["status", "origin", "createdAtUtc", "lastSignInAtUtc"]);
        if (includeAdministratorFlag)
        {
            header.Add("isAdministrator");
        }

        var csv = new StringBuilder().AppendLine(string.Join(',', header));

        foreach (var user in users)
        {
            var values = new List<string?>
            {
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.JobTitle,
                user.Company,
                user.Department,
                user.AddressLine,
                user.PostalCode,
                user.City,
                user.Country,
                StatusOf(user),
                user.Origin,
                user.CreatedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                user.LastSignInAtUtc?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            };

            if (includeAdministratorFlag)
            {
                values.Add(user.IsAdministrator ? "true" : "false");
            }

            csv.AppendRow([.. values]);
        }

        return Csv.ToBytes(csv);
    }

    /// <summary>
    /// Imports a file: new addresses are invited, existing accounts updated (when <paramref name="updateExisting"/>)
    /// or skipped. With <paramref name="dryRun"/> nothing is written and the result says what would happen.
    /// </summary>
    public async Task<Result<UserImportResultDto>> ImportAsync(
        string text,
        bool dryRun,
        bool updateExisting,
        CancellationToken cancellationToken)
    {
        var rows = Csv.Parse(text);
        if (rows.Count == 0)
        {
            return Result<UserImportResultDto>.Failure(UserImportErrors.NoEmailColumn);
        }

        var columns = rows[0]
            .Select((name, index) => (Name: name.Trim(), Index: index))
            .Where(column => column.Name.Length > 0)
            .GroupBy(column => column.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);

        if (!columns.ContainsKey("email"))
        {
            return Result<UserImportResultDto>.Failure(UserImportErrors.NoEmailColumn);
        }

        if (rows.Count - 1 > MaxImportRows)
        {
            return Result<UserImportResultDto>.Failure(UserImportErrors.TooManyRows);
        }

        var groupIds = await NamesAsync(columns, rows, "groups", LoadGroupsAsync, cancellationToken).ConfigureAwait(false);
        if (groupIds.IsFailure)
        {
            return Result<UserImportResultDto>.Failure(groupIds.Error);
        }

        var organizationIds = await NamesAsync(columns, rows, "organizations", LoadOrganizationsAsync, cancellationToken).ConfigureAwait(false);
        if (organizationIds.IsFailure)
        {
            return Result<UserImportResultDto>.Failure(organizationIds.Error);
        }

        var results = new List<UserImportRowDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index];
            string? Cell(string name) =>
                columns.TryGetValue(name, out var at) && at < row.Count && row[at].Trim() is { Length: > 0 } value ? value : null;

            var email = Cell("email") ?? string.Empty;
            var outcome = await ImportRowAsync(
                    email,
                    Cell,
                    groupIds.Value,
                    organizationIds.Value,
                    seen,
                    dryRun,
                    updateExisting,
                    cancellationToken)
                .ConfigureAwait(false);

            results.Add(new UserImportRowDto(index + 1, email, outcome.Outcome, outcome.ErrorCode));
        }

        var result = new UserImportResultDto(
            dryRun,
            results.Count(row => row.Outcome == "invited"),
            results.Count(row => row.Outcome == "updated"),
            results.Count(row => row.Outcome == "skipped"),
            results.Count(row => row.Outcome == "error"),
            results);

        if (!dryRun)
        {
            await _users.RecordImportAsync(
                    new Dictionary<string, string?>
                    {
                        ["invited"] = result.Invited.ToString(CultureInfo.InvariantCulture),
                        ["updated"] = result.Updated.ToString(CultureInfo.InvariantCulture),
                        ["skipped"] = result.Skipped.ToString(CultureInfo.InvariantCulture),
                        ["failed"] = result.Failed.ToString(CultureInfo.InvariantCulture),
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<UserImportResultDto>.Success(result);
    }

    private async Task<(string Outcome, string? ErrorCode)> ImportRowAsync(
        string email,
        Func<string, string?> cell,
        IReadOnlyDictionary<string, Guid> groups,
        IReadOnlyDictionary<string, Guid> organizations,
        HashSet<string> seen,
        bool dryRun,
        bool updateExisting,
        CancellationToken cancellationToken)
    {
        if (email.Length > 256 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            return ("error", UserImportErrors.EmailInvalid.Code);
        }

        if (!seen.Add(email))
        {
            return ("error", UserImportErrors.Duplicate.Code);
        }

        var existing = await _lookup.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!updateExisting)
            {
                return ("skipped", null);
            }

            var current = await _users.GetUserAsync(existing.Id, cancellationToken).ConfigureAwait(false);
            if (current.IsFailure)
            {
                return ("error", current.Error.Code);
            }

            // An empty cell keeps the current value: a partial file never wipes details out.
            var user = current.Value;
            var update = new UpdateUserRequest
            {
                IsAdministrator = user.IsAdministrator,
                FirstName = cell("firstName") ?? user.FirstName,
                LastName = cell("lastName") ?? user.LastName,
                PhoneNumber = cell("phoneNumber") ?? user.PhoneNumber,
                JobTitle = cell("jobTitle") ?? user.JobTitle,
                Company = cell("company") ?? user.Company,
                Department = cell("department") ?? user.Department,
                AddressLine = cell("addressLine") ?? user.AddressLine,
                PostalCode = cell("postalCode") ?? user.PostalCode,
                City = cell("city") ?? user.City,
                Country = cell("country") ?? user.Country,
            };

            if (dryRun)
            {
                return ("updated", null);
            }

            var updated = await _users.UpdateUserAsync(existing.Id, update, cancellationToken).ConfigureAwait(false);
            return updated.IsSuccess ? ("updated", null) : ("error", updated.Error.Code);
        }

        var groupIds = Resolve(cell("groups"), groups);
        if (groupIds is null)
        {
            return ("error", UserImportErrors.GroupUnknown.Code);
        }

        var organizationIds = Resolve(cell("organizations"), organizations);
        if (organizationIds is null)
        {
            return ("error", UserImportErrors.OrganizationUnknown.Code);
        }

        var memberships = await _provisioning.ValidateMembershipsAsync(groupIds, organizationIds, cancellationToken).ConfigureAwait(false);
        if (memberships.IsFailure)
        {
            return ("error", memberships.Error.Code);
        }

        if (dryRun)
        {
            return ("invited", null);
        }

        var invited = await _invitations.CreateAsync(
                new CreateInvitationRequest
                {
                    Email = email,
                    FirstName = cell("firstName"),
                    LastName = cell("lastName"),
                    GroupIds = groupIds,
                    OrganizationIds = organizationIds,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return invited.IsSuccess ? ("invited", null) : ("error", invited.Error.Code);
    }

    /// <summary>The ids behind a cell of names separated by <c>|</c>; null when one is unknown.</summary>
    private static List<Guid>? Resolve(string? cell, IReadOnlyDictionary<string, Guid> known)
    {
        var ids = new List<Guid>();
        foreach (var name in (cell ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!known.TryGetValue(name, out var id))
            {
                return null;
            }

            ids.Add(id);
        }

        return ids;
    }

    /// <summary>Loads the names a column refers to, only when the file uses it (listing needs its own page).</summary>
    private static async Task<Result<IReadOnlyDictionary<string, Guid>>> NamesAsync(
        Dictionary<string, int> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string column,
        Func<CancellationToken, Task<Result<IReadOnlyDictionary<string, Guid>>>> load,
        CancellationToken cancellationToken)
    {
        var used = columns.TryGetValue(column, out var at)
            && rows.Skip(1).Any(row => at < row.Count && !string.IsNullOrWhiteSpace(row[at]));

        return used
            ? await load(cancellationToken).ConfigureAwait(false)
            : Result<IReadOnlyDictionary<string, Guid>>.Success(new Dictionary<string, Guid>());
    }

    private async Task<Result<IReadOnlyDictionary<string, Guid>>> LoadGroupsAsync(CancellationToken cancellationToken)
    {
        var groups = await _groups.ListAsync(cancellationToken).ConfigureAwait(false);

        return groups.IsSuccess
            ? Result<IReadOnlyDictionary<string, Guid>>.Success(
                groups.Value.GroupBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase))
            : Result<IReadOnlyDictionary<string, Guid>>.Failure(groups.Error);
    }

    private async Task<Result<IReadOnlyDictionary<string, Guid>>> LoadOrganizationsAsync(CancellationToken cancellationToken)
    {
        var organizations = await _organizations.ListAsync(cancellationToken).ConfigureAwait(false);

        // By slug (its short, stable name) or by display name.
        return organizations.IsSuccess
            ? Result<IReadOnlyDictionary<string, Guid>>.Success(
                organizations.Value
                    .SelectMany(organization => new[] { (Key: organization.Slug, organization.Id), (Key: organization.Name, organization.Id) })
                    .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase))
            : Result<IReadOnlyDictionary<string, Guid>>.Failure(organizations.Error);
    }

    private static string StatusOf(UserSummaryDto user) =>
        user.DeletionScheduledAtUtc is not null ? UserStatusFilter.Deletion
        : user.PendingApproval ? UserStatusFilter.Pending
        : user.IsLockedOut ? UserStatusFilter.Locked
        : !user.EmailConfirmed ? UserStatusFilter.Unconfirmed
        : UserStatusFilter.Active;
}
