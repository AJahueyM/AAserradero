using AntiguoAserradero.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntiguoAserradero.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.ExternalId).HasMaxLength(64).IsRequired();
        builder.Property(user => user.UserName).HasMaxLength(128).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.IsActive).HasDefaultValue(true);
        builder.HasIndex(user => user.ExternalId).IsUnique();
    }
}
