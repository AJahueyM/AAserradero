using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiguoAserradero.Infrastructure.Persistence.Configurations;

public sealed class PaymentLocationConfiguration : IEntityTypeConfiguration<PaymentLocation>
{
    public void Configure(EntityTypeBuilder<PaymentLocation> builder)
    {
        builder.ToTable("PaymentLocations");
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Code).HasMaxLength(64).IsRequired();
        builder.Property(location => location.Name).HasMaxLength(120).IsRequired();
        builder.Property(location => location.IsActive).HasDefaultValue(true);
        builder.HasIndex(location => location.Code).IsUnique();
        builder.HasData(
            new PaymentLocation { Id = 1, Code = BillingSeedCodes.DefaultPaymentLocation, Name = "Recepción", IsActive = true },
            new PaymentLocation { Id = 2, Code = "BANK", Name = "Banco", IsActive = true },
            new PaymentLocation { Id = 3, Code = "ONLINE", Name = "En línea", IsActive = true });
    }
}
