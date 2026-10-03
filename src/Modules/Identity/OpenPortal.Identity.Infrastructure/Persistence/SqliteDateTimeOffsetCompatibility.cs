using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace OpenPortal.Identity.Infrastructure.Persistence;

/// <summary>
/// Makes <see cref="DateTimeOffset"/> sortable on SQLite.
/// <para>
/// See the matching helper in the Content module for the reasoning. Identity carries these columns too -
/// lockout and token expiry dates, for example - and any query ordering or filtering by them would
/// otherwise fail at translation time on SQLite.
/// </para>
/// </summary>
internal static class SqliteDateTimeOffsetCompatibility
{
    private static readonly ValueConverter<DateTimeOffset, long> ToTicks =
        new(value => value.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> ToNullableTicks =
        new(
            value => value.HasValue ? value.Value.UtcTicks : null,
            ticks => ticks.HasValue ? new DateTimeOffset(ticks.Value, TimeSpan.Zero) : null);

    public static void ApplyIfSqlite(ModelBuilder modelBuilder, string? providerName)
    {
        if (providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) is not true)
        {
            return;
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                Apply(property);
            }
        }
    }

    private static void Apply(IMutableProperty property)
    {
        if (property.ClrType == typeof(DateTimeOffset))
        {
            property.SetValueConverter(ToTicks);
        }
        else if (property.ClrType == typeof(DateTimeOffset?))
        {
            property.SetValueConverter(ToNullableTicks);
        }
    }
}