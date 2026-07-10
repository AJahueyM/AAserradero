using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.Reports;

public interface IReportService
{
    ExportMetadata GetExportMetadata();

    ExportTable CreateExport(ExportRequest request);

    Task<OccupancyFinancialReport> CreateOccupancyFinancialReportAsync(OccupancyFinancialReportRequest request, CancellationToken cancellationToken = default);
}

public sealed class ReportService : IReportService
{
    private readonly IApplicationDbContext _dbContext;

    public ReportService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public ExportMetadata GetExportMetadata() => ExportCatalog.GetMetadata();

    public ExportTable CreateExport(ExportRequest request)
    {
        var fields = ExportCatalog.Validate(request.Entity, request.Fields);
        return new ExportTable(request.Entity, fields, StreamRows(request.Entity, fields));
    }

    public async Task<OccupancyFinancialReport> CreateOccupancyFinancialReportAsync(OccupancyFinancialReportRequest request, CancellationToken cancellationToken = default)
    {
        var (startDate, endDate) = ValidateReportRequest(request);
        var countedWeekdays = request.Weekdays.ToHashSet();
        var countedDays = CountDays(startDate, endDate, countedWeekdays);

        var rooms = await _dbContext.Rooms
            .AsNoTracking()
            .Include(room => room.Area)
            .OrderBy(room => room.Area!.Name)
            .ThenBy(room => room.DisplayOrder)
            .ThenBy(room => room.Name)
            .ToArrayAsync(cancellationToken);

        var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var reservations = await _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.Status)
            .Where(reservation => reservation.EntryDate < endExclusive && reservation.ExitDate >= start)
            .ToArrayAsync(cancellationToken);

        var movements = await _dbContext.Movements
            .AsNoTracking()
            .Include(movement => movement.Concept)
            .Where(movement => movement.Date >= start && movement.Date < endExclusive)
            .ToArrayAsync(cancellationToken);

