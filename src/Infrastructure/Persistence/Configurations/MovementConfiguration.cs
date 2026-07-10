using AntiguoAserradero.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiguoAserradero.Infrastructure.Persistence.Configurations;

public sealed class MovementConfiguration : IEntityTypeConfiguration<Movement>
{
    public void Configure(EntityTypeBuilder<Movement> builder)
    {
        builder.ToTable("Movements", table =>
        {
            table.HasCheckConstraint("CK_Movements_Charge_NonNegative", "[Charge] >= 0");
            table.HasCheckConstraint("CK_Movements_Payment_NonNegative", "[Payment] >= 0");
            table.HasCheckConstraint("CK_Movements_ChargeOrPayment", "[Charge] > 0 OR [Payment] > 0");
        });
        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.Charge).HasColumnType("decimal(18,2)");
        builder.Property(movement => movement.Payment).HasColumnType("decimal(18,2)");
        builder.Property(movement => movement.Date).IsRequired();
        builder.Property(movement => movement.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(movement => movement.Reservation)
            .WithMany(reservation => reservation.Movements)
            .HasForeignKey(movement => movement.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.Concept)
            .WithMany()
            .HasForeignKey(movement => movement.ConceptId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.PaymentMethod)
            .WithMany()
            .HasForeignKey(movement => movement.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.PaymentLocation)
            .WithMany()
            .HasForeignKey(movement => movement.PaymentLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.ResponsibleUser)
            .WithMany()
            .HasForeignKey(movement => movement.ResponsibleUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(movement => movement.ReservationId);
    }
}
