using AntiguoAserradero.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.Abstractions;

/// <summary>
/// Data-access port used by Application-layer feature services so business logic lives in
/// Application (not Infrastructure) while remaining testable (e.g. with the EF Core InMemory
/// provider). The concrete EF Core DbContext in Infrastructure implements this.
///
/// Atomicity: make all related changes and call <see cref="SaveChangesAsync"/> exactly once —
/// EF Core wraps a single SaveChanges in one transaction on relational providers (e.g. create a
/// reservation and its initial lodging charge together, then save once).
///
/// All <see cref="System.DateTime"/> values are persisted in UTC by convention.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Area> Areas { get; }
    DbSet<Room> Rooms { get; }
    DbSet<Client> Clients { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<Movement> Movements { get; }
    DbSet<Concept> Concepts { get; }
    DbSet<PaymentMethod> PaymentMethods { get; }
    DbSet<PaymentLocation> PaymentLocations { get; }
    DbSet<ReservationStatus> ReservationStatuses { get; }
    DbSet<ConfigValue> ConfigValues { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
