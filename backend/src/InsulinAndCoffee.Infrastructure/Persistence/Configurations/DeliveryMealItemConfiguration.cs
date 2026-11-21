using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public class DeliveryMealItemConfiguration : IEntityTypeConfiguration<DeliveryMealItem>
{
    public void Configure(EntityTypeBuilder<DeliveryMealItem> entity)
    {
        entity.HasKey(item => item.Id);
        entity.HasOne(item => item.DeliveryMeal).WithMany(order => order.Items)
            .HasForeignKey(item => item.DeliveryMealId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(item => item.FoodItem).WithMany().HasForeignKey(item => item.FoodItemId).OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(item => new { item.DeliveryMealId, item.DisplayOrder });
        entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
        entity.Property(item => item.Quantity).HasPrecision(8, 2);
        entity.Property(item => item.CarbsPer100gSnapshot).HasPrecision(7, 2);
        entity.Property(item => item.CarbsPerUnitSnapshot).HasPrecision(8, 2);
        entity.Property(item => item.CalculatedCarbs).HasPrecision(8, 2);
        entity.Property(item => item.CarbOverride).HasPrecision(8, 2);
    }
}
