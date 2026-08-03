using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public class SupplyItemConfiguration : IEntityTypeConfiguration<SupplyItem>
{
    public void Configure(EntityTypeBuilder<SupplyItem> entity)
    {
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => new { item.UserId, item.Name });
        entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
        entity.Property(item => item.Unit).HasMaxLength(40).IsRequired();
        entity.Property(item => item.CurrentQuantity).HasPrecision(12, 4);
        entity.Property(item => item.DailyUsage).HasPrecision(12, 4);
        entity.HasOne(item => item.User)
            .WithMany(user => user.SupplyItems)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}