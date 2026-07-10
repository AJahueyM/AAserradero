using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Clients;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.UnitTests;

public sealed class ClientServiceTests
{
    [Fact]
    public async Task CreateRequiresNameAndCellphone()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationStatusesAsync(dbContext);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<AntiguoAserradero.Domain.Errors.ValidationException>(() =>
            service.CreateAsync(new CreateClientRequest("  ", null, null, "correo@ejemplo.com", null, "  ")));

        Assert.Equal("Client.ValidationFailed", exception.Code);
    }

    [Fact]
    public async Task UpdateRequiresBlacklistReasonWhenBlacklisted()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationStatusesAsync(dbContext);
        var client = new Client { Name = "Ana López", Cellphone = "6141234567", IsActive = true };
        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<AntiguoAserradero.Domain.Errors.ValidationException>(() =>
            service.UpdateAsync(client.Id, new UpdateClientRequest("Ana López", null, null, null, null, "6141234567", false, true, " ")));

        Assert.Equal("Client.ValidationFailed", exception.Code);
    }

    [Fact]
    public async Task SearchFiltersVipClients()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationStatusesAsync(dbContext);
        dbContext.Clients.AddRange(
            new Client { Name = "Ana López", Cellphone = "6141234567", IsVip = true, IsActive = true },
            new Client { Name = "Luis Pérez", Cellphone = "6147654321", IsVip = false, IsActive = true });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var response = await service.SearchAsync(null, true, 1, 20);

        Assert.Equal(1, response.Total);
        var item = Assert.Single(response.Items);
        Assert.True(item.IsVip);
        Assert.Equal("Ana López", item.Name);
    }

    [Fact]
    public async Task SearchPaginatesResultsWithTotal()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationStatusesAsync(dbContext);
        dbContext.Clients.AddRange(
            new Client { Name = "Cliente 01", Cellphone = "6140000001", IsActive = true },
            new Client { Name = "Cliente 02", Cellphone = "6140000002", IsActive = true },
            new Client { Name = "Cliente 03", Cellphone = "6140000003", IsActive = true },
            new Client { Name = "Cliente 04", Cellphone = "6140000004", IsActive = true },
            new Client { Name = "Cliente 05", Cellphone = "6140000005", IsActive = true });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var response = await service.SearchAsync("cliente", null, 2, 2);

        Assert.Equal(5, response.Total);
        Assert.Equal(2, response.Page);
        Assert.Equal(2, response.PageSize);
        Assert.Collection(response.Items,
            first => Assert.Equal("Cliente 03", first.Name),
            second => Assert.Equal("Cliente 04", second.Name));
    }

    [Fact]
    public async Task GetIncludesRecentNonCancelledActivityCount()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationStatusesAsync(dbContext);
        var client = new Client { Name = "Ana López", Cellphone = "6141234567", IsActive = true };
        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync();
        dbContext.Reservations.AddRange(
            new Reservation { ClientId = client.Id, EntryDate = DateTime.UtcNow.AddMonths(-1), ExitDate = DateTime.UtcNow.AddMonths(-1).AddDays(2), StatusId = 1 },
            new Reservation { ClientId = client.Id, EntryDate = DateTime.UtcNow.AddMonths(-2), ExitDate = DateTime.UtcNow.AddMonths(-2).AddDays(2), StatusId = 6 },
            new Reservation { ClientId = client.Id, EntryDate = DateTime.UtcNow.AddMonths(-13), ExitDate = DateTime.UtcNow.AddMonths(-13).AddDays(2), StatusId = 1 });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var response = await service.GetAsync(client.Id);

        Assert.Equal(1, response.RecentActivityCount);
    }

    private static AntiguoAserraderoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AntiguoAserraderoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AntiguoAserraderoDbContext(options);
    }

    private static ClientService CreateService(AntiguoAserraderoDbContext dbContext)
    {
        return new ClientService(dbContext, new StubCurrentStaffResolver());
    }

    private static async Task SeedReservationStatusesAsync(AntiguoAserraderoDbContext dbContext)
    {
        dbContext.ReservationStatuses.AddRange(
            new ReservationStatus { Id = 1, Code = ReservationStatusCodes.Pending, Label = "Oferta", SortOrder = 10 },
            new ReservationStatus { Id = 6, Code = ReservationStatusCodes.Cancelled, Label = "Cancelada", SortOrder = 60 });
        await dbContext.SaveChangesAsync();
    }

    private sealed class StubCurrentStaffResolver : ICurrentStaffResolver
    {
        public Task<User> GetOrCreateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new User
            {
                Id = 1,
                ExternalId = "test-user",
                UserName = "Test User",
                DisplayName = "Test User",
                IsActive = true,
            });
        }
    }
}
