using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiguoAserradero.Infrastructure.Persistence.Configurations;

public sealed class ReservationStatusConfiguration : IEntityTypeConfiguration<ReservationStatus>
{
    public void Configure(EntityTypeBuilder<ReservationStatus> builder)
    {
        builder.ToTable("ReservationStatuses");
        builder.HasKey(status => status.Id);
        builder.Property(status => status.Code).HasMaxLength(32).IsRequired();
        builder.Property(status => status.Label).HasMaxLength(80).IsRequired();
        builder.HasIndex(status => status.Code).IsUnique();
        builder.HasData(
            new ReservationStatus { Id = 1, Code = ReservationStatusCodes.Pending, Label = "Oferta", SortOrder = 10 },
            new ReservationStatus { Id = 2, Code = ReservationStatusCodes.Partial, Label = "Separada", SortOrder = 20 },
            new ReservationStatus { Id = 3, Code = ReservationStatusCodes.Paid, Label = "No disponible", SortOrder = 30 },
            new ReservationStatus { Id = 4, Code = ReservationStatusCodes.Maintenance, Label = "Mantenimiento", SortOrder = 40 },
            new ReservationStatus { Id = 5, Code = ReservationStatusCodes.Courtesy, Label = "Cortesía", SortOrder = 50 },
            new ReservationStatus { Id = 6, Code = ReservationStatusCodes.Cancelled, Label = "Cancelada", SortOrder = 60 });
    }
}
