using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public class MealItemConfiguration : IEntityTypeConfiguration<MealItem>
{
    public void Configure(EntityTypeBuilder<MealItem> entity)
    {
        entity.HasKey(i => i.Id);
        entity.Ignore(i => i.FoodItem);
        entity.Property(i => i.FoodNameSnapshot).HasMaxLength(160).IsRequired();
        entity.Property(i => i.Quantity).HasPrecision(8, 2);
        entity.Property(i => i.MeasurementType).IsRequired();
        entity.Property(i => i.WeightGrams).HasPrecision(8, 2);
        entity.Property(i => i.CarbsPer100gSnapshot).HasPrecision(7, 2);
        entity.Property(i => i.CarbsPerUnitSnapshot).HasPrecision(8, 2);
        entity.Property(i => i.CalculatedCarbs).HasPrecision(8, 2);
        entity.HasOne(i => i.Meal).WithMany(m => m.Items).HasForeignKey(i => i.MealId).OnDelete(DeleteBehavior.Cascade);
    }
}
