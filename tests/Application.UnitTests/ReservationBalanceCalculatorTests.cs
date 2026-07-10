using AntiguoAserradero.Application.Billing;

namespace AntiguoAserradero.Application.UnitTests;

public sealed class ReservationBalanceCalculatorTests
{
    [Fact]
    public void FromMovementsDerivesBalanceFromChargesAndPayments()
    {
        var balance = ReservationBalanceCalculator.FromMovements([
            new MovementAmount(1500m, 0m),
            new MovementAmount(0m, 500m),
            new MovementAmount(200m, 0m),
        ]);

        Assert.Equal(1700m, balance.Charges);
        Assert.Equal(500m, balance.Payments);
        Assert.Equal(1200m, balance.Balance);
    }
}
