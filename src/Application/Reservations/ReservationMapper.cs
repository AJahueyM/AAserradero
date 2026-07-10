using AntiguoAserradero.Application.Billing;
using AntiguoAserradero.Application.Movements;
using AntiguoAserradero.Domain.Entities;

namespace AntiguoAserradero.Application.Reservations;

public static class ReservationMapper
{
    public static ReservationSummaryResponse ToSummaryResponse(Reservation reservation, ReservationFinancialSummary financialSummary)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        return new ReservationSummaryResponse(
            reservation.Id,
            ToClientResponse(reservation.Client),
            ToRoomResponse(reservation.Room),
            reservation.EntryDate,
            reservation.ExitDate,
            ReservationDateRules.CountNights(reservation.EntryDate, reservation.ExitDate),
            reservation.Adults,
            reservation.Children,
            reservation.Infants,
            reservation.Pets,
            reservation.Fare,
            ToStatusResponse(reservation.Status),
            reservation.PromotorId,
            reservation.Notes,
            reservation.CreatedById,
            reservation.CreatedAt,
            financialSummary);
    }

    public static ReservationDetailResponse ToDetailResponse(Reservation reservation, ReservationFinancialSummary financialSummary)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        return new ReservationDetailResponse(
            reservation.Id,
            ToClientResponse(reservation.Client),
            ToRoomResponse(reservation.Room),
            reservation.EntryDate,
            reservation.ExitDate,
            ReservationDateRules.CountNights(reservation.EntryDate, reservation.ExitDate),
            reservation.Adults,
            reservation.Children,
            reservation.Infants,
            reservation.Pets,
            reservation.Fare,
            ToStatusResponse(reservation.Status),
            reservation.PromotorId,
            reservation.Notes,
            reservation.CreatedById,
            reservation.CreatedAt,
            financialSummary,
            reservation.Movements.OrderBy(movement => movement.Date).ThenBy(movement => movement.Id).Select(MovementMapper.ToResponse).ToArray());
    }

    public static ReservationFinancialSummary ToFinancialSummary(IEnumerable<Movement> movements)
    {
        ArgumentNullException.ThrowIfNull(movements);

        var balance = ReservationBalanceCalculator.FromMovements(movements.Select(movement => new MovementAmount(movement.Charge, movement.Payment)));
        return new ReservationFinancialSummary(balance.Charges, balance.Payments, balance.Balance);
    }

    private static ReservationClientResponse ToClientResponse(Client? client)
    {
        return client is null
            ? throw new InvalidOperationException("Reservation client was not loaded.")
            : new ReservationClientResponse(client.Id, client.Name, client.Phone, client.Cellphone);
    }

    private static ReservationRoomResponse ToRoomResponse(Room? room)
    {
        return room is null
            ? throw new InvalidOperationException("Reservation room was not loaded.")
            : new ReservationRoomResponse(room.Id, room.Name, room.Capacity, room.NightlyFare);
    }

    private static ReservationStatusResponse ToStatusResponse(ReservationStatus? status)
    {
        return status is null
            ? throw new InvalidOperationException("Reservation status was not loaded.")
            : new ReservationStatusResponse(status.Id, status.Code, status.Label);
    }
}