        var incomeByDate = movements
            .Where(movement => movement.Payment > 0m && movement.Concept?.IsDiscount != true)
            .GroupBy(movement => DateOnly.FromDateTime(movement.Date))
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Sum(movement => movement.Payment));

        var rows = rooms.Select(room =>
        {
            var roomReservations = reservations.Where(reservation => reservation.RoomId == room.Id).ToArray();
            var activeReservations = roomReservations.Where(reservation => reservation.Status?.Code != ReservationStatusCodes.Cancelled).ToArray();
            var roomMovements = movements.Where(movement => activeReservations.Any(reservation => reservation.Id == movement.ReservationId)).ToArray();
            var occupiedDays = activeReservations.Sum(reservation => CalculateOccupiedDays(reservation, startDate, endDate, countedWeekdays));

            return new RoomOccupancyFinancialRow(
                room.Id,
                room.Name,
                room.Area?.Name ?? string.Empty,
                countedDays == 0 ? 0m : Math.Round(occupiedDays / countedDays * 100m, 2, MidpointRounding.AwayFromZero),
                occupiedDays,
                countedDays,
                activeReservations.Sum(reservation => CalculateNights(reservation, startDate, endDate, countedWeekdays)),
                activeReservations.Length,
                roomReservations.Count(reservation => reservation.Status?.Code == ReservationStatusCodes.Cancelled),
                activeReservations.Sum(reservation => reservation.Adults + reservation.Children + reservation.Infants),
                roomMovements.Where(movement => movement.Payment > 0m && movement.Concept?.IsDiscount != true).Sum(movement => movement.Payment),
                roomMovements.Where(movement => movement.Payment > 0m && movement.Concept?.IsDiscount == true).Sum(movement => movement.Payment));
        }).ToArray();

        return new OccupancyFinancialReport(startDate, endDate, request.Weekdays, rows, incomeByDate);
    }

    public static decimal CalculateOccupiedDays(Reservation reservation, DateOnly startDate, DateOnly endDate, HashSet<DayOfWeek> countedWeekdays)
    {
        var entry = DateOnly.FromDateTime(reservation.EntryDate);
        var exit = DateOnly.FromDateTime(reservation.ExitDate);
        var first = entry > startDate ? entry : startDate;
        var last = exit < endDate ? exit : endDate;
        var occupied = 0m;

        for (var date = first; date <= last; date = date.AddDays(1))
        {
            if (!countedWeekdays.Contains(date.DayOfWeek))
            {
                continue;
            }

            occupied += date == entry || date == exit ? 0.5m : 1m;
        }

        return occupied;
    }

    private static int CalculateNights(Reservation reservation, DateOnly startDate, DateOnly endDate, HashSet<DayOfWeek> countedWeekdays)
    {
        var entry = DateOnly.FromDateTime(reservation.EntryDate);
        var exit = DateOnly.FromDateTime(reservation.ExitDate);
        var first = entry > startDate ? entry : startDate;
        var lastNight = exit.AddDays(-1) < endDate ? exit.AddDays(-1) : endDate;
        var nights = 0;

        for (var date = first; date <= lastNight; date = date.AddDays(1))
        {
            if (countedWeekdays.Contains(date.DayOfWeek))
            {
                nights++;
            }
        }

        return nights;
    }

    private static (DateOnly StartDate, DateOnly EndDate) ValidateReportRequest(OccupancyFinancialReportRequest request)
    {
        if (request.Year is < 2000 or > 2100)
        {
            throw new ValidationException("Reports.Period.YearInvalid", "El año del reporte no es válido.", new { request.Year });
        }

        if (request.Weekdays.Count == 0)
        {
            throw new ValidationException("Reports.Weekdays.Required", "Seleccione al menos un día de la semana para calcular ocupación.");
        }

        if (request.Weekdays.Distinct().Count() != request.Weekdays.Count)
        {
            throw new ValidationException("Reports.Weekdays.Duplicate", "Los días de la semana seleccionados no deben repetirse.");
        }

        return request.PeriodType switch
        {
            ReportPeriodType.Monthly when request.Month is >= 1 and <= 12 => (new DateOnly(request.Year, request.Month.Value, 1), new DateOnly(request.Year, request.Month.Value, DateTime.DaysInMonth(request.Year, request.Month.Value))),
            ReportPeriodType.Monthly => throw new ValidationException("Reports.Period.MonthInvalid", "El mes del reporte mensual debe estar entre 1 y 12.", new { request.Month }),
            ReportPeriodType.Annual => (new DateOnly(request.Year, 1, 1), new DateOnly(request.Year, 12, 31)),
            _ => throw new ValidationException("Reports.Period.TypeInvalid", "El tipo de periodo del reporte no es válido.", new { request.PeriodType }),
        };
    }

    private static int CountDays(DateOnly startDate, DateOnly endDate, HashSet<DayOfWeek> weekdays)
    {
        var count = 0;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (weekdays.Contains(date.DayOfWeek))
            {
                count++;
            }
        }

        return count;
    }

    private async IAsyncEnumerable<IReadOnlyList<object?>> StreamRows(string entity, IReadOnlyList<string> fields)
    {
        switch (entity.ToLowerInvariant())
        {
            case "areas":
                await foreach (var item in _dbContext.Areas.AsNoTracking().AsAsyncEnumerable())
                {
                    yield return Project(fields, field => field switch
                    {
                        "id" => item.Id,
                        "name" => item.Name,
                        "checkInTime" => item.CheckInTime,
                        "checkOutTime" => item.CheckOutTime,
                        "isActive" => item.IsActive,
                        _ => null,
                    });
                }

                break;
            case "rooms":
                await foreach (var item in _dbContext.Rooms.AsNoTracking().Include(room => room.Area).AsAsyncEnumerable())
                {
                    yield return Project(fields, field => field switch
                    {
                        "id" => item.Id,
                        "area" => item.Area?.Name,
                        "name" => item.Name,
                        "capacity" => item.Capacity,
                        "unitCount" => item.UnitCount,
                        "nightlyFare" => item.NightlyFare,
                        "description" => item.Description,
                        "displayOrder" => item.DisplayOrder,
                        "isActive" => item.IsActive,
                        _ => null,
                    });
                }

                break;
            case "clients":
                await foreach (var item in _dbContext.Clients.AsNoTracking().AsAsyncEnumerable())
                {
                    yield return Project(fields, field => field switch
                    {
                        "id" => item.Id,
                        "name" => item.Name,
                        "email" => item.Email,
                        "phone" => item.Phone,
                        "cellphone" => item.Cellphone,
                        "isVip" => item.IsVip,
                        "isActive" => item.IsActive,
                        _ => null,
                    });
                }

                break;
            case "reservations":
                await foreach (var item in _dbContext.Reservations.AsNoTracking().Include(r => r.Client).Include(r => r.Room).ThenInclude(r => r!.Area).Include(r => r.Status).AsAsyncEnumerable())
                {
                    yield return Project(fields, field => field switch
                    {
                        "id" => item.Id,
                        "client" => item.Client?.Name,
                        "room" => item.Room?.Name,
                        "area" => item.Room?.Area?.Name,
                        "entryDate" => item.EntryDate,
                        "exitDate" => item.ExitDate,
                        "checkInTime" => item.CheckInTime,
                        "checkOutTime" => item.CheckOutTime,
                        "adults" => item.Adults,
                        "children" => item.Children,
                        "infants" => item.Infants,
                        "pets" => item.Pets,
                        "fare" => item.Fare,
                        "status" => item.Status?.Label,
                        "notes" => item.Notes,
                        "createdAt" => item.CreatedAt,
                        "isActive" => item.IsActive,
                        _ => null,
                    });
                }

                break;
            case "movements":
                await foreach (var item in _dbContext.Movements.AsNoTracking().Include(m => m.Concept).Include(m => m.PaymentMethod).Include(m => m.PaymentLocation).AsAsyncEnumerable())
                {
                    yield return Project(fields, field => field switch
                    {
                        "id" => item.Id,
                        "reservationId" => item.ReservationId,
                        "concept" => item.Concept?.Name,
                        "paymentMethod" => item.PaymentMethod?.Name,
                        "paymentLocation" => item.PaymentLocation?.Name,
                        "charge" => item.Charge,
                        "payment" => item.Payment,
                        "date" => item.Date,
                        "createdAt" => item.CreatedAt,
                        _ => null,
                    });
                }

                break;
            case "concepts":
                await foreach (var item in _dbContext.Concepts.AsNoTracking().AsAsyncEnumerable())
                {
                    yield return Project(fields, field => field switch
                    {
                        "id" => item.Id,
                        "code" => item.Code,
                        "name" => item.Name,
                        "isDiscount" => item.IsDiscount,
                        "isProtected" => item.IsProtected,
                        "isActive" => item.IsActive,
                        _ => null,
                    });
                }

                break;
            case "paymentmethods":
                await foreach (var item in _dbContext.PaymentMethods.AsNoTracking().AsAsyncEnumerable())
                {
                    yield return Project(fields, field => field switch
                    {
                        "id" => item.Id,
                        "code" => item.Code,
                        "name" => item.Name,
                        "isActive" => item.IsActive,
                        _ => null,
                    });
                }

                break;
            case "paymentlocations":
                await foreach (var item in _dbContext.PaymentLocations.AsNoTracking().AsAsyncEnumerable())
                {
                    yield return Project(fields, field => field switch
                    {
                        "id" => item.Id,
                        "code" => item.Code,
                        "name" => item.Name,
                        "isActive" => item.IsActive,
                        _ => null,
                    });
                }

                break;
            case "reservationstatuses":
                await foreach (var item in _dbContext.ReservationStatuses.AsNoTracking().AsAsyncEnumerable())
                {
                    yield return Project(fields, field => field switch
                    {
                        "id" => item.Id,
                        "code" => item.Code,
                        "label" => item.Label,
                        "sortOrder" => item.SortOrder,
                        _ => null,
                    });
                }

                break;
        }
    }

    private static object?[] Project(IReadOnlyList<string> fields, Func<string, object?> valueFactory)
    {
        return fields.Select(valueFactory).ToArray();
    }
}
