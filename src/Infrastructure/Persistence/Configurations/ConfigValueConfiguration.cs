using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiguoAserradero.Infrastructure.Persistence.Configurations;

public sealed class ConfigValueConfiguration : IEntityTypeConfiguration<ConfigValue>
{
    private static readonly DateTime SeedUpdatedAtUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<ConfigValue> builder)
    {
        builder.ToTable("ConfigValues", table =>
        {
            table.HasCheckConstraint("CK_ConfigValues_Key_AllowList", "[Key] IN ('PaymentInstructions')");
        });
        builder.HasKey(config => config.Id);
        builder.Property(config => config.Key).HasMaxLength(128).IsRequired();
        builder.Property(config => config.Value).HasMaxLength(4000).IsRequired();
        builder.Property(config => config.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(config => config.Key).IsUnique();
        builder.HasData(new ConfigValue { Id = 1, Key = ConfigKeys.PaymentInstructions, Value = string.Empty, UpdatedAt = SeedUpdatedAtUtc });
    }
}
