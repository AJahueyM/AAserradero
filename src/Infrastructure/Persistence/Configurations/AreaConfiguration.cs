using AntiguoAserradero.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiguoAserradero.Infrastructure.Persistence.Configurations;

public sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Areas");
        builder.HasKey(area => area.Id);
        builder.Property(area => area.Name).HasMaxLength(120).IsRequired();
        builder.Property(area => area.CheckInTime).HasColumnType("time");
        builder.Property(area => area.CheckOutTime).HasColumnType("time");
        builder.Property(area => area.ReceptionOpenTime).HasColumnType("time");
        builder.Property(area => area.ReceptionCloseTime).HasColumnType("time");
        builder.Property(area => area.IsActive).HasDefaultValue(true);
    }
}
