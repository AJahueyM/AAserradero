namespace AntiguoAserradero.Application.Billing;

public readonly record struct ReservationBalance(decimal Charges, decimal Payments)
{
    public decimal Balance => Charges - Payments;
}
