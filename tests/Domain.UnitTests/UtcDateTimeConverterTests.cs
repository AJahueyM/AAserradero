using AntiguoAserradero.Infrastructure.Persistence.Converters;

namespace AntiguoAserradero.Domain.UnitTests;

public sealed class UtcDateTimeConverterTests
{
    [Fact]
    public void ConverterRoundTripsValuesAsUtc()
    {
        var converter = new UtcDateTimeConverter();
        var local = new DateTime(2026, 7, 9, 12, 30, 0, DateTimeKind.Local);

        var providerValue = converter.ConvertToProviderExpression.Compile()(local);
        var modelValue = converter.ConvertFromProviderExpression.Compile()(DateTime.SpecifyKind(providerValue, DateTimeKind.Unspecified));

        Assert.Equal(DateTimeKind.Utc, providerValue.Kind);
        Assert.Equal(DateTimeKind.Utc, modelValue.Kind);
        Assert.Equal(providerValue, modelValue);
    }
}
