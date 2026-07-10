using AntiguoAserradero.Domain.Entities;

namespace AntiguoAserradero.Application.Movements;

public static class MovementMapper
{
    public static MovementResponse ToResponse(Movement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);

        var concept = movement.Concept ?? throw new InvalidOperationException("Movement concept was not loaded.");
        var paymentMethod = movement.PaymentMethod is null
            ? null
            : new MovementPaymentMethodResponse(movement.PaymentMethod.Id, movement.PaymentMethod.Code, movement.PaymentMethod.Name);
        var paymentLocation = movement.PaymentLocation is null
            ? null
            : new MovementPaymentLocationResponse(movement.PaymentLocation.Id, movement.PaymentLocation.Code, movement.PaymentLocation.Name);

        return new MovementResponse(
            movement.Id,
            movement.ReservationId,
            new MovementConceptResponse(concept.Id, concept.Code, concept.Name, concept.IsDiscount),
            paymentMethod,
            paymentLocation,
            movement.Charge,
            movement.Payment,
            movement.Date,
            movement.ResponsibleUserId,
            movement.CreatedAt);
    }
}
