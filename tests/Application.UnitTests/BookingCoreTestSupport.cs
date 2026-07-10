using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Movements;
using AntiguoAserradero.Application.Reservations;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.UnitTests;

internal sealed class BookingCoreTestContext : IDisposable
{
    private BookingCoreTestContext(AntiguoAserraderoDbContext dbContext)
    {
        DbContext = dbContext;
        AppDbContext = new CountingApplicationDbContext(dbContext);
        SeedCoreData();

        StaffResolver = new TestStaffResolver(StaffUser);
        Notifier = new TestReservationLiveUpdateNotifier();
        FinancialService = new ReservationFinancialService(AppDbContext);
        ReservationService = new ReservationService(AppDbContext, StaffResolver, FinancialService, Notifier);
        MovementService = new MovementService(AppDbContext, StaffResolver, FinancialService, Notifier);
        AppDbContext.ResetSaveCount();
    }

    public AntiguoAserraderoDbContext DbContext { get; }

    public CountingApplicationDbContext AppDbContext { get; }

    public User StaffUser { get; private set; } = null!;

    public User PromotorUser { get; private set; } = null!;

    public Room Room { get; private set; } = null!;

    public Room SecondRoom { get; private set; } = null!;

    public Client Client { get; private set; } = null!;

    public TestStaffResolver StaffResolver { get; }

    public TestReservationLiveUpdateNotifier Notifier { get; }

    public ReservationFinancialService FinancialService { get; }

    public ReservationService ReservationService { get; }

    public MovementService MovementService { get; }

    public static BookingCoreTestContext Create()
    {
        var options = new DbContextOptionsBuilder<AntiguoAserraderoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new AntiguoAserraderoDbContext(options);
        dbContext.Database.EnsureCreated();
        return new BookingCoreTestContext(dbContext);
    }

    public CreateReservationRequest NewCreateRequest(
        DateTime? entryDate = null,
        DateTime? exitDate = null,
        int? roomId = null,
        int adults = 2,
        int children = 0,
        int infants = 0,
        int pets = 0,
        decimal? fare = null,
        string? statusCode = null)
    {
        return new CreateReservationRequest(
            roomId ?? Room.Id,
            Client.Id,
            entryDate ?? new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            exitDate ?? new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc),
            adults,
            children,
            infants,
            pets,
            fare,
            statusCode,
            PromotorUser.Id,
            "Notas");
    }

    public static UpdateReservationRequest NewUpdateRequest(ReservationDetailResponse reservation, DateTime? entryDate = null, DateTime? exitDate = null, int? roomId = null)
    {
        return new UpdateReservationRequest(
            roomId ?? reservation.Room.Id,
            reservation.Client.Id,
            entryDate ?? reservation.EntryDate,
            exitDate ?? reservation.ExitDate,
            reservation.Adults,
            reservation.Children,
            reservation.Infants,
            reservation.Pets,
            reservation.Fare,
            reservation.Status.Code,
            reservation.PromotorId,
            reservation.Notes);
    }

    public int StatusId(string code)
    {
        return DbContext.ReservationStatuses.Single(status => status.Code == code).Id;
    }

    public int ConceptId(string code)
    {
        return DbContext.Concepts.Single(concept => concept.Code == code).Id;
    }

    public void Dispose()
    {
        DbContext.Dispose();
    }

    private void SeedCoreData()
    {
        var area = new Area
        {
            Name = "Cabañas",
            CheckInTime = new TimeOnly(15, 0),
            CheckOutTime = new TimeOnly(12, 0),
            ReceptionOpenTime = new TimeOnly(8, 0),
            ReceptionCloseTime = new TimeOnly(22, 0),
            IsActive = true,
        };
        StaffUser = new User { ExternalId = "staff", UserName = "staff", DisplayName = "Staff", IsActive = true };
        PromotorUser = new User { ExternalId = "promotor", UserName = "promotor", DisplayName = "Promotor", IsActive = true };
        Client = new Client { Name = "Cliente Prueba", Cellphone = "555-0100", Phone = "555-0000", IsActive = true };
        Room = new Room { Area = area, Name = "Cabaña 1", Capacity = 4, UnitCount = 1, NightlyFare = 1500m, IsActive = true };
        SecondRoom = new Room { Area = area, Name = "Cabaña 2", Capacity = 2, UnitCount = 1, NightlyFare = 900m, IsActive = true };

        DbContext.AddRange(area, StaffUser, PromotorUser, Client, Room, SecondRoom);
        DbContext.SaveChanges();
    }
}

internal sealed class CountingApplicationDbContext : IApplicationDbContext
{
    private readonly AntiguoAserraderoDbContext _dbContext;

    public CountingApplicationDbContext(AntiguoAserraderoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public int SaveChangesAsyncCallCount { get; private set; }

    public DbSet<User> Users => _dbContext.Users;

    public DbSet<Area> Areas => _dbContext.Areas;

    public DbSet<Room> Rooms => _dbContext.Rooms;

    public DbSet<Client> Clients => _dbContext.Clients;

    public DbSet<Reservation> Reservations => _dbContext.Reservations;

    public DbSet<Movement> Movements => _dbContext.Movements;

    public DbSet<Concept> Concepts => _dbContext.Concepts;

    public DbSet<PaymentMethod> PaymentMethods => _dbContext.PaymentMethods;

    public DbSet<PaymentLocation> PaymentLocations => _dbContext.PaymentLocations;

    public DbSet<ReservationStatus> ReservationStatuses => _dbContext.ReservationStatuses;

    public DbSet<ConfigValue> ConfigValues => _dbContext.ConfigValues;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesAsyncCallCount++;
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void ResetSaveCount()
    {
        SaveChangesAsyncCallCount = 0;
    }
}

internal sealed class TestStaffResolver : ICurrentStaffResolver
{
    public TestStaffResolver(User user)
    {
        User = user;
    }

    public User User { get; }

    public Task<User> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(User);
    }
}

internal sealed class TestReservationLiveUpdateNotifier : IReservationLiveUpdateNotifier
{
    public List<string> Events { get; } = [];

    public Task PublishReservationChangedAsync(Reservation reservation, string action, CancellationToken cancellationToken = default)
    {
        Events.Add($"reservation:{action}:{reservation.Id}");
        return Task.CompletedTask;
    }

    public Task PublishMovementChangedAsync(Reservation reservation, int? movementId, string action, CancellationToken cancellationToken = default)
    {
        Events.Add($"movement:{action}:{reservation.Id}:{movementId}");
        return Task.CompletedTask;
    }
}
