namespace OpenPortal.Notifications.Domain;

/// <summary>Which notifications reach someone by email, besides the notification center.</summary>
public enum EmailDelivery
{
    /// <summary>Only notices about the account's safety (a new sign-in, a reset password), at once. The default.</summary>
    SecurityOnly = 0,

    /// <summary>Every notification, at once.</summary>
    Instant = 1,

    /// <summary>Security notices at once, and one email a day with the rest that is still unread.</summary>
    DailyDigest = 2,

    /// <summary>No email at all; the notification center only.</summary>
    Off = 3,
}

/// <summary>One person's choice of email for their notifications. No row means the default.</summary>
public sealed class NotificationPreference
{
    // Required by EF Core.
    private NotificationPreference()
    {
    }

    public NotificationPreference(Guid userId, EmailDelivery email, DateTimeOffset now)
    {
        UserId = userId;
        Email = email;
        UpdatedAtUtc = now;
        LastDigestAtUtc = now;
    }

    public Guid UserId { get; private set; }

    public EmailDelivery Email { get; private set; }

    /// <summary>When the last daily digest was sent (or the preference was saved): the next covers what came after.</summary>
    public DateTimeOffset? LastDigestAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Whether a notice is emailed at once to this person.</summary>
    public static bool EmailsAtOnce(EmailDelivery email, bool security) => email switch
    {
        EmailDelivery.Off => false,
        EmailDelivery.Instant => true,
        _ => security,
    };

    /// <summary>Changes the choice. Returns whether anything changed.</summary>
    public bool Change(EmailDelivery email, DateTimeOffset now)
    {
        if (!Enum.IsDefined(email))
        {
            throw new ArgumentOutOfRangeException(nameof(email), email, null);
        }

        if (Email == email)
        {
            return false;
        }

        // Switching to the digest starts it from now: nothing older is mailed in one go.
        if (email == EmailDelivery.DailyDigest)
        {
            LastDigestAtUtc = now;
        }

        Email = email;
        UpdatedAtUtc = now;

        return true;
    }
}
