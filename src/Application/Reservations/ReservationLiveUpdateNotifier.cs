using System.Text.Json;
using AntiguoAserradero.Application.Auth;
using AntiguoAserradero.Application.LiveUpdates;
using AntiguoAserradero.Domain.Entities;

namespace AntiguoAserradero.Application.Reservations;

public interface IReservationLiveUpdateNotifier
{
    Task PublishReservationChangedAsync(Reservation reservation, string action, CancellationToken cancellationToken = default);

    Task PublishMovementChangedAsync(Reservation reservation, int? movementId, string action, CancellationToken cancellationToken = default);
}

public sealed class ReservationLiveUpdateNotifier : IReservationLiveUpdateNotifier
{
    public const string ReservationChangedType = "reservation.changed";
    public const string MovementChangedType = "reservation.movement.changed";

    private readonly ILiveUpdatePublisher _publisher;
    private readonly ICurrentUser _currentUser;

    public ReservationLiveUpdateNotifier(ILiveUpdatePublisher publisher, ICurrentUser currentUser)
    {
        _publisher = publisher;
        _currentUser = currentUser;
    }

    public Task PublishReservationChangedAsync(Reservation reservation, string action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var payload = new ReservationChangedPayload(
            reservation.Id,
            reservation.RoomId,
            reservation.ClientId,
            reservation.EntryDate,
            reservation.ExitDate,
            reservation.StatusId,
            action);

        return PublishAsync(ReservationChangedType, payload, cancellationToken);
    }

    public Task PublishMovementChangedAsync(Reservation reservation, int? movementId, string action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var payload = new MovementChangedPayload(
            reservation.Id,
            movementId,
            reservation.RoomId,
            reservation.ClientId,
            reservation.EntryDate,
            reservation.ExitDate,
            reservation.StatusId,
            action);

        return PublishAsync(MovementChangedType, payload, cancellationToken);
    }

    private Task PublishAsync<TPayload>(string type, TPayload payload, CancellationToken cancellationToken)
    {
        var message = new LiveUpdateMessage(
            type,
            JsonSerializer.Serialize(payload),
            _currentUser.Id,
            DateTime.UtcNow);

        return _publisher.PublishAsync(message, cancellationToken).AsTask();
    }

    private sealed record ReservationChangedPayload(
        int ReservationId,
        int RoomId,
        int ClientId,
        DateTime EntryDate,
        DateTime ExitDate,
        int StatusId,
        string Action);

    private sealed record MovementChangedPayload(
        int ReservationId,
        int? MovementId,
        int RoomId,
        int ClientId,
        DateTime EntryDate,
        DateTime ExitDate,
        int StatusId,
        string Action);
}
