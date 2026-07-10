using AntiguoAserradero.Application.Areas;
using AntiguoAserradero.Application.Rooms;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using AntiguoAserradero.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.UnitTests;

public sealed class CatalogAreaRoomServiceTests
{
    [Fact]
    public async Task DeactivateRoomReturnsWarningWhenFutureActiveReservationExists()
    {
        await using var dbContext = CreateDbContext();
        var status = new ReservationStatus { Id = 1, Code = ReservationStatusCodes.Pending, Label = "Oferta", SortOrder = 10 };
        var area = new Area
        {
            Id = 1,
            Name = "Cabañas",
            CheckInTime = new TimeOnly(15, 0),
            CheckOutTime = new TimeOnly(12, 0),
            ReceptionOpenTime = new TimeOnly(8, 0),
            ReceptionCloseTime = new TimeOnly(20, 0),
        };
        var room = new Room { Id = 1, AreaId = area.Id, Area = area, Name = "Pino", Capacity = 4, UnitCount = 1, NightlyFare = 1200m };
        dbContext.AddRange(status, area, room);
        dbContext.Reservations.Add(new Reservation
        {
            ClientId = 1,
            RoomId = room.Id,
            Room = room,
            EntryDate = DateTime.UtcNow.Date.AddDays(1),
            ExitDate = DateTime.UtcNow.Date.AddDays(2),
            StatusId = status.Id,
            Status = status,
            PromotorId = 1,
            CreatedById = 1,
            CreatedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var result = await new RoomService(dbContext).DeactivateAsync(room.Id);

        Assert.False(result.Room.IsActive);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task CreateRoomRejectsMissingAreaWithTypedValidation()
    {
        await using var dbContext = CreateDbContext();
        var service = new RoomService(dbContext);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(new UpsertRoomRequest(99, "Pino", 4, 1, 1200m, null, 1)));

        Assert.Equal("Room.AreaNotFound", exception.Code);
    }

    [Fact]
    public async Task AreaReceptionWindowMustBeValid()
    {
        await using var dbContext = CreateDbContext();
        var service = new AreaService(dbContext);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(new UpsertAreaRequest(
                "Cabañas",
                new TimeOnly(15, 0),
                new TimeOnly(12, 0),
                new TimeOnly(20, 0),
                new TimeOnly(8, 0))));

        Assert.Equal("Area.ReceptionWindowInvalid", exception.Code);
    }

    private static AntiguoAserraderoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AntiguoAserraderoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AntiguoAserraderoDbContext(options);
    }
}
