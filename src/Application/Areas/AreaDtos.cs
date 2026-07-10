namespace AntiguoAserradero.Application.Areas;

public sealed record AreaDto(
    int Id,
    string Name,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime,
    TimeOnly ReceptionOpenTime,
    TimeOnly ReceptionCloseTime,
    bool IsActive);

public sealed record UpsertAreaRequest(
    string Name,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime,
    TimeOnly ReceptionOpenTime,
    TimeOnly ReceptionCloseTime);

public sealed record AreaMutationResponse(AreaDto Area, IReadOnlyList<string> Warnings);
