namespace InsulinAndCoffee.Domain.Entities;

// Operational metadata only. Never add prompts, responses, food names or credentials here.
public class AiUsageRecord
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Feature { get; set; } = "MealAssistant";
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = "";
    public Guid OperationId { get; set; }
    public Guid? ConversationId { get; set; }
    public string? ProviderRequestId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public long DurationMs { get; set; }
    public string Status { get; set; } = "Success";
    public bool ProviderCalled { get; set; } = true;
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? CachedInputTokens { get; set; }
    public int? ReasoningTokens { get; set; }
    public int? TotalTokens { get; set; }
    public decimal? InputCostUsd { get; set; }
    public decimal? CachedInputCostUsd { get; set; }
    public decimal? OutputCostUsd { get; set; }
    public decimal? EstimatedCostUsd { get; set; }
    public Guid? PricingVersion { get; set; }
    public decimal? InputPricePerMillionTokens { get; set; }
    public decimal? CachedInputPricePerMillionTokens { get; set; }
    public decimal? OutputPricePerMillionTokens { get; set; }
    public string CacheStatus { get; set; } = "Bypass";
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public class AiModelPricing
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public decimal InputPricePerMillionTokens { get; set; }
    public decimal? CachedInputPricePerMillionTokens { get; set; }
    public decimal OutputPricePerMillionTokens { get; set; }
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
}
