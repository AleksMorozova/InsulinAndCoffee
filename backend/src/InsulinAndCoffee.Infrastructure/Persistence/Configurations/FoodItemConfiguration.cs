using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    public void Configure(EntityTypeBuilder<FoodItem> entity)
    {
        entity.HasKey(f => f.Id);
        entity.Ignore(f => f.MealItems);
        entity.HasIndex(f => new { f.UserId, f.Name });
        entity.Property(f => f.Name).HasMaxLength(160).IsRequired();
        entity.Property(f => f.MeasurementType).IsRequired();
        entity.Property(f => f.CarbsPer100g).HasPrecision(7, 2);
        entity.Property(f => f.CarbsPerUnit).HasPrecision(8, 2);
        entity.Property(f => f.ProteinPer100g).HasPrecision(7, 2);
        entity.Property(f => f.FatPer100g).HasPrecision(7, 2);
        entity.Property(f => f.CaloriesPer100g).HasPrecision(8, 2);
    }
}
