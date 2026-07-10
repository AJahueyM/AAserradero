using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Reference;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.Areas;

public interface IAreaService
{
    Task<PagedResult<AreaDto>> ListAsync(CatalogListQuery query, CancellationToken cancellationToken = default);
    Task<AreaDto> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<AreaDto> CreateAsync(UpsertAreaRequest request, CancellationToken cancellationToken = default);
    Task<AreaDto> UpdateAsync(int id, UpsertAreaRequest request, CancellationToken cancellationToken = default);
    Task<AreaMutationResponse> DeactivateAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class AreaService(IApplicationDbContext dbContext) : IAreaService
{
    public async Task<PagedResult<AreaDto>> ListAsync(CatalogListQuery query, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = NormalizePaging(query.Page, query.PageSize);
        var search = query.Search?.Trim();
        var areas = dbContext.Areas.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            areas = areas.Where(area => area.Name.Contains(search));
        }

        var total = await areas.CountAsync(cancellationToken);
        var items = await areas
            .OrderBy(area => area.Name)
            .ThenBy(area => area.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(area => new AreaDto(area.Id, area.Name, area.CheckInTime, area.CheckOutTime, area.ReceptionOpenTime, area.ReceptionCloseTime, area.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<AreaDto>(items, page, pageSize, total);
    }

    public async Task<AreaDto> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var area = await FindAsync(id, cancellationToken);
        return ToDto(area);
    }

    public async Task<AreaDto> CreateAsync(UpsertAreaRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var area = new Area
        {
            Name = request.Name.Trim(),
            CheckInTime = request.CheckInTime,
            CheckOutTime = request.CheckOutTime,
            ReceptionOpenTime = request.ReceptionOpenTime,
            ReceptionCloseTime = request.ReceptionCloseTime,
            IsActive = true,
        };

        dbContext.Areas.Add(area);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(area);
    }

    public async Task<AreaDto> UpdateAsync(int id, UpsertAreaRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var area = await FindAsync(id, cancellationToken);
        area.Name = request.Name.Trim();
        area.CheckInTime = request.CheckInTime;
        area.CheckOutTime = request.CheckOutTime;
        area.ReceptionOpenTime = request.ReceptionOpenTime;
        area.ReceptionCloseTime = request.ReceptionCloseTime;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(area);
    }

    public async Task<AreaMutationResponse> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var area = await FindAsync(id, cancellationToken);
        var hasFutureReservations = await HasFutureReservationsAsync(id, cancellationToken);
        area.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        var warnings = hasFutureReservations
            ? new[] { "El área tiene reservaciones activas futuras; se desactivó sin eliminar el historial." }
            : [];

        return new AreaMutationResponse(ToDto(area), warnings);
    }

    private async Task<Area> FindAsync(int id, CancellationToken cancellationToken)
    {
        return await dbContext.Areas.FirstOrDefaultAsync(area => area.Id == id, cancellationToken)
            ?? throw new NotFoundException("Area.NotFound", "Area was not found.");
    }

    private async Task<bool> HasFutureReservationsAsync(int areaId, CancellationToken cancellationToken)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var cancelledStatusId = await dbContext.ReservationStatuses
            .Where(status => status.Code == ReservationStatusCodes.Cancelled)
            .Select(status => (int?)status.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var roomIds = dbContext.Rooms
            .Where(room => room.AreaId == areaId)
            .Select(room => room.Id);
        return await dbContext.Reservations
            .AsNoTracking()
            .AnyAsync(reservation =>
                reservation.IsActive &&
                reservation.ExitDate >= todayUtc &&
                reservation.StatusId != cancelledStatusId &&
                roomIds.Contains(reservation.RoomId),
                cancellationToken);
    }

    private static void Validate(UpsertAreaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Area.NameRequired", "Area name is required.", new { field = nameof(request.Name) });
        }

        if (request.ReceptionOpenTime >= request.ReceptionCloseTime)
        {
            throw new ValidationException("Area.ReceptionWindowInvalid", "Reception open time must be before close time.");
        }

        if (request.CheckInTime < request.ReceptionOpenTime || request.CheckInTime > request.ReceptionCloseTime)
        {
            throw new ValidationException("Area.CheckInTimeInvalid", "Check-in time must be inside the reception window.");
        }

        if (request.CheckOutTime < request.ReceptionOpenTime || request.CheckOutTime > request.ReceptionCloseTime)
        {
            throw new ValidationException("Area.CheckOutTimeInvalid", "Check-out time must be inside the reception window.");
        }
    }

    private static AreaDto ToDto(Area area)
    {
        return new AreaDto(area.Id, area.Name, area.CheckInTime, area.CheckOutTime, area.ReceptionOpenTime, area.ReceptionCloseTime, area.IsActive);
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        return (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
    }
}
