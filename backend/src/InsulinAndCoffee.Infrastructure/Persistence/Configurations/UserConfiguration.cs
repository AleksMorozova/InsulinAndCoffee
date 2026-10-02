using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.HasKey(u => u.Id);
        entity.HasIndex(u => u.NormalizedUsername).IsUnique();
        entity.Property(u => u.Username).HasMaxLength(50).IsRequired();
        entity.Property(u => u.NormalizedUsername).HasMaxLength(50).IsRequired();
        entity.Property(u => u.PasswordHash).HasMaxLength(500);
        entity.Property(u => u.GoogleSubject).HasMaxLength(255);
        entity.HasIndex(u => u.GoogleSubject).IsUnique();
        entity.Property(u => u.Name).HasMaxLength(120);
        entity.Property(u => u.Email).HasMaxLength(200);
    }
}
