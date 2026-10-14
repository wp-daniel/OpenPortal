namespace OpenPortal.Audit.Application.Contracts;

/// <summary>
/// What to count. The Audit module stores every module's events without knowing what they mean, so the host names
/// the action codes that are sign-ins.
/// </summary>
/// <param name="Days">Length of the period, ending today (1 to <see cref="MaxDays"/>).</param>
/// <param name="TimeZone">IANA or Windows time zone the days are counted in; UTC when unknown.</param>
/// <param name="SignInAction">The action recorded for a portal sign-in (its failures carry a <c>reason</c> detail).</param>
/// <param name="ApplicationSignInAction">The action recorded for a sign-in to an application (its target is the application).</param>
public sealed record ActivityStatisticsRequest(int Days, string? TimeZone, string SignInAction, string ApplicationSignInAction)
{
    public const int MaxDays = 90;
}

/// <summary>Sign-in activity over a period, with the period before it for comparison.</summary>
/// <param name="From">First day of the period (in <paramref name="TimeZone"/>).</param>
/// <param name="To">Last day of the period, today.</param>
/// <param name="TimeZone">The time zone the days were counted in.</param>
/// <param name="Current">Totals over the period.</param>
/// <param name="Previous">Totals over the period of the same length just before.</param>
/// <param name="Days">One entry per day, oldest first, including days without activity.</param>
/// <param name="TopApplications">Applications by sign-ins, most used first.</param>
/// <param name="FailureReasons">Failed portal sign-ins by reason, most frequent first.</param>
/// <param name="TopFailedAccounts">The accounts (as typed) with most failed sign-ins.</param>
/// <param name="TopFailedAddresses">The addresses with most failed sign-ins.</param>
/// <param name="IsComplete">False when the audit retention removed part of the period (or of the one before).</param>
public sealed record ActivityStatisticsDto(
    DateOnly From,
    DateOnly To,
    string TimeZone,
    ActivityTotalsDto Current,
    ActivityTotalsDto Previous,
    IReadOnlyList<DailyActivityDto> Days,
    IReadOnlyList<ApplicationUsageDto> TopApplications,
    IReadOnlyList<CountedValueDto> FailureReasons,
    IReadOnlyList<CountedValueDto> TopFailedAccounts,
    IReadOnlyList<CountedValueDto> TopFailedAddresses,
    bool IsComplete);

/// <param name="SignIns">Successful portal sign-ins.</param>
/// <param name="FailedSignIns">Refused portal sign-ins (wrong password, unknown account, lockout, wrong code).</param>
/// <param name="ApplicationSignIns">Sign-ins to applications through the portal.</param>
/// <param name="ActiveUsers">Distinct accounts that signed in to the portal or to an application.</param>
public sealed record ActivityTotalsDto(int SignIns, int FailedSignIns, int ApplicationSignIns, int ActiveUsers);

public sealed record DailyActivityDto(
    DateOnly Date,
    int SignIns,
    int FailedSignIns,
    int ApplicationSignIns,
    int ActiveUsers);

/// <param name="ClientId">The application's client id.</param>
/// <param name="Name">Its name as recorded by the latest sign-in.</param>
/// <param name="SignIns">Sign-ins over the period.</param>
/// <param name="Users">Distinct users who signed in.</param>
public sealed record ApplicationUsageDto(string ClientId, string? Name, int SignIns, int Users);

/// <param name="Value">A reason code, an email as typed or an address.</param>
/// <param name="LastAtUtc">When it last happened.</param>
public sealed record CountedValueDto(string Value, int Count, DateTimeOffset LastAtUtc);
