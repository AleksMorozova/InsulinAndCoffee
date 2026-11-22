using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsulinAndCoffee.Infrastructure.Persistence.Configurations;

public class AiUsageConfiguration : IEntityTypeConfiguration<AiUsageRecord>
{
    public void Configure(EntityTypeBuilder<AiUsageRecord> entity)
    {
        entity.HasIndex(row => row.StartedAtUtc);
        entity.HasIndex(row => new { row.UserId, row.StartedAtUtc });
        entity.HasIndex(row => new { row.Model, row.Feature, row.StartedAtUtc });
        entity.HasIndex(row => row.OperationId);
        foreach (var name in new[] { nameof(AiUsageRecord.Feature), nameof(AiUsageRecord.Provider), nameof(AiUsageRecord.Model),
            nameof(AiUsageRecord.ProviderRequestId), nameof(AiUsageRecord.CorrelationId), nameof(AiUsageRecord.Status), nameof(AiUsageRecord.CacheStatus), nameof(AiUsageRecord.ErrorCode) })
            entity.Property<string>(name).HasMaxLength(200);
        foreach (var name in new[] { nameof(AiUsageRecord.InputCostUsd), nameof(AiUsageRecord.CachedInputCostUsd), nameof(AiUsageRecord.OutputCostUsd),
            nameof(AiUsageRecord.EstimatedCostUsd), nameof(AiUsageRecord.InputPricePerMillionTokens), nameof(AiUsageRecord.CachedInputPricePerMillionTokens), nameof(AiUsageRecord.OutputPricePerMillionTokens) })
            entity.Property<decimal?>(name).HasPrecision(28, 14);
        // No FK to conversations: deleting private chat content must not erase accounting metadata.
    }
}

public class AiModelPricingConfiguration : IEntityTypeConfiguration<AiModelPricing>
{
    public void Configure(EntityTypeBuilder<AiModelPricing> entity)
    {
        entity.Property(row => row.Provider).HasMaxLength(100);
        entity.Property(row => row.Model).HasMaxLength(100);
        entity.HasIndex(row => new { row.Provider, row.Model, row.EffectiveFromUtc }).IsUnique();
        entity.Property(row => row.InputPricePerMillionTokens).HasPrecision(28, 14);
        entity.Property(row => row.CachedInputPricePerMillionTokens).HasPrecision(28, 14);
        entity.Property(row => row.OutputPricePerMillionTokens).HasPrecision(28, 14);
    }
}
