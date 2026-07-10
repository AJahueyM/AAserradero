using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AntiguoAserradero.Infrastructure.Persistence.Converters;

public sealed class UtcNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public UtcNullableDateTimeConverter()
        : base(
            value => value.HasValue
                ? value.Value.Kind == DateTimeKind.Utc
                    ? value.Value
                    : value.Value.ToUniversalTime()
                : null,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null)
    {
    }
}
