using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public class MealConfiguration : IEntityTypeConfiguration<Meal>
{
    public void Configure(EntityTypeBuilder<Meal> entity)
    {
        entity.HasKey(m => m.Id);
        entity.HasIndex(m => new { m.UserId, m.MealTime });
        entity.Property(m => m.PreMealGlucose).HasPrecision(6, 2);
        entity.Property(m => m.TotalCarbs).HasPrecision(8, 2);
        entity.Property(m => m.SuggestedBolus).HasPrecision(6, 2);
        entity.Property(m => m.ConfirmedBolus).HasPrecision(6, 2);
        entity.Property(m => m.Notes).HasMaxLength(1000);
    }
}