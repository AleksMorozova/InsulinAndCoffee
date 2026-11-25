using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InsulinAndCoffee.Application.Tests;

public partial class AssistantPostgreSqlSafetyTests
{
    [AssistantPostgresFact]
    public async Task CalculatorExport_RejectsAnotherClientsChangeBeforeAndDuringPreparation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        await using var other = database.Open();
        var interceptor = new DuringExport(async () => {
            await Service(other, seed.Owner).SendAsync(new(seed.ConversationId, "add Bread", Guid.NewGuid(), ExpectedDraftVersion: 1), CancellationToken.None);
        });
        await using var exporting = database.Open(interceptor);
        var error = await Assert.ThrowsAsync<AssistantConflictException>(() =>
            Service(exporting, seed.Owner).PrepareDraftAsync(seed.ConversationId, 1, CancellationToken.None));
        Assert.True(interceptor.Triggered); Assert.Equal(2, error.CurrentDraftVersion);
        Assert.Equal("draft_version_conflict", error.Code);
        await using var refreshed = database.Open();
        // A new exporter still holding version 1 must reject the already committed change.
        await Assert.ThrowsAsync<AssistantConflictException>(() => Service(refreshed, seed.Owner)
            .PrepareDraftAsync(seed.ConversationId, 1, CancellationToken.None));
        var current = await Service(refreshed, seed.Owner).GetDraftStateAsync(seed.ConversationId, CancellationToken.None);
        var exported = await Service(refreshed, seed.Owner).PrepareDraftAsync(seed.ConversationId, current.DraftVersion, CancellationToken.None);
        Assert.Equal(current.Draft!.Items.Select(item => item.EffectiveCarbs), exported.Select(item => item.EffectiveCarbs));
        Assert.Equal(2, exported.Count); Assert.Empty(await refreshed.Meals.ToListAsync());
    }

    [AssistantPostgresFact]
    public async Task CalculatorExport_HttpContractRequiresVersionAndReturnsRefreshInformation()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var factory = new StartupFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var credentials = new { username = "export-" + Guid.NewGuid().ToString("N")[..12], password = "export-test-password" };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", credentials)).StatusCode);
        using var auth = JsonDocument.Parse(await (await client.PostAsJsonAsync("/api/auth/login", credentials)).Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.RootElement.GetProperty("accessToken").GetString());
        foreach (var food in new[] {
            new { name = "Mac and cheese", measurementType = "Portion", carbsPer100g = (decimal?)null, carbsPerUnit = (decimal?)25m, proteinPer100g = 0m, fatPer100g = 0m, caloriesPer100g = 0m, isFavorite = false },
            new { name = "Rice", measurementType = "Grams", carbsPer100g = (decimal?)20m, carbsPerUnit = (decimal?)null, proteinPer100g = 0m, fatPer100g = 0m, caloriesPer100g = 0m, isFavorite = false } })
            Assert.True((await client.PostAsJsonAsync("/api/foods", food)).IsSuccessStatusCode);
        var send = await client.PostAsJsonAsync("/api/meal-assistant/messages", new MealAssistantRequest(null,
            "I want 1 serving Mac and cheese and 120 g Rice", Guid.NewGuid()));
        Assert.True(send.IsSuccessStatusCode, await send.Content.ReadAsStringAsync());
        using var answer = JsonDocument.Parse(await send.Content.ReadAsStringAsync());
        var id = answer.RootElement.GetProperty("conversationId").GetGuid();
        var version = answer.RootElement.GetProperty("draftVersion").GetInt64();
        Assert.Equal(2, answer.RootElement.GetProperty("draft").GetProperty("items").GetArrayLength());
        var url = $"/api/meal-assistant/conversations/{id}/calculator-draft";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(url + "?expectedDraftVersion=-1")).StatusCode);
        using var matching = JsonDocument.Parse(await client.GetStringAsync(url + $"?expectedDraftVersion={version}"));
        Assert.Equal(2, matching.RootElement.GetArrayLength());
        Assert.Equal(49m, matching.RootElement.EnumerateArray().Sum(item => item.GetProperty("effectiveCarbs").GetDecimal()));
        Assert.True((await client.PostAsJsonAsync("/api/meal-assistant/messages", new MealAssistantRequest(id,
            "add 100 g Rice", Guid.NewGuid(), ExpectedDraftVersion: version))).IsSuccessStatusCode);
        var stale = await client.GetAsync(url + $"?expectedDraftVersion={version}");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var problem = JsonDocument.Parse(await stale.Content.ReadAsStringAsync());
        Assert.Equal("draft_version_conflict", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(version + 1, problem.RootElement.GetProperty("currentDraftVersion").GetInt64());
    }

    private sealed class DuringExport(Func<Task> change) : DbCommandInterceptor
    {
        public bool Triggered { get; private set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Triggered && command.CommandText.Contains("\"FoodItems\"", StringComparison.Ordinal))
            { Triggered = true; await change(); }
            return result;
        }
    }
}
