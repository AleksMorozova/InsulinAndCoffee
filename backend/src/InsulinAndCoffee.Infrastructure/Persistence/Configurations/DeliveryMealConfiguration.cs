using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public class DeliveryMealConfiguration : IEntityTypeConfiguration<DeliveryMeal>
{
    public void Configure(EntityTypeBuilder<DeliveryMeal> entity)
    {
        entity.HasKey(k => k.Id);
        entity.HasIndex(k => new { k.UserId, k.PlaceName, k.DishName });
        entity.HasIndex(k => new { k.UserId, k.IsFavorite });
        entity.HasIndex(k => new { k.UserId, k.UsageCount });
        entity.Property(k => k.PlaceName).HasMaxLength(180).IsRequired();
        entity.Property(k => k.DishName).HasMaxLength(180).IsRequired();
        entity.Property(k => k.PortionDescription).HasMaxLength(220).IsRequired();
        entity.Property(k => k.Carbs).HasPrecision(8, 2);
        entity.Property(k => k.UsualInsulinUnits).HasPrecision(6, 2);
        entity.Property(k => k.LastPreMealGlucose).HasPrecision(6, 2);
        entity.Property(k => k.Tags).HasMaxLength(400);
        entity.Property(k => k.Notes).HasMaxLength(1000);
    }
}