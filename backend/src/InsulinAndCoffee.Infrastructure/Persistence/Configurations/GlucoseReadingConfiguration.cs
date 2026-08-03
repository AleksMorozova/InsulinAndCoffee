using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public class GlucoseReadingConfiguration : IEntityTypeConfiguration<GlucoseReading>
{
    public void Configure(EntityTypeBuilder<GlucoseReading> entity)
    {
        entity.HasKey(r => r.Id);
        entity.HasIndex(r => new { r.UserId, r.ReadingTime });
        entity.Property(r => r.Value).HasPrecision(6, 2);
        entity.Property(r => r.Notes).HasMaxLength(1000);
        entity.HasOne(r => r.Meal).WithMany(m => m.GlucoseReadings).HasForeignKey(r => r.MealId).OnDelete(DeleteBehavior.SetNull);
    }
}