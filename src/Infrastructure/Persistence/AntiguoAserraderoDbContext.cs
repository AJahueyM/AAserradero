using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Infrastructure.Persistence;

public sealed class AntiguoAserraderoDbContext : DbContext
{
    public AntiguoAserraderoDbContext(DbContextOptions<AntiguoAserraderoDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Movement> Movements => Set<Movement>();
    public DbSet<Concept> Concepts => Set<Concept>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<PaymentLocation> PaymentLocations => Set<PaymentLocation>();
    public DbSet<ReservationStatus> ReservationStatuses => Set<ReservationStatus>();
    public DbSet<ConfigValue> ConfigValues => Set<ConfigValue>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcNullableDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AntiguoAserraderoDbContext).Assembly);
    }
}
