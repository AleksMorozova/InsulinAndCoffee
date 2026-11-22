using InsulinAndCoffee.Domain.Entities;

namespace InsulinAndCoffee.Domain;

public static class AiCostCalculation
{
    public static void Apply(AiUsageRecord record, AiModelPricing? price)
    {
        if (!record.ProviderCalled || price is null) return;
        record.PricingVersion = price.Id;
        record.InputPricePerMillionTokens = price.InputPricePerMillionTokens;
        record.CachedInputPricePerMillionTokens = price.CachedInputPricePerMillionTokens;
        record.OutputPricePerMillionTokens = price.OutputPricePerMillionTokens;
        // Provider input/output totals already include cached/reasoning tokens respectively.
        // Unknown cache usage with differing tariffs cannot produce a defensible estimate.
        if (record.InputTokens is not int input || record.OutputTokens is not int output || input < 0 || output < 0) return;
        if (record.CachedInputTokens is null)
        {
            if (price.CachedInputPricePerMillionTokens != price.InputPricePerMillionTokens) return;
            // Equal tariffs permit a total estimate, but not an invented cache/non-cache split.
            record.OutputCostUsd = output * price.OutputPricePerMillionTokens / 1_000_000m;
            record.EstimatedCostUsd = input * price.InputPricePerMillionTokens / 1_000_000m + record.OutputCostUsd;
            return;
        }
        var cached = record.CachedInputTokens ?? 0;
        if (cached < 0 || cached > input || cached > 0 && price.CachedInputPricePerMillionTokens is null) return;
        record.InputCostUsd = (input - cached) * price.InputPricePerMillionTokens / 1_000_000m;
        record.CachedInputCostUsd = cached * (price.CachedInputPricePerMillionTokens ?? 0m) / 1_000_000m;
        record.OutputCostUsd = output * price.OutputPricePerMillionTokens / 1_000_000m;
        record.EstimatedCostUsd = record.InputCostUsd + record.CachedInputCostUsd + record.OutputCostUsd;
    }
}
