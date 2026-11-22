using InsulinAndCoffee.Domain.Entities;

namespace InsulinAndCoffee.Application.Dtos;

public record AiUsageFilter(DateTimeOffset? From = null, DateTimeOffset? To = null, Guid? UserId = null,
    string? Model = null, string? Feature = null, string? Status = null);
public record AiUsageTotals(int Events, int ProviderRequests, int SuccessfulRequests, int FailedRequests, int CancelledRequests,
    long? InputTokens, long? OutputTokens, int MissingUsageRequests, decimal? EstimatedCostUsd, int UnpricedRequests,
    decimal? AverageCostUsd, double? AverageLatencyMs, decimal? ErrorRate, int CacheHits, int CacheMisses, int CacheBypasses,
    decimal? CacheHitRate, int AvoidedProviderRequests);
public record AiUsageBucket(string Key, int Requests, long? InputTokens, long? OutputTokens, decimal? EstimatedCostUsd, int UnpricedRequests);
public record AiMonitoringOverview(AiUsageTotals Totals, IReadOnlyList<AiUsageBucket> Daily,
    IReadOnlyList<AiUsageBucket> Features, IReadOnlyList<AiUsageBucket> Models);
public record CreateAiPricingRequest(string Provider, string Model, decimal InputPricePerMillionTokens,
    decimal? CachedInputPricePerMillionTokens, decimal OutputPricePerMillionTokens, DateTimeOffset EffectiveFromUtc, DateTimeOffset? EffectiveToUtc = null);
