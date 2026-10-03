namespace OpenPortal.SharedKernel.Time;

/// <inheritdoc />
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}