using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain;
using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InsulinAndCoffee.Application.Tests;

public partial class AssistantPostgreSqlSafetyTests
{
    [AssistantPostgresFact]
    public async Task AiMonitoring_RealRequestPipeline_TracksRetryButNotDurableReplayAndExposesOnlyAdminMetadata()
    {
        await using var database = await TestDatabase.CreateAsync();
        var calls = 0;
        await using var baseFactory = new StartupFactory(database.ConnectionString);
        await using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["MealAssistant:ApiKey"] = "synthetic-monitor-key", ["MealAssistant:Model"] = "synthetic-monitor-model" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILlmClient>();
                services.AddHttpClient<ILlmClient, OpenAiLlmClient>().ConfigurePrimaryHttpMessageHandler(() =>
                    new MonitoringProviderHandler(() => Interlocked.Increment(ref calls)));
            });
        });
        using var client = factory.CreateClient();
        var username = "monitor-" + Guid.NewGuid().ToString("N")[..14];
        var owner = await Post<AuthUserDto>(client, "/api/auth/register", new { username, password = "monitor-test-password" });
        var login = await Post<AuthTokenDto>(client, "/api/auth/login", new { username, password = "monitor-test-password" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        await using (var db = database.Open())
        {
            db.AiModelPrices.Add(new() { Id = Guid.NewGuid(), Provider = "OpenAI", Model = "synthetic-monitor-model", EffectiveFromUtc = TimeProvider.System.GetUtcNow().AddMinutes(-1),
                InputPricePerMillionTokens = 2m, CachedInputPricePerMillionTokens = .5m, OutputPricePerMillionTokens = 8m });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        var request = new MealAssistantRequest(null, "Please interpret my private test request", Guid.NewGuid());
        using var failed = await client.PostAsJsonAsync("/api/meal-assistant/messages", request, AcceptanceJson);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        var success = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", request);
        var replay = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", request);
        Assert.True(replay.Replayed); Assert.Equal(success.ConversationId, replay.ConversationId); Assert.Equal(2, calls);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/ai-monitoring/requests")).StatusCode);
        await using (var db = database.Open())
        {
            var rows = await db.AiUsageRecords.AsNoTracking().OrderBy(row => row.StartedAtUtc).ToListAsync(CancellationToken.None);
            Assert.Equal(2, rows.Count); Assert.All(rows, row => { Assert.Equal(owner.Id, row.UserId); Assert.Equal(request.RequestId, row.OperationId); Assert.NotNull(row.CorrelationId); });
            Assert.Equal("provider_rate_limit", rows[0].ErrorCode); Assert.Null(rows[0].EstimatedCostUsd);
            Assert.Equal(.0025m, rows[1].EstimatedCostUsd); Assert.Equal("synthetic-request-id", rows[1].ProviderRequestId);
        }
        var admin = await Post<AuthTokenDto>(client, "/api/auth/login", new { username = "admin", password = "admin" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        var overview = await Get<AiMonitoringOverview>(client, "/api/admin/ai-monitoring/overview?model=synthetic-monitor-model");
        Assert.Equal(2, overview.Totals.ProviderRequests); Assert.Equal(.0025m, overview.Totals.EstimatedCostUsd); Assert.Equal(1, overview.Totals.UnpricedRequests);
        var metadata = await client.GetStringAsync("/api/admin/ai-monitoring/requests?model=synthetic-monitor-model");
        Assert.DoesNotContain(request.Message, metadata); Assert.DoesNotContain("synthetic-monitor-key", metadata);
    }

    private sealed class MonitoringProviderHandler(Func<int> next) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(next() == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK)
            { Content = new StringContent("""{"model":"synthetic-monitor-model","usage":{"prompt_tokens":1000,"completion_tokens":100,"total_tokens":1100,"prompt_tokens_details":{"cached_tokens":200},"completion_tokens_details":{"reasoning_tokens":80}},"choices":[{"message":{"tool_calls":[{"function":{"name":"search_meal_history","arguments":"{\"foods\":[],\"intent\":\"lookup\"}"}}]}}]}""") };
            response.Headers.Add("x-request-id", "synthetic-request-id");
            return Task.FromResult(response);
        }
    }

    [AssistantPostgresFact]
    public async Task AiMonitoring_MigrationAggregationPrecisionAndPricingUniqueness_WorkOnPostgreSql()
    {
        await using var database = await TestDatabase.CreateAsync();
        var today = TimeProvider.System.GetUtcNow();
        var now = new DateTimeOffset(today.Year, today.Month, today.Day, 0, 30, 0, TimeSpan.Zero);
        var owner = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        await using (var db = database.Open())
        {
            db.Users.Add(User(owner));
            var price = new AiModelPricing { Id = Guid.NewGuid(), Provider = "OpenAI", Model = "test-model", InputPricePerMillionTokens = .01m,
                CachedInputPricePerMillionTokens = .01m, OutputPricePerMillionTokens = .02m, EffectiveFromUtc = now.AddDays(-1) };
            var record = new AiUsageRecord { Id = recordId, UserId = owner, Model = "test-model", StartedAtUtc = now, CompletedAtUtc = now,
                InputTokens = 1, CachedInputTokens = 0, OutputTokens = 1, TotalTokens = 2, DurationMs = 123, CreatedAtUtc = now, OperationId = Guid.NewGuid() };
            AiCostCalculation.Apply(record, price);
            db.AiModelPrices.Add(price); db.AiUsageRecords.Add(record);
            db.AiUsageRecords.Add(new() { Id = Guid.NewGuid(), UserId = owner, Model = "unknown", StartedAtUtc = now, Status = "Failure", DurationMs = 321 });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        await using (var db = database.Open())
        {
            Assert.False(db.Database.HasPendingModelChanges());
            await db.Database.OpenConnectionAsync(CancellationToken.None);
            await db.Database.ExecuteSqlRawAsync("SET TIME ZONE 'Pacific/Honolulu'", CancellationToken.None);
            var service = new AiMonitoringService(db, TimeProvider.System);
            var filter = new AiUsageFilter(now.AddMinutes(-1), now.AddMinutes(1), owner);
            var overview = await service.OverviewAsync(filter, CancellationToken.None);
            Assert.Equal(2, overview.Totals.ProviderRequests);
            Assert.Equal(.00000003m, overview.Totals.EstimatedCostUsd);
            Assert.Equal(.5m, overview.Totals.ErrorRate);
            Assert.Equal(222d, overview.Totals.AverageLatencyMs);
            Assert.Equal(1, overview.Totals.UnpricedRequests);
            Assert.Null(overview.Totals.CacheHitRate);
            Assert.Equal(now.ToString("yyyy-MM-dd"), Assert.Single(overview.Daily).Key); Assert.Single(overview.Features); Assert.Equal(2, overview.Models.Count);
            var page = await service.HistoryAsync(filter, 1, 1, "cost", false, CancellationToken.None);
            Assert.Equal(2, page.TotalPages); Assert.Single(page.Items);
            var details = await service.DetailsAsync(recordId, CancellationToken.None);
            Assert.Equal(.00000003m, details.EstimatedCostUsd); Assert.Equal(.01m, details.InputPricePerMillionTokens);
            var price = await db.AiModelPrices.AsNoTracking().SingleAsync(CancellationToken.None);
            db.AiModelPrices.Add(new() { Id = Guid.NewGuid(), Provider = price.Provider, Model = price.Model, EffectiveFromUtc = price.EffectiveFromUtc });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(CancellationToken.None));
        }
    }
}
