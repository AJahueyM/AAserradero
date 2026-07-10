using AntiguoAserradero.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiguoAserradero.Infrastructure.Persistence.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients", table =>
        {
            table.HasCheckConstraint("CK_Clients_BlacklistReason_Required", "[IsBlacklisted] = 0 OR LEN(LTRIM(RTRIM(COALESCE([BlacklistReason], '')))) > 0");
        });
        builder.HasKey(client => client.Id);
        builder.Property(client => client.Name).HasMaxLength(200).IsRequired();
        builder.Property(client => client.TaxId).HasMaxLength(32);
        builder.Property(client => client.Address).HasMaxLength(500);
        builder.Property(client => client.Email).HasMaxLength(254);
        builder.Property(client => client.Phone).HasMaxLength(50);
        builder.Property(client => client.Cellphone).HasMaxLength(50).IsRequired();
        builder.Property(client => client.IsVip).HasDefaultValue(false);
        builder.Property(client => client.IsBlacklisted).HasDefaultValue(false);
        builder.Property(client => client.BlacklistReason).HasMaxLength(1000);
        builder.Property(client => client.IsActive).HasDefaultValue(true);
        builder.HasIndex(client => client.Name);
    }
}
