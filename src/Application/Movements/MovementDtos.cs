namespace AntiguoAserradero.Application.Movements;

public sealed record UpsertMovementRequest(
    string ConceptCode,
    string? PaymentMethodCode,
    string? PaymentLocationCode,
    decimal Charge,
    decimal Payment,
    DateTime? Date);

public sealed record MovementConceptResponse(int Id, string Code, string Name, bool IsDiscount);

public sealed record MovementPaymentMethodResponse(int Id, string Code, string Name);

public sealed record MovementPaymentLocationResponse(int Id, string Code, string Name);

public sealed record MovementResponse(
    int Id,
    int ReservationId,
    MovementConceptResponse Concept,
    MovementPaymentMethodResponse? PaymentMethod,
    MovementPaymentLocationResponse? PaymentLocation,
    decimal Charge,
    decimal Payment,
    DateTime Date,
    int ResponsibleUserId,
    DateTime CreatedAt);
