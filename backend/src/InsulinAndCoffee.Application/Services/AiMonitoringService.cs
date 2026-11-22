using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public sealed class AiMonitoringService(IAppDbContext db, TimeProvider clock)
{
    private IQueryable<AiUsageRecord> Filter(AiUsageFilter filter)
    {
        var to = (filter.To ?? clock.GetUtcNow()).ToUniversalTime();
        var from = (filter.From ?? to.AddDays(-30)).ToUniversalTime();
        if (from >= to || to - from > TimeSpan.FromDays(366)) throw new ValidationException("Select a date range of up to 366 days, with From before To.");
        var query = db.AiUsageRecords.AsNoTracking().Where(row => row.StartedAtUtc >= from && row.StartedAtUtc < to);
        if (filter.UserId.HasValue) query = query.Where(row => row.UserId == filter.UserId);
        if (!string.IsNullOrWhiteSpace(filter.Model)) query = query.Where(row => row.Model == filter.Model);
        if (!string.IsNullOrWhiteSpace(filter.Feature)) query = query.Where(row => row.Feature == filter.Feature);
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(row => row.Status == filter.Status);
        return query;
    }

    public async Task<PaginatedResult<AiUsageRecord>> HistoryAsync(AiUsageFilter filter, int page, int pageSize, string sort, bool descending, CancellationToken token)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > 1000000) throw new ValidationException("Invalid page or page size (1–100).");
        var query = Filter(filter);
        var count = await query.CountAsync(token);
        IOrderedQueryable<AiUsageRecord> ordered = sort switch
        {
            "time" => descending ? query.OrderByDescending(row => row.StartedAtUtc) : query.OrderBy(row => row.StartedAtUtc),
            "cost" => descending ? query.OrderByDescending(row => row.EstimatedCostUsd) : query.OrderBy(row => row.EstimatedCostUsd),
            "duration" => descending ? query.OrderByDescending(row => row.DurationMs) : query.OrderBy(row => row.DurationMs),
            _ => throw new ValidationException("Sort must be time, cost or duration.")
        };
        var items = await ordered.ThenBy(row => row.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(token);
        return new(items, page, pageSize, count, (int)Math.Ceiling(count / (double)pageSize));
    }

    public async Task<AiUsageRecord> DetailsAsync(Guid id, CancellationToken token) =>
        await db.AiUsageRecords.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, token) ?? throw new NotFoundException("AI request", id);

    public async Task<AiMonitoringOverview> OverviewAsync(AiUsageFilter filter, CancellationToken token)
    {
        var query = Filter(filter);
        var all = await query.GroupBy(row => 1).Select(group => new
        {
            Events = group.Count(), Requests = group.Count(row => row.ProviderCalled),
            Success = group.Count(row => row.ProviderCalled && row.Status == "Success"),
            Failed = group.Count(row => row.ProviderCalled && row.Status == "Failure"),
            Cancelled = group.Count(row => row.ProviderCalled && row.Status == "Cancelled"),
            Input = group.Sum(row => (long?)row.InputTokens), Output = group.Sum(row => (long?)row.OutputTokens),
            InputKnown = group.Count(row => row.InputTokens != null), OutputKnown = group.Count(row => row.OutputTokens != null),
            MissingUsage = group.Count(row => row.ProviderCalled && (row.InputTokens == null || row.OutputTokens == null)),
            Cost = group.Sum(row => row.EstimatedCostUsd), Priced = group.Count(row => row.ProviderCalled && row.EstimatedCostUsd != null),
            Latency = group.Where(row => row.ProviderCalled).Average(row => (double?)row.DurationMs),
            Hits = group.Count(row => row.CacheStatus == "Hit"), Misses = group.Count(row => row.CacheStatus == "Miss"),
            Bypasses = group.Count(row => row.CacheStatus == "Bypass")
        }).SingleOrDefaultAsync(token);
        var totals = all is null ? new AiUsageTotals(0, 0, 0, 0, 0, null, null, 0, null, 0, null, null, null, 0, 0, 0, null, 0)
            : new AiUsageTotals(all.Events, all.Requests, all.Success, all.Failed, all.Cancelled,
                all.InputKnown == 0 ? null : all.Input, all.OutputKnown == 0 ? null : all.Output, all.MissingUsage,
                all.Priced == 0 ? null : all.Cost, all.Requests - all.Priced, all.Priced == 0 ? null : all.Cost / all.Priced,
                all.Latency, all.Requests == 0 ? null : (decimal)all.Failed / all.Requests,
                all.Hits, all.Misses, all.Bypasses, all.Hits + all.Misses == 0 ? null : (decimal)all.Hits / (all.Hits + all.Misses), all.Hits);
        var daily = await query.GroupBy(row => new { row.StartedAtUtc.Year, row.StartedAtUtc.Month, row.StartedAtUtc.Day })
            .OrderBy(group => group.Key.Year).ThenBy(group => group.Key.Month).ThenBy(group => group.Key.Day).Select(group => new
        {
            Day = group.Key, Requests = group.Count(row => row.ProviderCalled), Input = group.Sum(row => (long?)row.InputTokens),
            Output = group.Sum(row => (long?)row.OutputTokens), Cost = group.Sum(row => row.EstimatedCostUsd),
            Priced = group.Count(row => row.EstimatedCostUsd != null), Unpriced = group.Count(row => row.ProviderCalled && row.EstimatedCostUsd == null),
            InputKnown = group.Count(row => row.InputTokens != null), OutputKnown = group.Count(row => row.OutputTokens != null)
        }).ToListAsync(token);
        var features = await Buckets(query, false, token);
        var models = await Buckets(query, true, token);
        return new(totals, daily.Select(day => new AiUsageBucket($"{day.Day.Year:D4}-{day.Day.Month:D2}-{day.Day.Day:D2}", day.Requests,
            day.InputKnown == 0 ? null : day.Input, day.OutputKnown == 0 ? null : day.Output, day.Priced == 0 ? null : day.Cost, day.Unpriced)).ToArray(), features, models);
    }

    private static async Task<IReadOnlyList<AiUsageBucket>> Buckets(IQueryable<AiUsageRecord> query, bool model, CancellationToken token) =>
        await query.GroupBy(row => model ? row.Model : row.Feature).OrderBy(group => group.Key).Select(group => new AiUsageBucket(group.Key,
            group.Count(row => row.ProviderCalled), group.Any(row => row.InputTokens != null) ? group.Sum(row => (long?)row.InputTokens) : null,
            group.Any(row => row.OutputTokens != null) ? group.Sum(row => (long?)row.OutputTokens) : null,
            group.Any(row => row.EstimatedCostUsd != null) ? group.Sum(row => row.EstimatedCostUsd) : null,
            group.Count(row => row.ProviderCalled && row.EstimatedCostUsd == null))).ToListAsync(token);

    public async Task<IReadOnlyList<AiModelPricing>> PricesAsync(CancellationToken token) =>
        await db.AiModelPrices.AsNoTracking().OrderBy(row => row.Provider).ThenBy(row => row.Model).ThenByDescending(row => row.EffectiveFromUtc).ToListAsync(token);

    public async Task<AiModelPricing> AddPriceAsync(CreateAiPricingRequest request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Provider) || request.Provider.Length > 100 || string.IsNullOrWhiteSpace(request.Model) || request.Model.Length > 100
            || request.InputPricePerMillionTokens is < 0 or > 1000000 || request.OutputPricePerMillionTokens is < 0 or > 1000000
            || request.CachedInputPricePerMillionTokens is < 0 or > 1000000
            || request.EffectiveFromUtc < clock.GetUtcNow() || request.EffectiveToUtc <= request.EffectiveFromUtc)
            throw new ValidationException("Enter provider, exact model, non-negative prices, and a future effective date. Historical pricing cannot be edited.");
        var record = new AiModelPricing { Id = Guid.NewGuid(), Provider = request.Provider.Trim(), Model = request.Model.Trim(),
            InputPricePerMillionTokens = request.InputPricePerMillionTokens, CachedInputPricePerMillionTokens = request.CachedInputPricePerMillionTokens,
            OutputPricePerMillionTokens = request.OutputPricePerMillionTokens, EffectiveFromUtc = request.EffectiveFromUtc.ToUniversalTime(), EffectiveToUtc = request.EffectiveToUtc?.ToUniversalTime() };
        db.AiModelPrices.Add(record);
        await db.SaveChangesAsync(token);
        return record;
    }
}
