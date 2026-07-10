using AntiguoAserradero.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiguoAserradero.Infrastructure.Persistence.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms", table =>
        {
            table.HasCheckConstraint("CK_Rooms_Capacity_NonNegative", "[Capacity] >= 0");
            table.HasCheckConstraint("CK_Rooms_UnitCount_NonNegative", "[UnitCount] >= 0");
            table.HasCheckConstraint("CK_Rooms_NightlyFare_NonNegative", "[NightlyFare] >= 0");
        });
        builder.HasKey(room => room.Id);
        builder.Property(room => room.Name).HasMaxLength(120).IsRequired();
        builder.Property(room => room.NightlyFare).HasColumnType("decimal(18,2)");
        builder.Property(room => room.Description).HasMaxLength(1000);
        builder.Property(room => room.IsActive).HasDefaultValue(true);
        builder.HasOne(room => room.Area)
            .WithMany(area => area.Rooms)
            .HasForeignKey(room => room.AreaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(room => new { room.AreaId, room.Name }).IsUnique();
        builder.HasIndex(room => room.DisplayOrder);
    }
}
