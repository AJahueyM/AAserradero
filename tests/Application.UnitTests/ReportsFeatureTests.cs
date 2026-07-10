using AntiguoAserradero.Application.Reports;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using AntiguoAserradero.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.UnitTests;

public sealed class ReportsFeatureTests
{
    [Fact]
    public void OccupancyCountsCheckInAndCheckOutAsPartialAndHonorsWeekdays()
    {
        var reservation = new Reservation
        {
            EntryDate = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc),
            ExitDate = new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc),
            CheckInTime = new TimeOnly(15, 0),
            CheckOutTime = new TimeOnly(12, 0),
        };

        var occupied = ReportService.CalculateOccupiedDays(
            reservation,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Thursday });

        Assert.Equal(2m, occupied);
    }

    [Fact]
    public async Task FinancialReportExcludesDiscountsFromIncome()
    {
        await using var dbContext = CreateDbContext();
        SeedReportData(dbContext);
        await dbContext.SaveChangesAsync();
        var service = new ReportService(dbContext);

        var report = await service.CreateOccupancyFinancialReportAsync(new OccupancyFinancialReportRequest(
            ReportPeriodType.Monthly,
            2026,
            7,
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday]));

        var room = Assert.Single(report.Rooms);
        Assert.Equal(500m, room.Income);
        Assert.Equal(100m, room.Discounts);
        Assert.Equal(500m, Assert.Single(report.IncomeByPaymentDate).Value);
    }

    [Fact]
    public void ExportAllowListRejectsUnknownFields()
    {
        var exception = Assert.Throws<ValidationException>(() => ExportCatalog.Validate("clients", ["name", "externalId"]));
        Assert.Equal("Reports.Export.FieldNotAllowed", exception.Code);
    }

    private static AntiguoAserraderoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AntiguoAserraderoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AntiguoAserraderoDbContext(options);
    }

    private static void SeedReportData(AntiguoAserraderoDbContext dbContext)
    {
        var area = new Area { Id = 1, Name = "Cabañas" };
        var room = new Room { Id = 1, AreaId = 1, Area = area, Name = "Roble", Capacity = 4, UnitCount = 1 };
        var client = new Client { Id = 1, Name = "Cliente", Cellphone = "555" };
        var status = new ReservationStatus { Id = 1, Code = ReservationStatusCodes.Paid, Label = "Pagada" };
        var charge = new Concept { Id = 1, Code = "LODGE", Name = "Hospedaje" };
        var discount = new Concept { Id = 2, Code = "DISC", Name = "Descuento", IsDiscount = true };
        var reservation = new Reservation
        {
            Id = 1,
            ClientId = 1,
            Client = client,
            RoomId = 1,
            Room = room,
            EntryDate = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc),
            ExitDate = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc),
            CheckInTime = new TimeOnly(15, 0),
            CheckOutTime = new TimeOnly(12, 0),
            StatusId = 1,
            Status = status,
        };

        dbContext.AddRange(area, room, client, status, charge, discount, reservation);
        dbContext.Movements.AddRange(
            new Movement { Id = 1, ReservationId = 1, Reservation = reservation, ConceptId = 1, Concept = charge, Charge = 1000m, Date = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc) },
            new Movement { Id = 2, ReservationId = 1, Reservation = reservation, ConceptId = 1, Concept = charge, Payment = 500m, Date = new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc) },
            new Movement { Id = 3, ReservationId = 1, Reservation = reservation, ConceptId = 2, Concept = discount, Payment = 100m, Date = new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc) });
    }
}
