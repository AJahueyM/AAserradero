using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiguoAserradero.Infrastructure.Persistence.Configurations;

public sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("PaymentMethods");
        builder.HasKey(method => method.Id);
        builder.Property(method => method.Code).HasMaxLength(64).IsRequired();
        builder.Property(method => method.Name).HasMaxLength(120).IsRequired();
        builder.Property(method => method.IsActive).HasDefaultValue(true);
        builder.HasIndex(method => method.Code).IsUnique();
        builder.HasData(
            new PaymentMethod { Id = 1, Code = BillingSeedCodes.DefaultPaymentMethod, Name = "Efectivo", IsActive = true },
            new PaymentMethod { Id = 2, Code = "TRANSFER", Name = "Transferencia", IsActive = true },
            new PaymentMethod { Id = 3, Code = "CARD", Name = "Tarjeta", IsActive = true });
    }
}
