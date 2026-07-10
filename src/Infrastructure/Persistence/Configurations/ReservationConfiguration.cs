using AntiguoAserradero.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiguoAserradero.Infrastructure.Persistence.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations", table =>
        {
            table.HasCheckConstraint("CK_Reservations_DateRange", "[EntryDate] < [ExitDate]");
            table.HasCheckConstraint("CK_Reservations_Occupants_NonNegative", "[Adults] >= 0 AND [Children] >= 0 AND [Infants] >= 0 AND [Pets] >= 0");
            table.HasCheckConstraint("CK_Reservations_Fare_NonNegative", "[Fare] >= 0");
        });
        builder.HasKey(reservation => reservation.Id);
        builder.Property(reservation => reservation.EntryDate).IsRequired();
        builder.Property(reservation => reservation.ExitDate).IsRequired();
        builder.Property(reservation => reservation.CheckInTime).HasColumnType("time");
        builder.Property(reservation => reservation.CheckOutTime).HasColumnType("time");
        builder.Property(reservation => reservation.Fare).HasColumnType("decimal(18,2)");
        builder.Property(reservation => reservation.Notes).HasMaxLength(2000);
        builder.Property(reservation => reservation.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(reservation => reservation.IsActive).HasDefaultValue(true);
        builder.HasOne(reservation => reservation.Client)
            .WithMany()
            .HasForeignKey(reservation => reservation.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(reservation => reservation.Room)
            .WithMany()
            .HasForeignKey(reservation => reservation.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(reservation => reservation.Status)
            .WithMany()
            .HasForeignKey(reservation => reservation.StatusId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(reservation => reservation.Promotor)
            .WithMany()
            .HasForeignKey(reservation => reservation.PromotorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(reservation => reservation.CreatedBy)
            .WithMany()
            .HasForeignKey(reservation => reservation.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(reservation => new { reservation.RoomId, reservation.EntryDate, reservation.ExitDate });
        builder.HasIndex(reservation => reservation.EntryDate);
        builder.HasIndex(reservation => reservation.ExitDate);
    }
}
