using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace OpenPortal.Content.Infrastructure.Persistence;

/// <summary>
/// Makes <see cref="DateTimeOffset"/> sortable on SQLite.
/// <para>
/// SQLite stores a <see cref="DateTimeOffset"/> as text and, more to the point, refuses to translate a
/// <c>DateTimeOffset</c> into an <c>ORDER BY</c> at all - the query throws
/// <see cref="NotSupportedException"/> rather than sorting the text. That makes every
/// <c>DateTimeOffset</c> column in the model unusable for ordering and unusable for range filters, which is
/// not a limitation a caller can work around: it surfaces as a 500 from a read endpoint.
/// </para>
/// <para>
/// The fix is to store the value as UTC ticks, which sorts and compares correctly as an integer. Applied
/// only when SQLite is the configured provider, so PostgreSQL keeps its native
/// <c>timestamptz</c> columns and its own semantics.
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
        // Nullable and non-nullable need separate converters: EF will not wrap one for the other.
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