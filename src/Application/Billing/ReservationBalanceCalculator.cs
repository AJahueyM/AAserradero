namespace AntiguoAserradero.Application.Billing;

public static class ReservationBalanceCalculator
{
    public static ReservationBalance FromMovements(IEnumerable<MovementAmount> movements)
    {
        ArgumentNullException.ThrowIfNull(movements);

        var charges = 0m;
        var payments = 0m;
        foreach (var movement in movements)
        {
            charges += movement.Charge;
            payments += movement.Payment;
        }

        return new ReservationBalance(charges, payments);
    }
}
