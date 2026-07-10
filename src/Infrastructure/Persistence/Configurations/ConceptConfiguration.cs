using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiguoAserradero.Infrastructure.Persistence.Configurations;

public sealed class ConceptConfiguration : IEntityTypeConfiguration<Concept>
{
    public void Configure(EntityTypeBuilder<Concept> builder)
    {
        builder.ToTable("Concepts");
        builder.HasKey(concept => concept.Id);
        builder.Property(concept => concept.Code).HasMaxLength(64).IsRequired();
        builder.Property(concept => concept.Name).HasMaxLength(160).IsRequired();
        builder.Property(concept => concept.IsDiscount).HasDefaultValue(false);
        builder.Property(concept => concept.IsProtected).HasDefaultValue(false);
        builder.Property(concept => concept.IsActive).HasDefaultValue(true);
        builder.HasIndex(concept => concept.Code).IsUnique();
        builder.HasData(
            new Concept { Id = 1, Code = BillingSeedCodes.LodgingConcept, Name = "Hospedaje", IsDiscount = false, IsProtected = true, IsActive = true },
            new Concept { Id = 2, Code = BillingSeedCodes.DiscountConcept, Name = "Descuento", IsDiscount = true, IsProtected = true, IsActive = true });
    }
}
