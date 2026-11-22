using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace InsulinAndCoffee.Application.Tests;

public sealed class AssistantPostgresFactAttribute : FactAttribute
{
    public AssistantPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASSISTANT_TEST_POSTGRES")))
            Skip = "Set ASSISTANT_TEST_POSTGRES to a local PostgreSQL admin connection; tests create isolated temporary databases.";
    }
}

[CollectionDefinition("Assistant PostgreSQL", DisableParallelization = true)]
public class AssistantPostgresCollection;

[Collection("Assistant PostgreSQL")]
public partial class AssistantPostgreSqlSafetyTests
{
    [AssistantPostgresFact]
    public async Task StructuredOrders_MigrationRelationsAndConcurrentEdits_PreserveLegacyAndAtomicChildren()
    {
        await using var database = await TestDatabase.CreateAsync("20261121132517_AddCarbProvenanceAndVerification");
        var owner = Guid.NewGuid();
        var legacyId = Guid.NewGuid();
        var now = TimeProvider.System.GetUtcNow();
        await using (var old = database.Open())
        {
            old.Users.Add(User(owner)); await old.SaveChangesAsync(CancellationToken.None);
            await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"DeliveryMeals\" (\"Id\", \"UserId\", \"PlaceName\", \"DishName\", \"PortionDescription\", \"Carbs\", \"UsualInsulinUnits\", \"ResultRating\", \"Tags\", \"IsFavorite\", \"UsageCount\", \"CreatedAt\", \"CarbReferenceSource\", \"CarbTransformation\") VALUES ({legacyId}, {owner}, {"Legacy cafe"}, {"Untouched description"}, {"Five pieces, unknown composition"}, {135m}, {8m}, {0}, {""}, {false}, {3}, {now}, {0}, {0})", CancellationToken.None);
            await old.Database.MigrateAsync(CancellationToken.None);
        }
        Guid itemId;
        Guid foodId;
        Guid orderId;
        await using (var setup = database.Open())
        {
            var legacy = await setup.DeliveryMeals.Include(order => order.Items).SingleAsync(order => order.Id == legacyId);
            Assert.Empty(legacy.Items); Assert.Equal(135m, legacy.Carbs); Assert.Equal(8m, legacy.UsualInsulinUnits);
            Assert.Equal("Untouched description", legacy.DishName); Assert.Equal(3, legacy.UsageCount);
            var converted = await Orders(setup).UpdateAsync(legacy.Id, Request(135m,
                [new("California", 1m, FoodMeasurementType.Portion, CarbsPerUnit: 60m), new("Sushi", 5m, FoodMeasurementType.Piece, CarbsPerUnit: 15m)]) with {
                    ExpectedRevision = 0, PlaceName = legacy.PlaceName, DishName = legacy.DishName, PortionDescription = legacy.PortionDescription,
                    UsualInsulinUnits = legacy.UsualInsulinUnits }, CancellationToken.None);
            Assert.Equal(2, converted.Items?.Count);
            Assert.Equal(135m, converted.Carbs);
            Assert.Equal(legacy.PortionDescription, converted.PortionDescription);
            var food = Food(owner, "Linked sushi", 15m); foodId = food.Id;
            setup.FoodItems.Add(food); await setup.SaveChangesAsync(CancellationToken.None);
            var created = await Orders(setup).CreateAsync(Request(75m, [new("Sushi", 5m, FoodMeasurementType.Piece, CarbsPerUnit: 15m, FoodItemId: food.Id)]), CancellationToken.None);
            orderId = created.Id; itemId = created.Items![0].Id;
            Assert.False(setup.Database.HasPendingModelChanges());
        }
        await using var first = database.Open();
        await using var stale = database.Open();
        await first.DeliveryMeals.Include(order => order.Items).SingleAsync(order => order.Id == orderId);
        await stale.DeliveryMeals.Include(order => order.Items).SingleAsync(order => order.Id == orderId);
        await Orders(first).UpdateAsync(orderId, Request(45m, [new("Sushi", 3m, FoodMeasurementType.Piece, CarbsPerUnit: 15m, Id: itemId, FoodItemId: foodId)]) with
            { ExpectedRevision = 0, AcceptCalculatedTotal = true }, CancellationToken.None);
        await Assert.ThrowsAsync<DeliveryMealConflictException>(() => Orders(stale).UpdateAsync(orderId, Request(30m,
            [new("Sushi", 2m, FoodMeasurementType.Piece, CarbsPerUnit: 15m, Id: itemId, FoodItemId: foodId)]) with
            { ExpectedRevision = 0, AcceptCalculatedTotal = true }, CancellationToken.None));
        await using var check = database.Open();
        var saved = await check.DeliveryMeals.Include(order => order.Items).SingleAsync(order => order.Id == orderId);
        Assert.Equal(45m, saved.Carbs); Assert.Equal(3m, saved.Items.Single().Quantity); Assert.Equal(1, saved.Revision);
        check.FoodItems.Remove(await check.FoodItems.SingleAsync(food => food.Id == foodId));
        await check.SaveChangesAsync(CancellationToken.None);
        check.DiscardChanges();
        var detachedFood = await check.DeliveryMealItems.SingleAsync(item => item.Id == itemId);
        Assert.Null(detachedFood.FoodItemId); Assert.Equal(15m, detachedFood.CarbsPerUnitSnapshot);
        check.DeliveryMeals.Remove(await check.DeliveryMeals.SingleAsync(order => order.Id == orderId));
        await check.SaveChangesAsync(CancellationToken.None);
        Assert.False(await check.DeliveryMealItems.AnyAsync(item => item.DeliveryMealId == orderId));

