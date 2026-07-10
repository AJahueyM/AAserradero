using AntiguoAserradero.Application.Movements;

namespace AntiguoAserradero.Application.Reservations;

public sealed record UpdateReservationRequest(
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

public sealed record ReservationSearchRequest(
    DateTime? From,
    DateTime? To,
    ReservationDateSearchMode DateMode,
    string? ClientPhone,
    string? ClientName,
    int? ClientId);

public enum ReservationDateSearchMode
{
    Calendar,
    Arrivals,
    Departures,
}

public sealed record ReservationFinancialSummary(decimal Charges, decimal Payments, decimal OutstandingBalance);

public sealed record ReservationStatusResponse(int Id, string Code, string Label);

public sealed record ReservationClientResponse(int Id, string Name, string? Phone, string Cellphone);

public sealed record ReservationRoomResponse(int Id, string Name, int Capacity, decimal NightlyFare);

public sealed record ReservationSummaryResponse(
    int Id,
    ReservationClientResponse Client,
    ReservationRoomResponse Room,
    DateTime EntryDate,
    DateTime ExitDate,
    int Nights,
    int Adults,
    int Children,
    int Infants,
    int Pets,
    decimal Fare,
    ReservationStatusResponse Status,
    int PromotorId,
    string? Notes,
    int CreatedById,
    DateTime CreatedAt,
    ReservationFinancialSummary FinancialSummary);

public sealed record ReservationDetailResponse(
    int Id,
    ReservationClientResponse Client,
    ReservationRoomResponse Room,
    DateTime EntryDate,
    DateTime ExitDate,
    int Nights,
    int Adults,
    int Children,
    int Infants,
    int Pets,
    decimal Fare,
    ReservationStatusResponse Status,
    int PromotorId,
    string? Notes,
    int CreatedById,
    DateTime CreatedAt,
    ReservationFinancialSummary FinancialSummary,
    IReadOnlyList<MovementResponse> Movements);
