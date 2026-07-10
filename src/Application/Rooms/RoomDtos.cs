namespace AntiguoAserradero.Application.Rooms;

public sealed record RoomDto(
    int Id,
    int AreaId,
    string AreaName,
    string Name,
    int Capacity,
    int UnitCount,
    decimal NightlyFare,
    string? Description,
    int DisplayOrder,
    bool IsActive);

public sealed record UpsertRoomRequest(
    int AreaId,
    string Name,
    int Capacity,
    int UnitCount,
    decimal NightlyFare,
    string? Description,
    int DisplayOrder);

public sealed record RoomMutationResponse(RoomDto Room, IReadOnlyList<string> Warnings);
