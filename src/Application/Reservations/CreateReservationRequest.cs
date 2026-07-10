namespace AntiguoAserradero.Application.Reservations;

public sealed record CreateReservationRequest(DateTime EntryDate, DateTime ExitDate, decimal Fare);