        DeliveryMealService Orders(AppDbContext db) => new(db, TimeProvider.System, new CurrentUser(owner));
        UpsertDeliveryMealRequest Request(decimal carbs, IReadOnlyList<DeliveryMealItemInput> items) => new("Le Grill", "Structured order", "Original description", carbs, null, null, ResultRating.Unknown, "", null, false, items);
    }

    [AssistantPostgresFact]
    public async Task NewAccount_EmptyCollections_ReturnSuccessfulResponsesOnPostgreSql()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var factory = new StartupFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var username = "empty-" + Guid.NewGuid().ToString("N")[..12];
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", new { username, password = "empty-account-test" })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "empty-account-test" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var auth = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.RootElement.GetProperty("accessToken").GetString());
        foreach (var route in new[] { "/api/meals", "/api/meals?search=soup", "/api/meals?mealType=Lunch", "/api/meal-assistant/conversations", "/api/supplies" })
        {
            var response = await client.GetAsync(route);
            Assert.True(response.IsSuccessStatusCode, $"{route}: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(0, json.RootElement.GetArrayLength());
        }
        using var foods = JsonDocument.Parse(await client.GetStringAsync("/api/foods"));
        Assert.Equal(0, foods.RootElement.GetProperty("totalCount").GetInt32());
        using var orders = JsonDocument.Parse(await client.GetStringAsync("/api/delivery-meals"));
        foreach (var section in new[] { "favorites", "mostUsed", "recentlyUsed", "searchResults" })
            Assert.Equal(0, orders.RootElement.GetProperty(section).GetArrayLength());
        using var dashboard = JsonDocument.Parse(await client.GetStringAsync("/api/dashboard/today"));
        Assert.Equal(0, dashboard.RootElement.GetProperty("meals").GetArrayLength());
        Assert.Equal(0m, dashboard.RootElement.GetProperty("totalCarbs").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/meals/calculate", new { mealType = "Lunch", preMealGlucose = 5.5m, directCarbs = 20m, items = Array.Empty<object>() })).StatusCode);
        var owner = auth.RootElement.GetProperty("user").GetProperty("id").GetGuid();
        await using var assistantDb = database.Open();
        var recent = await Service(assistantDb, owner, new EmptyAccountModel(new([], "recent")))
            .SendAsync(new(null, "What did I eat last time?", Guid.NewGuid()), CancellationToken.None);
        Assert.Empty(recent.Meals);
        Assert.Contains("couldn't find", recent.Message);
        var lookup = await Service(assistantDb, owner, new EmptyAccountModel(new([new("Bread", null, null)], "lookup")))
            .SendAsync(new(null, "Find bread", Guid.NewGuid()), CancellationToken.None);
        Assert.Empty(lookup.Meals);
        var draft = await Service(assistantDb, owner, new EmptyAccountModel(new([new("Bread", 100m, FoodMeasurementType.Grams)], "draft")))
            .SendAsync(new(null, "Plan 100g bread", Guid.NewGuid()), CancellationToken.None);
        Assert.Empty(Assert.IsType<AssistantDraft>(draft.Draft).Items);
        Assert.NotEmpty(draft.Draft.Issues);
        Assert.False(await assistantDb.Meals.AnyAsync(meal => meal.UserId == owner));
        Assert.False(await assistantDb.FoodItems.AnyAsync(food => food.UserId == owner));
        Assert.False(await assistantDb.DeliveryMeals.AnyAsync(order => order.UserId == owner));

        // First saved food, first meal, and first reusable order need no pre-existing history.
        var firstFood = await client.PostAsJsonAsync("/api/foods", new {
            name = "Empty-account test food", measurementType = "Grams", carbsPer100g = 20m,
            proteinPer100g = 0m, fatPer100g = 0m, caloriesPer100g = 0m, isFavorite = false });
        Assert.Equal(HttpStatusCode.Created, firstFood.StatusCode);
        using var foodJson = JsonDocument.Parse(await firstFood.Content.ReadAsStringAsync());
        var foodId = foodJson.RootElement.GetProperty("id").GetGuid();
        var firstMeal = await client.PostAsJsonAsync("/api/meals", new { mealType = "Lunch", preMealGlucose = 5.5m,
            items = new[] { new { foodItemId = foodId, quantity = 100m } } });
        Assert.Equal(HttpStatusCode.Created, firstMeal.StatusCode);
        using var firstMealJson = JsonDocument.Parse(await firstMeal.Content.ReadAsStringAsync());
        Assert.Equal(20m, firstMealJson.RootElement.GetProperty("totalCarbs").GetDecimal());
        Assert.Equal(JsonValueKind.Null, firstMealJson.RootElement.GetProperty("confirmedBolus").ValueKind);
        using var historyJson = JsonDocument.Parse(await client.GetStringAsync("/api/meals"));
        Assert.Equal(1, historyJson.RootElement.GetArrayLength());
        var firstOrder = await client.PostAsJsonAsync("/api/delivery-meals", new {
            placeName = "Test cafe", dishName = "Test order", portionDescription = "One order", carbs = 30m,
            usualInsulinUnits = (decimal?)null, resultRating = "Unknown", tags = "", isFavorite = false });
        Assert.Equal(HttpStatusCode.Created, firstOrder.StatusCode);
        using var orderJson = JsonDocument.Parse(await firstOrder.Content.ReadAsStringAsync());
        var orderId = orderJson.RootElement.GetProperty("id").GetGuid();
        var reuse = await client.PostAsync($"/api/delivery-meals/{orderId}/meal-draft", null);
        Assert.Equal(HttpStatusCode.OK, reuse.StatusCode);
        using var reuseJson = JsonDocument.Parse(await reuse.Content.ReadAsStringAsync());
        Assert.Equal(30m, reuseJson.RootElement.GetProperty("carbs").GetDecimal());
        // Reusing an order prepares a draft, never silently creates another historical meal.
        Assert.Equal(1, await assistantDb.Meals.CountAsync(meal => meal.UserId == owner));
    }

    private sealed class EmptyAccountModel(AssistantSearch search) : ILlmClient
    {
        public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken) => Task.FromResult(search);
    }

    [AssistantPostgresFact]
    public async Task Settings_ConcurrentFirstRequests_ReturnSamePersistedDefaults()
    {
        await using var database = await TestDatabase.CreateAsync();
        var owner = Guid.NewGuid();
        await using (var setup = database.Open())
        {
            setup.Users.Add(User(owner));
            await setup.SaveChangesAsync(CancellationToken.None);
        }
        var gate = new ConcurrentSave();
        await using var first = database.Open(gate);
        await using var second = database.Open(gate);
        var results = await Task.WhenAll(
            new SettingsService(first, TimeProvider.System, new CurrentUser(owner)).GetSettingsAsync(CancellationToken.None),
            new SettingsService(second, TimeProvider.System, new CurrentUser(owner)).GetSettingsAsync(CancellationToken.None));
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(results[0].CarbRatio, results[1].CarbRatio);
        Assert.True(Math.Abs((results[0].UpdatedAt - results[1].UpdatedAt).Ticks) < 10); // PostgreSQL microsecond precision.
        await using var check = database.Open();
        Assert.Equal(1, await check.DiabetesSettings.CountAsync(s => s.UserId == owner));
        Assert.Equal(10m, results[0].CarbRatio);
        Assert.Equal(6.5m, results[0].TargetGlucose);
    }

    [AssistantPostgresFact]
    public async Task CarbProvenanceMigration_LegacySnapshotRemainsUnknownAndNumericallyUnchanged()
    {
        await using var database = await TestDatabase.CreateAsync("20261120223407_AddAssistantDraftConcurrency");
        var owner = Guid.NewGuid();
        var mealId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var now = TimeProvider.System.GetUtcNow();
        await using (var legacy = database.Open())
        {
            legacy.Users.Add(User(owner));
            await legacy.SaveChangesAsync(CancellationToken.None);
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Meals\" (\"Id\", \"UserId\", \"MealType\", \"MealTime\", \"PreMealGlucose\", \"TotalCarbs\", \"CarbAdjustment\", \"SuggestedBolus\", \"ConfirmedBolus\", \"CreatedAt\") VALUES ({mealId}, {owner}, {1}, {now}, {6m}, {17m}, {-1m}, {2m}, {1.5m}, {now})", CancellationToken.None);
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"MealItems\" (\"Id\", \"MealId\", \"FoodItemId\", \"FoodNameSnapshot\", \"Quantity\", \"MeasurementType\", \"WeightGrams\", \"CarbsPer100gSnapshot\", \"CalculatedCarbs\", \"CarbOverride\") VALUES ({itemId}, {mealId}, {Guid.NewGuid()}, {"Legacy soup"}, {150m}, {0}, {150m}, {10m}, {15m}, {18m})", CancellationToken.None);
            await legacy.Database.MigrateAsync(CancellationToken.None);
        }
        await using var check = database.Open();
        var meal = await check.Meals.Include(m => m.Items).SingleAsync(m => m.Id == mealId);
        var item = Assert.Single(meal.Items);
        Assert.Equal(CarbReferenceSource.Unknown, item.CarbReferenceSource);
        Assert.Equal(CarbTransformation.None, item.CarbTransformation);
        Assert.Null(item.SourceMealVerifiedAt);
        Assert.Null(meal.CarbsVerifiedAt);
        Assert.Equal(0, meal.CarbRevision);
        Assert.Equal(15m, item.CalculatedCarbs);
        Assert.Equal(18m, item.CarbOverride);
        Assert.Equal(10m, item.CarbsPer100gSnapshot);
        Assert.Equal(150m, item.Quantity);
        Assert.Equal(-1m, meal.CarbAdjustment);
        Assert.Equal(17m, meal.TotalCarbs);
        Assert.Equal(1.5m, meal.ConfirmedBolus);
        Assert.False(check.Database.HasPendingModelChanges());
    }

    [AssistantPostgresFact]
    public async Task CarbVerification_ConcurrentSnapshotEditAndReview_CannotCommitStaleVerification()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        foreach (var verifyFirst in new[] { true, false })
        {
            Guid mealId;
            Guid itemId;
            await using (var setup = database.Open())
            {
                if (!await setup.DiabetesSettings.AnyAsync(s => s.UserId == seed.Owner))
                    setup.DiabetesSettings.Add(DiabetesSettings.CreateDefaults(Guid.NewGuid(), seed.Owner, TimeProvider.System.GetUtcNow()));
                await setup.SaveChangesAsync(CancellationToken.None);
                var meal = await Meals(setup).CreateMealAsync(new(MealType.Lunch, null, 6m, null, null, [new(seed.BreadId, 1m)]), CancellationToken.None);
                mealId = meal.Id;
                itemId = meal.Items[0].Id;
            }
            await using var stale = database.Open();
            await stale.Meals.Include(m => m.Items).SingleAsync(m => m.Id == mealId);
            await using (var winner = database.Open())
            {
                if (verifyFirst) await Meals(winner).VerifyCarbsAsync(mealId, new(0), CancellationToken.None);
                else await Meals(winner).UpdateMealItemAsync(mealId, itemId, new(Quantity: 2m), CancellationToken.None);
            }
            if (verifyFirst)
                await Assert.ThrowsAsync<CarbVerificationConflictException>(() => Meals(stale).UpdateMealItemAsync(mealId, itemId, new(Quantity: 2m), CancellationToken.None));
            else
                await Assert.ThrowsAsync<CarbVerificationConflictException>(() => Meals(stale).VerifyCarbsAsync(mealId, new(0), CancellationToken.None));
            await using var check = database.Open();
            var saved = await check.Meals.Include(m => m.Items).SingleAsync(m => m.Id == mealId);
            Assert.Equal(verifyFirst ? 1m : 2m, saved.Items.Single().Quantity);
            Assert.Equal(verifyFirst, saved.CarbsVerifiedAt.HasValue);
            Assert.Equal(1, saved.CarbRevision);
        }

        MealService Meals(AppDbContext db) => new(db, TimeProvider.System, new MealCalculationService(db, new CurrentUser(seed.Owner)), new CurrentUser(seed.Owner));
    }

    [AssistantPostgresFact]
    public async Task Send_TwoServiceInstancesEditingSameVersion_OnlyOneMutationCommits()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        var model = new ConcurrentModel();
        await using var firstDb = database.Open();
        await using var secondDb = database.Open();
        var first = SendOrConflict(Service(firstDb, seed.Owner, model), new(seed.ConversationId, "Please add bread", Guid.NewGuid(), ExpectedDraftVersion: 1));
        var second = SendOrConflict(Service(secondDb, seed.Owner, model), new(seed.ConversationId, "Please add bread", Guid.NewGuid(), ExpectedDraftVersion: 1));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Response is not null);
        Assert.Equal("draft_version_conflict", Assert.Single(results, result => result.Error is not null).Error?.Code);
        await using var verify = database.Open();
        var state = await Service(verify, seed.Owner).GetDraftStateAsync(seed.ConversationId, CancellationToken.None);
        Assert.Equal(2, state.DraftVersion);
        Assert.Equal(2, Assert.IsType<AssistantDraft>(state.Draft).Items.Count);
        Assert.Equal(15m, state.Draft.TotalCarbs);
        Assert.Equal(2, await verify.AssistantRequestReceipts.CountAsync(CancellationToken.None));
        Assert.Equal(4, await verify.AssistantMessages.CountAsync(CancellationToken.None));
    }

    [AssistantPostgresFact]
    public async Task Send_SameRequestConcurrentAcrossInstances_ReplaysWinnerWithoutDuplicateAddition()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        var request = new MealAssistantRequest(seed.ConversationId, "Please add bread", Guid.NewGuid(), ExpectedDraftVersion: 1);
        var model = new ConcurrentModel();
        await using var firstDb = database.Open();
        await using var secondDb = database.Open();
        var answers = await Task.WhenAll(Service(firstDb, seed.Owner, model).SendAsync(request, CancellationToken.None),
            Service(secondDb, seed.Owner, model).SendAsync(request, CancellationToken.None));

        Assert.Single(answers, answer => answer.Replayed);
        Assert.All(answers, answer => { Assert.Equal(2, answer.DraftVersion); Assert.Equal(15m, answer.Draft?.TotalCarbs); });
        Assert.Equal(2, model.Calls); // Durable mutation protection does not promise exactly-once interpretation.
        await using var verify = database.Open();
        Assert.Equal(2, await verify.AssistantRequestReceipts.CountAsync(CancellationToken.None));
        Assert.Equal(4, await verify.AssistantMessages.CountAsync(CancellationToken.None));
        Assert.Equal(2, (await Service(verify, seed.Owner).GetDraftStateAsync(seed.ConversationId, CancellationToken.None)).Draft?.Items.Count);
    }

    [AssistantPostgresFact]
    public async Task Send_ConcurrentFirstRequest_ReplaysWithoutCreatingTwoConversations()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        var gate = new ConcurrentSave();
        await using var firstDb = database.Open(gate);
        await using var secondDb = database.Open(gate);
        var request = new MealAssistantRequest(null, "Choose bread", Guid.NewGuid(), new(seed.BreadId, 1m));
        var answers = await Task.WhenAll(Service(firstDb, seed.Owner).SendAsync(request, CancellationToken.None),
            Service(secondDb, seed.Owner).SendAsync(request, CancellationToken.None));
        Assert.Equal(answers[0].ConversationId, answers[1].ConversationId);
        Assert.Single(answers, answer => answer.Replayed);
        await using var verify = database.Open();
        Assert.Equal(2, await verify.AssistantConversations.CountAsync(CancellationToken.None)); // Seed + one new chat.
        Assert.Equal(2, await verify.AssistantRequestReceipts.CountAsync(CancellationToken.None));
        Assert.Equal(4, await verify.AssistantMessages.CountAsync(CancellationToken.None));
    }

    [AssistantPostgresFact]
    public async Task Send_ConcurrentRequestIdWithDifferentContent_RejectsLoserWithoutPartialCommit()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        var model = new ConcurrentModel();
        var request = new MealAssistantRequest(seed.ConversationId, "Please add bread", Guid.NewGuid(), ExpectedDraftVersion: 1);
        await using var firstDb = database.Open();
        await using var secondDb = database.Open();
        var answers = await Task.WhenAll(SendOrConflict(Service(firstDb, seed.Owner, model), request),
            SendOrConflict(Service(secondDb, seed.Owner, model), request with { Message = "Please add a different amount of bread" }));
        Assert.Single(answers, answer => answer.Response is not null);
        Assert.Equal("assistant_request_reused", Assert.Single(answers, answer => answer.Error is not null).Error?.Code);
        await using var verify = database.Open();
        Assert.Equal(2, await verify.AssistantRequestReceipts.CountAsync(CancellationToken.None));
        Assert.Equal(4, await verify.AssistantMessages.CountAsync(CancellationToken.None));
    }

    [AssistantPostgresFact]
    public async Task Send_ValidSequentialEdit_RejectsStaleVersionAndPayloadReuse()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        await using var db = database.Open();
        var service = Service(db, seed.Owner);
        var request = new MealAssistantRequest(seed.ConversationId, "Choose bread", Guid.NewGuid(), new(seed.BreadId, 1m), 1);
        var response = await service.SendAsync(request, CancellationToken.None);
        Assert.Equal(2, response.DraftVersion);
        var stale = await Assert.ThrowsAsync<AssistantConflictException>(() => service.SendAsync(
            request with { RequestId = Guid.NewGuid() }, CancellationToken.None));
        Assert.Equal("draft_version_conflict", stale.Code);
        Assert.Equal(2, stale.CurrentDraftVersion);
        var missing = await Assert.ThrowsAsync<AssistantConflictException>(() => service.SendAsync(
            request with { RequestId = Guid.NewGuid(), ExpectedDraftVersion = null }, CancellationToken.None));
        Assert.Equal("draft_version_required", missing.Code);
        var reused = await Assert.ThrowsAsync<AssistantConflictException>(() => service.SendAsync(
            request with { Selection = new(seed.BreadId, 2m) }, CancellationToken.None));
        Assert.Equal("assistant_request_reused", reused.Code);
        var versionReused = await Assert.ThrowsAsync<AssistantConflictException>(() => service.SendAsync(
            request with { ExpectedDraftVersion = 2 }, CancellationToken.None));
        Assert.Equal("assistant_request_reused", versionReused.Code);
        Assert.Equal(4, await db.AssistantMessages.CountAsync(CancellationToken.None));
        Assert.Equal(15m, (await service.GetDraftStateAsync(seed.ConversationId, CancellationToken.None)).Draft?.TotalCarbs);
    }

    [AssistantPostgresFact]
    public async Task Replay_AfterTranscriptPruningAndLostResponse_ReturnsOriginalWithoutChangingCurrentDraft()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        await using var db = database.Open();
        var service = Service(db, seed.Owner);
        var request = new MealAssistantRequest(seed.ConversationId, "Choose bread", Guid.NewGuid(), new(seed.BreadId, 1m), 1);
        // Treat this committed result as a response lost in transit.
        var original = await service.SendAsync(request, CancellationToken.None);
        var originalMessage = await db.AssistantMessages.SingleAsync(message => message.Role == "assistant" &&
            message.ResponseJson != null && message.ResponseJson.Contains(request.RequestId.ToString() ?? ""), CancellationToken.None);
        var newer = await service.SendAsync(new(seed.ConversationId, "Choose another bread", Guid.NewGuid(), new(seed.BreadId, 1m), 2), CancellationToken.None);
        for (var index = 0; index < 25; index++)
            await service.SendAsync(new(seed.ConversationId, "What did I eat last time?", Guid.NewGuid()), CancellationToken.None);
        Assert.False(await db.AssistantMessages.AnyAsync(message => message.Id == originalMessage.Id, CancellationToken.None));
        var replay = await service.SendAsync(request, CancellationToken.None);
        Assert.True(replay.Replayed);
        Assert.Equal(original.RequestId, replay.RequestId);
        Assert.Equal(original.DraftVersion, replay.DraftVersion);
        Assert.Equal(original.Draft?.TotalCarbs, replay.Draft?.TotalCarbs);
        var state = await service.GetDraftStateAsync(seed.ConversationId, CancellationToken.None);
        Assert.Equal(newer.DraftVersion, state.DraftVersion);
        Assert.Equal(20m, state.Draft?.TotalCarbs);
        Assert.Equal(3, state.Draft?.Items.Count);
    }

    [AssistantPostgresFact]
    public async Task Receipt_DatabaseUniquenessAndUserScope_AreEnforced()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        await using var db = database.Open();
        var receipt = await db.AssistantRequestReceipts.AsNoTracking().SingleAsync(CancellationToken.None);
        db.AssistantRequestReceipts.Add(new AssistantRequestReceipt { UserId = receipt.UserId, RequestId = receipt.RequestId,
            ConversationId = receipt.ConversationId, PayloadHash = receipt.PayloadHash, ResponseJson = receipt.ResponseJson, CreatedAt = receipt.CreatedAt });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(CancellationToken.None));
        db.DiscardChanges();

        var other = Guid.NewGuid();
        db.Users.Add(User(other));
        var otherFood = Food(other, "Other soup", 3m);
        db.FoodItems.Add(otherFood);
        await db.SaveChangesAsync(CancellationToken.None);
        var otherResponse = await Service(db, other).SendAsync(new(null, "Choose other soup", receipt.RequestId, new(otherFood.Id)), CancellationToken.None);
        Assert.NotEqual(seed.ConversationId, otherResponse.ConversationId);
        await Assert.ThrowsAsync<InsulinAndCoffee.Domain.Exceptions.NotFoundException>(() =>
            Service(db, other).GetDraftStateAsync(seed.ConversationId, CancellationToken.None));
        Assert.Equal(2, await db.AssistantRequestReceipts.CountAsync(CancellationToken.None));
    }

    [AssistantPostgresFact]
    public async Task Send_ReadOnlyLookup_PreservesPersistedDraftAndVersion()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        await using var db = database.Open();
        var before = await db.AssistantConversations.AsNoTracking().SingleAsync(conversation => conversation.Id == seed.ConversationId, CancellationToken.None);
        var response = await Service(db, seed.Owner).SendAsync(new(seed.ConversationId, "What did I eat last time?", Guid.NewGuid(), ExpectedDraftVersion: 0), CancellationToken.None);
        var after = await db.AssistantConversations.AsNoTracking().SingleAsync(conversation => conversation.Id == seed.ConversationId, CancellationToken.None);
        Assert.Null(response.Draft);
        Assert.Equal(before.DraftVersion, after.DraftVersion);
        Assert.Equal(before.DraftJson, after.DraftJson);
    }

    [AssistantPostgresFact]
    public async Task Pruning_PendingClarification_RemainsAvailableToInterpretation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        await using var db = database.Open();
        var pendingModel = new ProbeModel(new([new("Unknown sauce", 3m, FoodMeasurementType.Portion)], "draft"));
        var pending = await Service(db, seed.Owner, pendingModel).SendAsync(
            new(seed.ConversationId, "Plan unknown sauce", Guid.NewGuid(), ExpectedDraftVersion: 1), CancellationToken.None);
        Assert.Single(Assert.IsType<AssistantDraft>(pending.Draft).Issues);
        for (var index = 0; index < 25; index++)
            await Service(db, seed.Owner).SendAsync(new(seed.ConversationId, "What did I eat last time?", Guid.NewGuid()), CancellationToken.None);
        var probe = new ProbeModel(new([]));
        await Service(db, seed.Owner, probe).SendAsync(new(seed.ConversationId, "What about three portions?", Guid.NewGuid()), CancellationToken.None);
        Assert.Contains(probe.Context, context => context.Content.Contains("Pending clarification:", StringComparison.Ordinal)
            && context.Content.Contains("Unknown sauce", StringComparison.Ordinal));
    }

    [AssistantPostgresFact]
    public async Task Delete_Conversation_ClearsReceiptContentAndRetryDoesNotRecreateIt()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        await using var db = database.Open();
        var service = Service(db, seed.Owner);
        var request = new MealAssistantRequest(seed.ConversationId, "Choose bread", Guid.NewGuid(), new(seed.BreadId, 1m), 1);
        await service.SendAsync(request, CancellationToken.None);
        await service.DeleteAsync(seed.ConversationId, CancellationToken.None);
        await Assert.ThrowsAsync<InsulinAndCoffee.Domain.Exceptions.NotFoundException>(() => service.SendAsync(request, CancellationToken.None));
        Assert.Empty(await db.AssistantConversations.ToListAsync(CancellationToken.None));
        Assert.Empty(await db.AssistantMessages.ToListAsync(CancellationToken.None));
        Assert.All(await db.AssistantRequestReceipts.ToListAsync(CancellationToken.None), receipt => Assert.Equal("", receipt.ResponseJson));
    }

    [AssistantPostgresFact]
    public async Task Migration_ExistingTranscript_BackfillsReadableVersionZeroDraft()
    {
        await using var database = await TestDatabase.CreateAsync("20261114211333_AllowUnrecordedDeliveryInsulin");
        var owner = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var camelConversationId = Guid.NewGuid();
        var now = TimeProvider.System.GetUtcNow();
        var legacy = new AssistantDraft([], ["Legacy unresolved item"], 0m);
        var responseJson = JsonSerializer.Serialize(new MealAssistantResponse(conversationId, "Legacy draft", [], legacy));
        await using (var oldDb = database.Open())
        {
            oldDb.Users.Add(User(owner));
            await oldDb.SaveChangesAsync(CancellationToken.None);
            await oldDb.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AssistantConversations\" (\"Id\", \"UserId\", \"UpdatedAt\") VALUES ({conversationId}, {owner}, {now})", CancellationToken.None);
            await oldDb.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AssistantMessages\" (\"Id\", \"ConversationId\", \"Role\", \"Content\", \"CreatedAt\", \"ResponseJson\") VALUES ({Guid.NewGuid()}, {conversationId}, {"assistant"}, {"Legacy draft"}, {now}, {responseJson})", CancellationToken.None);
            var camelJson = JsonSerializer.Serialize(new MealAssistantResponse(camelConversationId, "Legacy draft", [], legacy),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await oldDb.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AssistantConversations\" (\"Id\", \"UserId\", \"UpdatedAt\") VALUES ({camelConversationId}, {owner}, {now})", CancellationToken.None);
            await oldDb.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AssistantMessages\" (\"Id\", \"ConversationId\", \"Role\", \"Content\", \"CreatedAt\", \"ResponseJson\") VALUES ({Guid.NewGuid()}, {camelConversationId}, {"assistant"}, {"Legacy draft"}, {now}, {camelJson})", CancellationToken.None);
            await oldDb.Database.MigrateAsync(CancellationToken.None);
        }
        await using var verify = database.Open();
        var state = await Service(verify, owner).GetDraftStateAsync(conversationId, CancellationToken.None);
        Assert.Equal(0, state.DraftVersion);
        Assert.Equal("Legacy unresolved item", Assert.Single(Assert.IsType<AssistantDraft>(state.Draft).Issues));
        var camelState = await Service(verify, owner).GetDraftStateAsync(camelConversationId, CancellationToken.None);
        Assert.Equal("Legacy unresolved item", Assert.Single(Assert.IsType<AssistantDraft>(camelState.Draft).Issues));
        Assert.False(verify.Database.HasPendingModelChanges());
    }

    [AssistantPostgresFact]
    public async Task Startup_ProductionEnvironment_AppliesMigrationAndKeepsHealthPublic()
    {
        await using var database = await TestDatabase.CreateAsync(migrate: false);
        await using var factory = new StartupFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/foods")).StatusCode);
        await using var verify = database.Open();
        Assert.Empty(await verify.Database.GetPendingMigrationsAsync(CancellationToken.None));
        Assert.Equal(0, await verify.AssistantRequestReceipts.CountAsync(CancellationToken.None));
    }

    private sealed class StartupFactory(string connectionString, ILlmClient? model = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            if (model is not null) builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILlmClient>();
                services.AddSingleton(model);
            });
        }

        protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Jwt:SigningKey"] = "isolated-postgresql-startup-test-key-32-characters"
            }));
            return base.CreateHost(builder);
        }
    }

    private static async Task<(MealAssistantResponse? Response, AssistantConflictException? Error)> SendOrConflict(MealAssistantService service, MealAssistantRequest request)
    {
        try { return (await service.SendAsync(request, CancellationToken.None), null); }
        catch (AssistantConflictException error) { return (null, error); }
    }

    private static async Task<(Guid Owner, Guid BreadId, Guid ConversationId)> SeedAsync(TestDatabase database)
    {
        await using var db = database.Open();
        var owner = Guid.NewGuid();
        db.Users.Add(User(owner));
        var soup = Food(owner, "Soup", 10m);
        var bread = Food(owner, "Bread", 5m);
        db.FoodItems.AddRange(soup, bread);
        await db.SaveChangesAsync(CancellationToken.None);
        var created = await Service(db, owner).SendAsync(new(null, "Choose soup", Guid.NewGuid(), new(soup.Id, 1m)), CancellationToken.None);
        Assert.Equal(1, created.DraftVersion);
        return (owner, bread.Id, created.ConversationId);
    }

    private static User User(Guid id) => new() { Id = id, Username = "pg-" + id.ToString("N"), NormalizedUsername = "PG-" + id.ToString("N"), PasswordHash = "test-only", CreatedAt = TimeProvider.System.GetUtcNow() };
    private static FoodItem Food(Guid owner, string name, decimal carbs) => new() { Id = Guid.NewGuid(), UserId = owner, Name = name, MeasurementType = FoodMeasurementType.Portion, CarbsPerUnit = carbs };
    private static MealAssistantService Service(AppDbContext db, Guid owner, ILlmClient? model = null)
    {
        var user = new CurrentUser(owner);
        return new(db, user, model ?? new UnexpectedModel(), new MealHistorySearchService(db, user), TimeProvider.System,
            NullLogger<MealAssistantService>.Instance, new AssistantDraftBuilder(db, user, new MealCalculationService(db, user)), new FoodKnowledgeSearchService(db, user));
    }
    private sealed class CurrentUser(Guid id) : ICurrentUser { public Guid UserId => id; }
    private sealed class UnexpectedModel : ILlmClient
    {
        public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This request must not call a provider.");
    }
    private sealed class ProbeModel(AssistantSearch result) : ILlmClient
    {
        public IReadOnlyList<AssistantContext> Context { get; private set; } = [];
        public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken)
        { Context = context; return Task.FromResult(result); }
    }
    private sealed class ConcurrentModel : ILlmClient
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;
        public int Calls => calls;
        public async Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return new([new("Bread", 1m, FoodMeasurementType.Portion)], "draft", "add");
        }
    }

    private sealed class ConcurrentSave : SaveChangesInterceptor
    {
        private int calls;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref calls) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }

    private sealed class TestDatabase(string adminConnection, string name) : IAsyncDisposable
    {
        public string ConnectionString { get; } = new NpgsqlConnectionStringBuilder(adminConnection) { Database = name }.ConnectionString;
        public AppDbContext Open(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString).AddInterceptors(interceptors).Options);
        public static async Task<TestDatabase> CreateAsync(string? target = null, bool migrate = true)
        {
            var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ASSISTANT_TEST_POSTGRES"));
            if (settings.Host is not ("localhost" or "127.0.0.1" or "::1"))
                throw new InvalidOperationException("PostgreSQL safety tests require a local host; remote databases are forbidden.");
            settings.Database = "postgres";
            var name = "iac_assistant_pr3_" + Guid.NewGuid().ToString("N");
            var database = new TestDatabase(settings.ConnectionString, name);
            await using var admin = new NpgsqlConnection(settings.ConnectionString);
            await admin.OpenAsync(CancellationToken.None);
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await create.ExecuteNonQueryAsync(CancellationToken.None);
            try
            {
                if (migrate)
                {
                    await using var db = database.Open();
                    await db.GetService<IMigrator>().MigrateAsync(target, CancellationToken.None);
                }
                return database;
            }
            catch { await database.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            if (!name.StartsWith("iac_assistant_pr3_", StringComparison.Ordinal) || name.Length != 50)
                throw new InvalidOperationException("Refusing to remove an unexpected database.");
            await using var admin = new NpgsqlConnection(adminConnection);
            await admin.OpenAsync(CancellationToken.None);
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
