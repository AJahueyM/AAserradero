using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Reference;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.Rooms;

public interface IRoomService
{
    Task<PagedResult<RoomDto>> ListAsync(CatalogListQuery query, CancellationToken cancellationToken = default);
    Task<RoomDto> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<RoomDto> CreateAsync(UpsertRoomRequest request, CancellationToken cancellationToken = default);
    Task<RoomDto> UpdateAsync(int id, UpsertRoomRequest request, CancellationToken cancellationToken = default);
    Task<RoomMutationResponse> DeactivateAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class RoomService(IApplicationDbContext dbContext) : IRoomService
{
    public async Task<PagedResult<RoomDto>> ListAsync(CatalogListQuery query, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = NormalizePaging(query.Page, query.PageSize);
        var search = query.Search?.Trim();
        var rooms = dbContext.Rooms.AsNoTracking().Include(room => room.Area).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            rooms = rooms.Where(room => room.Name.Contains(search));
        }

        var total = await rooms.CountAsync(cancellationToken);
        var items = await rooms
            .OrderBy(room => room.DisplayOrder)
            .ThenBy(room => room.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(room => new RoomDto(
                room.Id,
                room.AreaId,
                room.Area == null ? string.Empty : room.Area.Name,
                room.Name,
                room.Capacity,
                room.UnitCount,
                room.NightlyFare,
                room.Description,
                room.DisplayOrder,
                room.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<RoomDto>(items, page, pageSize, total);
    }

    public async Task<RoomDto> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var room = await FindAsync(id, cancellationToken);
        return ToDto(room);
    }

    public async Task<RoomDto> CreateAsync(UpsertRoomRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request, cancellationToken);
        var room = new Room
        {
            AreaId = request.AreaId,
            Name = request.Name.Trim(),
            Capacity = request.Capacity,
            UnitCount = request.UnitCount,
            NightlyFare = request.NightlyFare,
            Description = NormalizeOptional(request.Description),
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
        };

        dbContext.Rooms.Add(room);
        await dbContext.SaveChangesAsync(cancellationToken);
        room.Area = await dbContext.Areas.AsNoTracking().FirstAsync(area => area.Id == room.AreaId, cancellationToken);
        return ToDto(room);
    }

    public async Task<RoomDto> UpdateAsync(int id, UpsertRoomRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request, cancellationToken);
        var room = await FindAsync(id, cancellationToken);
        room.AreaId = request.AreaId;
        room.Name = request.Name.Trim();
        room.Capacity = request.Capacity;
        room.UnitCount = request.UnitCount;
        room.NightlyFare = request.NightlyFare;
        room.Description = NormalizeOptional(request.Description);
        room.DisplayOrder = request.DisplayOrder;
        await dbContext.SaveChangesAsync(cancellationToken);
        room.Area = await dbContext.Areas.AsNoTracking().FirstAsync(area => area.Id == room.AreaId, cancellationToken);
        return ToDto(room);
    }

    public async Task<RoomMutationResponse> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var room = await FindAsync(id, cancellationToken);
        var hasFutureReservations = await HasFutureReservationsAsync(id, cancellationToken);
        room.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        var warnings = hasFutureReservations
            ? new[] { "La habitación tiene reservaciones activas futuras; se desactivó sin eliminar el historial." }
            : [];

        return new RoomMutationResponse(ToDto(room), warnings);
    }

    private async Task<Room> FindAsync(int id, CancellationToken cancellationToken)
    {
        return await dbContext.Rooms.Include(room => room.Area).FirstOrDefaultAsync(room => room.Id == id, cancellationToken)
            ?? throw new NotFoundException("Room.NotFound", "Room was not found.");
    }

    private async Task ValidateAsync(UpsertRoomRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Room.NameRequired", "Room name is required.", new { field = nameof(request.Name) });
        }

        if (request.Capacity < 0)
        {
            throw new ValidationException("Room.CapacityNegative", "Capacity must be non-negative.", new { field = nameof(request.Capacity) });
        }

        if (request.UnitCount < 0)
        {
            throw new ValidationException("Room.UnitCountNegative", "Unit count must be non-negative.", new { field = nameof(request.UnitCount) });
        }

        if (request.NightlyFare < 0m)
        {
            throw new ValidationException("Room.NightlyFareNegative", "Nightly fare must be non-negative.", new { field = nameof(request.NightlyFare) });
        }

        var areaExists = await dbContext.Areas.AnyAsync(area => area.Id == request.AreaId, cancellationToken);
        if (!areaExists)
        {
            throw new ValidationException("Room.AreaNotFound", "The specified area does not exist.", new { field = nameof(request.AreaId), request.AreaId });
        }
    }

    private async Task<bool> HasFutureReservationsAsync(int roomId, CancellationToken cancellationToken)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var cancelledStatusId = await dbContext.ReservationStatuses
            .Where(status => status.Code == ReservationStatusCodes.Cancelled)
            .Select(status => (int?)status.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return await dbContext.Reservations
            .AsNoTracking()
            .AnyAsync(reservation =>
                reservation.RoomId == roomId &&
                reservation.IsActive &&
                reservation.ExitDate >= todayUtc &&
                reservation.StatusId != cancelledStatusId,
                cancellationToken);
    }

    private static RoomDto ToDto(Room room)
    {
        return new RoomDto(room.Id, room.AreaId, room.Area?.Name ?? string.Empty, room.Name, room.Capacity, room.UnitCount, room.NightlyFare, room.Description, room.DisplayOrder, room.IsActive);
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        return (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
    }
}
