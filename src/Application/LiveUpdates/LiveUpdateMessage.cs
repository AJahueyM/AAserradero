namespace AntiguoAserradero.Application.LiveUpdates;

public sealed record LiveUpdateMessage(
    string Type,
    string? JsonPayload,
    string? OriginatorUserId,
    DateTime OccurredAtUtc);
