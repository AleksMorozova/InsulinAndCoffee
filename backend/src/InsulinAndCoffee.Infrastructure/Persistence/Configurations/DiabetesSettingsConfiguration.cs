using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public class DiabetesSettingsConfiguration : IEntityTypeConfiguration<DiabetesSettings>
{
    public void Configure(EntityTypeBuilder<DiabetesSettings> entity)
    {
        entity.HasKey(s => s.Id);
        entity.HasIndex(s => s.UserId).IsUnique();
        entity.Property(s => s.TargetGlucose).HasPrecision(6, 2);
        entity.Property(s => s.CarbRatio).HasPrecision(6, 2);
        entity.Property(s => s.CorrectionFactor).HasPrecision(6, 2);
        entity.Property(s => s.InsulinDurationHours).HasPrecision(4, 1);
    }
}