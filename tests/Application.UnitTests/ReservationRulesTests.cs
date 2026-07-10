using AntiguoAserradero.Application.Reservations;
using AntiguoAserradero.Domain.Errors;

namespace AntiguoAserradero.Application.UnitTests;

public sealed class ReservationRulesTests
{
    [Fact]
    public void OverlapsUsesHalfOpenIntervals()
    {
        var entry = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var exit = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(ReservationDateRules.Overlaps(entry, exit, new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc)));
        Assert.False(ReservationDateRules.Overlaps(entry, exit, new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc)));
        Assert.False(ReservationDateRules.Overlaps(entry, exit, new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void CountNightsRequiresEntryBeforeExit()
    {
        var entry = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
        var exit = new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(3, ReservationDateRules.CountNights(entry, exit));
        Assert.Throws<ValidationException>(() => ReservationDateRules.CountNights(entry, entry));
        Assert.Throws<ValidationException>(() => ReservationDateRules.CountNights(exit, entry));
    }

    [Fact]
    public void CapacityCountsHumanOccupants()
    {
        ReservationOccupancyRules.ValidateCapacity(2, 1, 1, 3, 4);

        var exception = Assert.Throws<ValidationException>(() => ReservationOccupancyRules.ValidateCapacity(2, 2, 1, 0, 4));
        Assert.Equal("Reservation.CapacityExceeded", exception.Code);
    }

    [Theory]
    [InlineData(1000, 0, "Pending")]
    [InlineData(1000, 1, "Partial")]
    [InlineData(1000, 999.99, "Partial")]
    [InlineData(1000, 1000, "Paid")]
    [InlineData(1000, 1200, "Paid")]
    public void PaymentStatusIsDerivedFromChargesAndPayments(decimal charges, decimal payments, string expected)
    {
        Assert.Equal(expected, ReservationPaymentStatusRules.DeriveStatusCode(charges, payments));
    }
}
