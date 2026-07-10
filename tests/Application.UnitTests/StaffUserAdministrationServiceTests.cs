using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Auth;
using AntiguoAserradero.Application.Users;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using AntiguoAserradero.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AntiguoAserradero.Application.UnitTests;

public sealed class StaffUserAdministrationServiceTests
{
    [Fact]
    public async Task UserAdministrationRequiresBothCapabilities()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new FakeStaffDirectory(), new FakeCurrentUser([ApplicationCapability.CatalogManage]));

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => service.ListAsync());

        Assert.Equal("Auth.Forbidden", exception.Code);
    }

    [Fact]
    public async Task CreateValidatesEmailFormat()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.CreateAsync(new CreateStaffUserRequest("not-an-email", "Ana Admin", "ValidPassword123!")));

        Assert.Contains(exception.Errors, error => error.PropertyName == "Email");
    }

    [Fact]
    public async Task CreateUpsertsLocalUserFromDirectoryIdentity()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new User
        {
            ExternalId = "old-external-id",
            UserName = "ana@example.com",
            DisplayName = "Ana",
            IsActive = false,
        });
        await dbContext.SaveChangesAsync();
        var directory = new FakeStaffDirectory
        {
            CreatedUser = new StaffDirectoryUser("new-external-id", "ana@example.com", "Ana Admin", true, []),
        };
        var service = CreateService(dbContext, directory);

        var response = await service.CreateAsync(new CreateStaffUserRequest(" ANA@example.com ", " Ana Admin ", "ValidPassword123!"));

        var user = await dbContext.Users.SingleAsync();
        Assert.Equal(user.Id, response.Id);
        Assert.Equal("new-external-id", user.ExternalId);
        Assert.Equal("ana@example.com", user.UserName);
        Assert.Equal("Ana Admin", user.DisplayName);
        Assert.True(user.IsActive);
        Assert.Equal("ana@example.com", directory.LastCreatedEmail);
    }

    [Fact]
    public async Task AssignCapabilityUsesDirectoryAppRoleMappingPort()
    {
        await using var dbContext = CreateDbContext();
        var user = new User
        {
            ExternalId = "11111111-1111-1111-1111-111111111111",
            UserName = "ana@example.com",
            DisplayName = "Ana Admin",
            IsActive = true,
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var directory = new FakeStaffDirectory();
        var service = CreateService(dbContext, directory);

        var response = await service.AssignCapabilityAsync(user.Id, new StaffUserCapabilityRequest(ApplicationCapability.ReservationsManage));

        Assert.Equal((user.ExternalId, ApplicationCapability.ReservationsManage), directory.LastAssignedCapability);
        Assert.Contains(ApplicationCapability.ReservationsManage, response.AssignedCapabilities);
    }

    private static AntiguoAserraderoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AntiguoAserraderoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AntiguoAserraderoDbContext(options);
    }

    private static StaffUserAdministrationService CreateService(
        AntiguoAserraderoDbContext dbContext,
        FakeStaffDirectory? directory = null,
        ICurrentUser? currentUser = null)
    {
        return new StaffUserAdministrationService(
            dbContext,
            directory ?? new FakeStaffDirectory(),
            currentUser ?? new FakeCurrentUser([ApplicationCapability.CatalogManage, ApplicationCapability.ReservationsManage]),
            new StubCurrentStaffResolver(),
            NullLogger<StaffUserAdministrationService>.Instance);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public FakeCurrentUser(IReadOnlyCollection<string> capabilities)
        {
            Capabilities = capabilities;
        }

        public string? Id => "actor-external-id";

        public string DisplayName => "Actor Admin";

        public IReadOnlyCollection<string> Capabilities { get; }

        public bool IsAuthenticated => true;
    }

    private sealed class StubCurrentStaffResolver : ICurrentStaffResolver
    {
        public Task<User> GetOrCreateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new User
            {
                Id = 100,
                ExternalId = "actor-external-id",
                UserName = "actor@example.com",
                DisplayName = "Actor Admin",
                IsActive = true,
            });
        }
    }

    private sealed class FakeStaffDirectory : IStaffDirectory
    {
        private readonly Dictionary<string, HashSet<string>> _capabilities = new(StringComparer.Ordinal);

        public StaffDirectoryUser CreatedUser { get; set; } = new("new-external-id", "ana@example.com", "Ana Admin", true, []);

        public string? LastCreatedEmail { get; private set; }

        public (string ExternalId, string Capability)? LastAssignedCapability { get; private set; }

        public Task<IReadOnlyDictionary<string, StaffDirectoryUser>> GetUsersByExternalIdsAsync(IReadOnlyCollection<string> externalIds, CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<string, StaffDirectoryUser> users = externalIds.ToDictionary(
                id => id,
                id => new StaffDirectoryUser(id, $"{id}@example.com", id, true, GetCapabilityArray(id)),
                StringComparer.Ordinal);
            return Task.FromResult(users);
        }

        public Task<StaffDirectoryUser> CreateUserAsync(string email, string displayName, string initialPassword, CancellationToken cancellationToken = default)
        {
            LastCreatedEmail = email;
            return Task.FromResult(CreatedUser with { UserName = email, DisplayName = displayName });
        }

        public Task<StaffDirectoryUser> UpdateUserAsync(string externalId, string? displayName, bool? isActive, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new StaffDirectoryUser(externalId, $"{externalId}@example.com", displayName ?? externalId, isActive ?? true, GetCapabilityArray(externalId)));
        }

        public Task<IReadOnlyList<string>> GetCapabilitiesAsync(string externalId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> capabilities = GetCapabilityArray(externalId);
            return Task.FromResult(capabilities);
        }

        public Task AssignCapabilityAsync(string externalId, string capability, CancellationToken cancellationToken = default)
        {
            LastAssignedCapability = (externalId, capability);
            GetCapabilities(externalId).Add(capability);
            return Task.CompletedTask;
        }

        public Task RemoveCapabilityAsync(string externalId, string capability, CancellationToken cancellationToken = default)
        {
            GetCapabilities(externalId).Remove(capability);
            return Task.CompletedTask;
        }

        public Task ResetPasswordAsync(string externalId, string newPassword, bool forceChangePasswordNextSignIn, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        private HashSet<string> GetCapabilities(string externalId)
        {
            if (!_capabilities.TryGetValue(externalId, out var capabilities))
            {
                capabilities = new HashSet<string>(StringComparer.Ordinal);
                _capabilities[externalId] = capabilities;
            }

            return capabilities;
        }

        private string[] GetCapabilityArray(string externalId)
        {
            return GetCapabilities(externalId).Order(StringComparer.Ordinal).ToArray();
        }
    }
}
