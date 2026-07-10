namespace AntiguoAserradero.Application.Reservations;

public sealed record CreateReservationRequest(
    int RoomId,
    int ClientId,
    DateTime EntryDate,
    DateTime ExitDate,
    int Adults,
    int Children,
    int Infants,
    int Pets,
    decimal? Fare,
    string? StatusCode,
    int PromotorId,
    string? Notes);
