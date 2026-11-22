export interface AiUsageRecord {
  id: string; userId: string | null; feature: string; provider: string; model: string; operationId: string;
  conversationId: string | null; providerRequestId: string | null; correlationId: string | null;
  startedAtUtc: string; completedAtUtc: string; createdAtUtc: string; durationMs: number; status: string; providerCalled: boolean;
  inputTokens: number | null; outputTokens: number | null; cachedInputTokens: number | null; reasoningTokens: number | null; totalTokens: number | null;
  estimatedCostUsd: number | null; inputCostUsd: number | null; cachedInputCostUsd: number | null; outputCostUsd: number | null;
  pricingVersion: string | null; inputPricePerMillionTokens: number | null; cachedInputPricePerMillionTokens: number | null;
  outputPricePerMillionTokens: number | null; cacheStatus: string; errorCode: string | null;
}
export interface AiUsageBucket { key: string; requests: number; inputTokens: number | null; outputTokens: number | null; estimatedCostUsd: number | null; unpricedRequests: number; }
export interface AiMonitoringOverview {
  totals: { events: number; providerRequests: number; successfulRequests: number; failedRequests: number; cancelledRequests: number;
    inputTokens: number | null; outputTokens: number | null; missingUsageRequests: number; estimatedCostUsd: number | null; unpricedRequests: number;
    averageCostUsd: number | null; averageLatencyMs: number | null; errorRate: number | null; cacheHits: number; cacheMisses: number;
    cacheBypasses: number; cacheHitRate: number | null; avoidedProviderRequests: number; };
  daily: AiUsageBucket[]; features: AiUsageBucket[]; models: AiUsageBucket[];
}
export interface AiUsageFilters { from: string; to: string; userId?: string; model?: string; feature?: string; status?: string; }
export interface CreateAiPricing { provider: string; model: string; inputPricePerMillionTokens: number; cachedInputPricePerMillionTokens: number | null;
  outputPricePerMillionTokens: number; effectiveFromUtc: string; effectiveToUtc: string | null; }
export interface AiModelPricing extends CreateAiPricing { id: string; }
