using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsulinAndCoffee.Application.Tests;

public class AiMonitoringTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Token = CancellationToken.None;
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static AiModelPricing Price() => new() { Id = Guid.NewGuid(), Provider = "OpenAI", Model = "test-model", InputPricePerMillionTokens = 2m,
        CachedInputPricePerMillionTokens = .5m, OutputPricePerMillionTokens = 8m, EffectiveFromUtc = Now.AddDays(-1) };
    private static AiUsageRecord Record() => new() { Id = Guid.NewGuid(), Model = "test-model", OperationId = Guid.NewGuid(), StartedAtUtc = Now,
        InputTokens = 1000, CachedInputTokens = 200, OutputTokens = 100, ReasoningTokens = 80, TotalTokens = 1100, DurationMs = 200 };

    [Fact]
    public void Apply_CachedAndReasoningTokens_NoDoubleCountingAndNoDisplayRounding()
    {
        var record = Record(); var price = Price();
        AiCostCalculation.Apply(record, price);
        Assert.Equal(.0016m, record.InputCostUsd); Assert.Equal(.0001m, record.CachedInputCostUsd);
        Assert.Equal(.0008m, record.OutputCostUsd); Assert.Equal(.0025m, record.EstimatedCostUsd);
        Assert.Equal(price.Id, record.PricingVersion);
        var tiny = Record(); tiny.InputTokens = 1; tiny.CachedInputTokens = 0; tiny.OutputTokens = 0;
        AiCostCalculation.Apply(tiny, price); Assert.Equal(.000002m, tiny.EstimatedCostUsd);
        price.InputPricePerMillionTokens = 999m;
        Assert.Equal(2m, record.InputPricePerMillionTokens); Assert.Equal(.0025m, record.EstimatedCostUsd);
    }

    [Fact]
    public void Apply_MissingUsageUnknownPricingAndApplicationHit_DoNotInventCosts()
    {
        var missing = Record(); missing.InputTokens = null;
        var unknownCached = Record(); unknownCached.CachedInputTokens = null;
        var badCached = Record(); badCached.CachedInputTokens = 1001;
        var hit = Record(); hit.ProviderCalled = false; hit.CacheStatus = "Hit";
        foreach (var record in new[] { missing, unknownCached, badCached, hit }) { AiCostCalculation.Apply(record, Price()); Assert.Null(record.EstimatedCostUsd); }
        var unknownPrice = Record(); AiCostCalculation.Apply(unknownPrice, null); Assert.Null(unknownPrice.EstimatedCostUsd);
        var zero = Record(); zero.InputTokens = 0; zero.OutputTokens = 0; zero.CachedInputTokens = 0;
        AiCostCalculation.Apply(zero, Price()); Assert.Equal(0m, zero.EstimatedCostUsd);
        var equalTariff = Price(); equalTariff.CachedInputPricePerMillionTokens = equalTariff.InputPricePerMillionTokens;
        AiCostCalculation.Apply(unknownCached, equalTariff);
        Assert.Equal(.0028m, unknownCached.EstimatedCostUsd); Assert.Null(unknownCached.InputCostUsd); Assert.Null(unknownCached.CachedInputCostUsd);
    }

    [Fact]
    public async Task Overview_FilteringPaginationAndUnknownTotals_AreAccurate()
    {
        await using var db = Db(); var owner = Guid.NewGuid(); var other = Guid.NewGuid();
        var first = Record(); first.UserId = owner; first.EstimatedCostUsd = .0025m;
        var error = Record(); error.UserId = owner; error.Status = "Failure"; error.InputTokens = null; error.OutputTokens = null; error.StartedAtUtc = Now.AddMinutes(1);
        var hit = Record(); hit.UserId = owner; hit.ProviderCalled = false; hit.CacheStatus = "Hit"; hit.InputTokens = null; hit.OutputTokens = null;
        var outside = Record(); outside.UserId = owner; outside.StartedAtUtc = Now.AddDays(-40);
        var foreign = Record(); foreign.UserId = other;
        db.AiUsageRecords.AddRange(first, error, hit, outside, foreign); await db.SaveChangesAsync(Token);
        var service = new AiMonitoringService(db, new Clock()); var filter = new AiUsageFilter(Now.AddHours(-1), Now.AddHours(1), owner);
        var summary = await service.OverviewAsync(filter, Token);
        Assert.Equal(2, summary.Totals.ProviderRequests); Assert.Equal(.0025m, summary.Totals.EstimatedCostUsd);
        Assert.Equal(1, summary.Totals.UnpricedRequests); Assert.Equal(.5m, summary.Totals.ErrorRate);
        Assert.Equal(1, summary.Totals.CacheHits); Assert.Equal(1m, summary.Totals.CacheHitRate);
        Assert.Equal(1000, summary.Totals.InputTokens); Assert.Equal(.0025m, Assert.Single(summary.Daily).EstimatedCostUsd);
        var page = await service.HistoryAsync(filter, 1, 1, "time", true, Token);
        Assert.Equal(3, page.TotalCount); Assert.Equal(error.Id, Assert.Single(page.Items).Id);
        Assert.Equal(3, page.TotalPages);
        var failure = await service.OverviewAsync(filter with { Status = "Failure" }, Token);
        Assert.Null(failure.Totals.EstimatedCostUsd); Assert.Null(failure.Totals.InputTokens);
        await Assert.ThrowsAsync<ValidationException>(() => service.HistoryAsync(filter, 1, 101, "time", true, Token));
        await Assert.ThrowsAsync<ValidationException>(() => service.OverviewAsync(filter with { To = filter.From }, Token));
    }

    [Fact]
    public async Task Write_PricingVersionMatchesRequestTime_AndHistoricalCostRemainsStable()
    {
        var dbName = Guid.NewGuid().ToString(); var services = new ServiceCollection();
        services.AddScoped<IAppDbContext>(_ => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options));
        using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>(); db.AiModelPrices.Add(Price()); await db.SaveChangesAsync(Token); }
        var writer = new AiUsageWriter(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AiUsageWriter>.Instance);
        var record = Record(); await writer.WriteAsync(record);
        await using var verify = provider.CreateAsyncScope(); var context = verify.ServiceProvider.GetRequiredService<IAppDbContext>();
        var saved = await context.AiUsageRecords.AsNoTracking().SingleAsync(Token);
        Assert.Equal(.0025m, saved.EstimatedCostUsd); Assert.NotNull(saved.PricingVersion);
        var future = Price(); future.InputPricePerMillionTokens = 500m; future.EffectiveFromUtc = Now.AddHours(1);
        context.AiModelPrices.Add(future); await context.SaveChangesAsync(Token);
        var second = Record(); second.Id = Guid.NewGuid(); await writer.WriteAsync(second);
        Assert.Equal(.0025m, (await context.AiUsageRecords.AsNoTracking().SingleAsync(row => row.Id == saved.Id, Token)).EstimatedCostUsd);
        Assert.Equal(saved.PricingVersion, second.PricingVersion);
        var unknown = Record(); unknown.Model = "unknown"; await writer.WriteAsync(unknown); Assert.Null(unknown.EstimatedCostUsd);
    }

    [Theory]
    [InlineData(200, "Success", null)]
    [InlineData(429, "Failure", "provider_rate_limit")]
    [InlineData(401, "Failure", "provider_auth")]
    public async Task Interpret_ProviderCalls_RecordMetadataWithoutSensitiveContent(int code, string status, string? error)
    {
        var writer = new CaptureWriter(); var operation = Guid.NewGuid();
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)code) { Content = new StringContent(ResponseBody) })));
        var client = Client(http, writer, new() { UserId = Guid.NewGuid(), OperationId = operation, ConversationId = Guid.NewGuid(), CorrelationId = "test-correlation" });
        if (code == 200) await client.InterpretAsync([new("user", "sensitive test conversation")], Token);
        else await Assert.ThrowsAsync<MealAssistantUnavailableException>(() => client.InterpretAsync([], Token));
        var record = Assert.Single(writer.Records); Assert.Equal(status, record.Status); Assert.Equal(error, record.ErrorCode);
        Assert.Equal(operation, record.OperationId); Assert.Equal("Bypass", record.CacheStatus);
        if (code == 200) { Assert.Equal(1000, record.InputTokens); Assert.Equal(80, record.ReasoningTokens); Assert.Equal(200, record.CachedInputTokens); }
        else Assert.Null(record.InputTokens);
        var json = JsonSerializer.Serialize(record);
        Assert.DoesNotContain("sensitive test conversation", json); Assert.DoesNotContain("synthetic-secret", json);
    }

    [Fact]
    public async Task Interpret_TimeoutCancellationRetriesAndMultipleCalls_TrackEachActualAttemptOnce()
    {
        var writer = new CaptureWriter(); var tracking = new AiRequestContext { OperationId = Guid.NewGuid() };
        using var timeoutHttp = new HttpClient(new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return new(HttpStatusCode.OK); }));
        await Assert.ThrowsAsync<MealAssistantUnavailableException>(() => Client(timeoutHttp, writer, tracking).InterpretAsync([], Token));
        Assert.Equal("provider_timeout", Assert.Single(writer.Records).ErrorCode);
        using var cancel = new CancellationTokenSource();
        using var cancelledHttp = new HttpClient(new Handler((_, _) => { cancel.Cancel(); throw new OperationCanceledException(cancel.Token); }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(cancelledHttp, writer, tracking).InterpretAsync([], cancel.Token));
        Assert.Equal("Cancelled", writer.Records.Last().Status);
        using var goodHttp = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseBody) })));
        var good = Client(goodHttp, writer, tracking); await good.InterpretAsync([], Token); await good.InterpretAsync([], Token);
        Assert.Equal(4, writer.Records.Count); Assert.Equal(4, writer.Records.Select(row => row.Id).Distinct().Count());
        Assert.All(writer.Records, row => Assert.Equal(tracking.OperationId, row.OperationId));
    }

    [Fact]
    public async Task Interpret_UnknownUsageAndFailedTracking_DoNotBreakSuccessfulResponse()
    {
        var writer = new CaptureWriter();
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseBody.Replace("\"usage\":", "\"unused\":")) })));
        await Client(http, writer, new()).InterpretAsync([], Token); Assert.Null(Assert.Single(writer.Records).InputTokens);
        writer.Fail = true; await Client(http, writer, new()).InterpretAsync([], Token);
        var services = new ServiceCollection(); using var provider = services.BuildServiceProvider();
        // Missing persistence registration simulates a genuine recording failure; the writer contains it.
        await new AiUsageWriter(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AiUsageWriter>.Instance).WriteAsync(Record());
    }

    [Fact]
    public async Task AddPrice_OnlyAppendsValidatedVersions_DoesNotRewriteRecordedCosts()
    {
        await using var db = Db(); var service = new AiMonitoringService(db, new Clock());
        var request = new CreateAiPricingRequest("OpenAI", "test-model", 2m, .5m, 8m, Now.AddMinutes(1));
        var first = await service.AddPriceAsync(request, Token);
        var second = await service.AddPriceAsync(request with { EffectiveFromUtc = Now.AddMinutes(2), InputPricePerMillionTokens = 3m }, Token);
        Assert.NotEqual(first.Id, second.Id); Assert.Equal(2, (await service.PricesAsync(Token)).Count);
        Assert.Equal(2m, (await db.AiModelPrices.SingleAsync(row => row.Id == first.Id, Token)).InputPricePerMillionTokens);
        await Assert.ThrowsAsync<ValidationException>(() => service.AddPriceAsync(request with { EffectiveFromUtc = Now.AddMinutes(-1) }, Token));
        await Assert.ThrowsAsync<ValidationException>(() => service.AddPriceAsync(request with { InputPricePerMillionTokens = -1m }, Token));
    }

    private const string ResponseBody = """{"usage":{"prompt_tokens":1000,"completion_tokens":100,"total_tokens":1100,"prompt_tokens_details":{"cached_tokens":200},"completion_tokens_details":{"reasoning_tokens":80}},"choices":[{"message":{"tool_calls":[{"function":{"name":"search_meal_history","arguments":"{\"foods\":[]}"}}]}}]}""";
    private static OpenAiLlmClient Client(HttpClient http, IAiUsageWriter writer, AiRequestContext context) => new(http,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MealAssistant:ApiKey"] = "synthetic-secret", ["MealAssistant:Model"] = "test-model", ["MealAssistant:TimeoutSeconds"] = "1" }).Build(),
        NullLogger<OpenAiLlmClient>.Instance, writer, context, new Clock());
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class CaptureWriter : IAiUsageWriter
    {
        public List<AiUsageRecord> Records { get; } = []; public bool Fail { get; set; }
        public Task WriteAsync(AiUsageRecord record) { if (Fail) throw new InvalidOperationException("synthetic tracking failure"); Records.Add(record); return Task.CompletedTask; }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
}
