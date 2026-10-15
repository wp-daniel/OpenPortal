using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Domain.Settings;

/// <summary>
/// What account holders may do with their own data, edited by administrators from the portal. A single row; until
/// an administrator saves it for the first time the defaults below apply.
/// </summary>
public sealed class PrivacySettings
{
    /// <summary>The key of the one row.</summary>
    public const int SingletonId = 1;

    public const int DefaultGraceDays = 30;
    public const int MaxGraceDays = 365;

    /// <summary>Required by Entity Framework Core materialisation, and the defaults when no row exists.</summary>
    private PrivacySettings()
    {
    }

    public int Id { get; private set; } = SingletonId;

    /// <summary>Whether account holders may ask for their own account to be deleted.</summary>
    public bool SelfDeletionEnabled { get; private set; } = true;

    /// <summary>
    /// Days between the request and the deletion, during which signing in lets the owner change their mind. Zero
    /// deletes at the next sweep (within the hour).
    /// </summary>
    public int DeletionGraceDays { get; private set; } = DefaultGraceDays;

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static PrivacySettings Defaults() => new();

    /// <summary>Applies new values; the result says whether anything changed.</summary>
    public Result<bool> Update(bool selfDeletionEnabled, int deletionGraceDays, DateTimeOffset now)
    {
        if (deletionGraceDays is < 0 or > MaxGraceDays)
        {
            return Result<bool>.Failure(UserErrors.GraceDaysInvalid);
        }

        if (SelfDeletionEnabled == selfDeletionEnabled && DeletionGraceDays == deletionGraceDays)
        {
            return Result<bool>.Success(false);
        }

        SelfDeletionEnabled = selfDeletionEnabled;
        DeletionGraceDays = deletionGraceDays;
        UpdatedAtUtc = now;

        return Result<bool>.Success(true);
    }
}
