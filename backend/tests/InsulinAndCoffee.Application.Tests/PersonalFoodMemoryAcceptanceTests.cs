using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Tests;

public partial class AssistantPostgreSqlSafetyTests
{
    private static readonly JsonSerializerOptions AcceptanceJson = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };

    [AssistantPostgresFact]
    public async Task Acceptance_KnownFood_SearchSaveHistoryAndRepeat_PreservePersistedReferenceAndRounding()
    {
        await using var database = await TestDatabase.CreateAsync();
        var model = new AcceptanceModel();
        await using var factory = new StartupFactory(database.ConnectionString, model);
        using var client = await AcceptanceLogin(factory);
        var food = await Post<FoodItemDto>(client, "/api/foods", FoodRequest("Porridge", FoodMeasurementType.Grams, 23.37m));
        var lookup = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", new MealAssistantRequest(null, "Porridge", Guid.NewGuid()));
        Assert.Equal(food.Id, Assert.Single(lookup.Knowledge!.Foods).Id);
        var draft = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", new MealAssistantRequest(null, "Use 150g porridge", Guid.NewGuid(), new(food.Id, 150m, FoodMeasurementType.Grams)));
        Assert.Equal(35.06m, draft.Draft?.TotalCarbs);
        var prepared = await Get<List<CalculatedMealItemDto>>(client, $"/api/meal-assistant/conversations/{draft.ConversationId}/calculator-draft?expectedDraftVersion={draft.DraftVersion}");
        Assert.Equal(CarbReferenceSource.Manual, prepared[0].Provenance?.ReferenceSource);
        Assert.Equal(CarbTransformation.Calculated, prepared[0].Provenance?.Transformation);
        Assert.Equal(35.06m, (await Post<MealCalculationDto>(client, "/api/meals/calculate", new CalculateMealRequest(MealType.Lunch, 5.5m, Inputs(prepared)))).TotalCarbs);
        var meal = await Post<MealDetailDto>(client, "/api/meals", MealRequest(Inputs(prepared)));
        Assert.Null(meal.ConfirmedBolus); Assert.Null(meal.CarbsVerifiedAt);
        Assert.Equal(meal.Id, Assert.Single(await Get<List<MealSummaryDto>>(client, "/api/meals")).Id);
        await Send<FoodItemDto>(client, HttpMethod.Put, $"/api/foods/{food.Id}", FoodRequest("Changed reference", FoodMeasurementType.Grams, 99m));
        var original = await Get<MealDetailDto>(client, $"/api/meals/{meal.Id}");
        var repeated = await Post<MealDetailDto>(client, "/api/meals", MealRequest(Repeat(original)));
        Assert.Equal(35.06m, repeated.TotalCarbs); Assert.Equal(23.37m, repeated.Items[0].CarbsPer100gSnapshot);
        Assert.Equal("Porridge", repeated.Items[0].FoodNameSnapshot);
        Assert.True(repeated.Items[0].Provenance?.Transformation.HasFlag(CarbTransformation.HistoricalReuse));
        Assert.Equal(0, model.Calls);
    }

    [AssistantPostgresFact]
    public async Task Acceptance_HistoricalRepeat_OverrideAdjustmentVerificationAndMedicalValuesRemainDistinct()
    {
        await using var database = await TestDatabase.CreateAsync();
        var model = new AcceptanceModel();
        await using var factory = new StartupFactory(database.ConnectionString, model);
        using var client = await AcceptanceLogin(factory);
        var rice = await Post<FoodItemDto>(client, "/api/foods", FoodRequest("Rice", FoodMeasurementType.Grams, 20m));
        var bread = await Post<FoodItemDto>(client, "/api/foods", FoodRequest("Bread", FoodMeasurementType.Piece, 15m));
        var chicken = await Post<FoodItemDto>(client, "/api/foods", FoodRequest("Chicken", FoodMeasurementType.Portion, 0m));
        var original = await Post<MealDetailDto>(client, "/api/meals", MealRequest([new(rice.Id, 150m, CarbOverride: 25m), new(bread.Id, 2m), new(chicken.Id, 1m)], -3m) with { PreMealGlucose = 11m, ConfirmedBolus = 4m });
        Assert.Equal(52m, original.TotalCarbs); Assert.Null(original.CarbsVerifiedAt);
        await Post<MealDetailDto>(client, $"/api/meals/{original.Id}/carb-verification", new VerifyMealCarbsRequest(0));
        var recent = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", new MealAssistantRequest(null, "What did I eat last time?", Guid.NewGuid()));
        Assert.Equal(original.Id, Assert.Single(recent.Meals).Id);
        var persisted = await Get<MealDetailDto>(client, $"/api/meals/{original.Id}");
        Assert.NotNull(persisted.CarbsVerifiedAt);
        var input = Repeat(persisted);
        var repeat = await Post<MealDetailDto>(client, "/api/meals", MealRequest(input, persisted.CarbAdjustment));
        Assert.Equal(52m, repeat.TotalCarbs); Assert.Equal(-3m, repeat.CarbAdjustment);
        Assert.Equal(5.5m, repeat.PreMealGlucose); Assert.Null(repeat.ConfirmedBolus); Assert.Null(repeat.CarbsVerifiedAt);
        Assert.Equal(persisted.CarbsVerifiedAt, repeat.Items.Single(item => item.FoodItemId == rice.Id).Provenance?.SourceMealVerifiedAt);
        var index = Array.FindIndex(input, item => item.FoodItemId == rice.Id);
        input[index] = input[index] with { Quantity = 200m };
        var changed = await Post<MealCalculationDto>(client, "/api/meals/calculate", new CalculateMealRequest(MealType.Lunch, 5.5m, input, CarbAdjustment: -3m));
        Assert.Equal(40m, changed.Items[index].CalculatedCarbs); Assert.Equal(25m, changed.Items[index].CarbOverride); Assert.Equal(52m, changed.TotalCarbs);
        input[index] = input[index] with { CarbOverride = null, ClearCarbOverride = true };
        Assert.Equal(67m, (await Post<MealCalculationDto>(client, "/api/meals/calculate", new CalculateMealRequest(MealType.Lunch, 5.5m, input, CarbAdjustment: -3m))).TotalCarbs);
        var unchanged = await Get<MealDetailDto>(client, $"/api/meals/{original.Id}");
        var unchangedRice = unchanged.Items.Single(item => item.FoodItemId == rice.Id);
        Assert.Equal(150m, unchangedRice.Quantity); Assert.Equal(30m, unchangedRice.CalculatedCarbs); Assert.Equal(25m, unchangedRice.CarbOverride);
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/meals/{original.Id}/confirmed-bolus")).StatusCode);
        var edited = await Send<MealDetailDto>(client, HttpMethod.Put, $"/api/meals/{original.Id}/items/{original.Items.Single(item => item.FoodItemId == bread.Id).Id}", new UpdateMealItemRequest(3m));
        Assert.Null(edited.CarbsVerifiedAt); Assert.Equal(67m, edited.TotalCarbs); Assert.Equal(0, model.Calls);
    }

    [AssistantPostgresFact]
    public async Task Acceptance_StructuredAndLegacyOrders_SearchScaleMixPrepareAndSaveWithoutInventingData()
    {
        await using var database = await TestDatabase.CreateAsync();
        var model = new AcceptanceModel();
        await using var factory = new StartupFactory(database.ConnectionString, model);
        using var client = await AcceptanceLogin(factory);
        var bread = await Post<FoodItemDto>(client, "/api/foods", FoodRequest("Bread", FoodMeasurementType.Piece, 15m));
        var order = await Post<DeliveryMealDto>(client, "/api/delivery-meals", OrderRequest("Le Grill", 165m,
            [new("Rice", 150m, FoodMeasurementType.Grams, 20m), new("California", 1m, FoodMeasurementType.Portion, CarbsPerUnit: 60m), new("Sushi", 5m, FoodMeasurementType.Piece, CarbsPerUnit: 15m)]) with { UsualInsulinUnits = 4m, LastPreMealGlucose = 11m });
        var lookup = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", new MealAssistantRequest(null, "Sushi Le Grill", Guid.NewGuid()));
        Assert.Equal(order.Items![2].Id, Assert.Single(lookup.Knowledge!.Items!).Id);
        var selected = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", new MealAssistantRequest(null, "Add sushi", Guid.NewGuid(), new(order.Items[2].Id, 3m, FoodMeasurementType.Piece, "DeliveryMealItem")));
        var mixed = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", new MealAssistantRequest(selected.ConversationId, "Add bread", Guid.NewGuid(), new(bread.Id, 2m, FoodMeasurementType.Piece), selected.DraftVersion));
        Assert.Equal(75m, mixed.Draft?.TotalCarbs); Assert.Equal(0, model.Calls);
        model.Plans.Enqueue(new([new("Sushi", 2m, FoodMeasurementType.Piece)], "draft", "set", "Sushi"));
        var scaled = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", new MealAssistantRequest(mixed.ConversationId, "Use 2 sushi pieces instead of 3", Guid.NewGuid(), ExpectedDraftVersion: mixed.DraftVersion));
        Assert.Equal(60m, scaled.Draft?.TotalCarbs); Assert.Equal(1, model.Calls);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync($"/api/meal-assistant/conversations/{mixed.ConversationId}/calculator-draft?expectedDraftVersion={mixed.DraftVersion}")).StatusCode);
        var prepared = await Get<List<CalculatedMealItemDto>>(client, $"/api/meal-assistant/conversations/{scaled.ConversationId}/calculator-draft?expectedDraftVersion={scaled.DraftVersion}");
        Assert.Equal(order.Items[2].Id, prepared[0].SourceDeliveryMealItemId);
        Assert.True(prepared[0].Provenance?.Transformation.HasFlag(CarbTransformation.HistoricalReuse));
        var saved = await Post<MealDetailDto>(client, "/api/meals", MealRequest(Inputs(prepared)));
        Assert.Equal(60m, saved.TotalCarbs); Assert.Null(saved.ConfirmedBolus); Assert.Equal(5.5m, saved.PreMealGlucose);
        Assert.Equal(165m, (await Get<DeliveryMealDto>(client, $"/api/delivery-meals/{order.Id}")).Carbs);
        var legacy = await Post<DeliveryMealDto>(client, "/api/delivery-meals", OrderRequest("Huli Huli", 135m, null) with { PortionDescription = "Original description: five pieces plus California" });
        var found = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", new MealAssistantRequest(null, "Huli Huli", Guid.NewGuid()));
        Assert.Equal(legacy.Id, Assert.Single(found.Knowledge!.Orders).Id);
        var whole = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", new MealAssistantRequest(null, "Reuse complete order", Guid.NewGuid(), new(legacy.Id, Kind: "DeliveryMeal")));
        Assert.Equal(135m, whole.Draft?.TotalCarbs); Assert.Single(whole.Draft!.Items);
        var legacyPrepared = await Get<List<CalculatedMealItemDto>>(client, $"/api/meal-assistant/conversations/{whole.ConversationId}/calculator-draft?expectedDraftVersion={whole.DraftVersion}");
        Assert.Equal(legacy.Id, legacyPrepared[0].SourceDeliveryMealId); Assert.Equal(135m, legacyPrepared[0].EffectiveCarbs);
        Assert.Empty((await Get<DeliveryMealDto>(client, $"/api/delivery-meals/{legacy.Id}")).Items!);
        Assert.Equal(1, model.Calls);
    }

    [AssistantPostgresFact]
    public async Task Acceptance_AmbiguityIsolationAndUntrustedIds_RequireAuthorizedChoiceAndDoNotInventValues()
    {
        await using var database = await TestDatabase.CreateAsync();
        var model = new AcceptanceModel();
        await using var factory = new StartupFactory(database.ConnectionString, model);
        using var owner = await AcceptanceLogin(factory);
        var food = await Post<FoodItemDto>(owner, "/api/foods", FoodRequest("Rice", FoodMeasurementType.Grams, 20m));
        await Post<FoodItemDto>(owner, "/api/foods", FoodRequest("Rice", FoodMeasurementType.Grams, 25m));
        var order = await Post<DeliveryMealDto>(owner, "/api/delivery-meals", OrderRequest("Le Grill", 30m, [new("Rice", 150m, FoodMeasurementType.Grams, 20m)]));
        await Post<DeliveryMealDto>(owner, "/api/delivery-meals", OrderRequest("Huli Huli", 40m, [new("Rice", 200m, FoodMeasurementType.Grams, 20m)]));
        model.Plans.Enqueue(new([new("Rice", 150m, FoodMeasurementType.Grams)], "draft"));
        var ambiguous = await Post<MealAssistantResponse>(owner, "/api/meal-assistant/messages", new MealAssistantRequest(null, "Plan 150g rice", Guid.NewGuid()));
        Assert.Empty(ambiguous.Draft!.Items); Assert.NotEmpty(ambiguous.Draft.Issues);
        Assert.Equal(2, ambiguous.Knowledge!.Foods.Count); Assert.Equal(2, ambiguous.Knowledge.Items?.Count);
        var selected = await Post<MealAssistantResponse>(owner, "/api/meal-assistant/messages", new MealAssistantRequest(ambiguous.ConversationId, "Choose Le Grill rice", Guid.NewGuid(), new(order.Items![0].Id, 150m, FoodMeasurementType.Grams, "DeliveryMealItem"), ambiguous.DraftVersion));
        Assert.Equal(order.Items[0].Id, Assert.Single(selected.Draft!.Items).SourceDeliveryMealItemId); Assert.Equal(30m, selected.Draft.TotalCarbs); Assert.Equal(1, model.Calls);
        var meal = await Post<MealDetailDto>(owner, "/api/meals", MealRequest([new(food.Id, 150m)]));
        using var other = await AcceptanceLogin(factory);
        foreach (var path in new[] { $"/api/meals/{meal.Id}", $"/api/delivery-meals/{order.Id}", $"/api/meal-assistant/conversations/{selected.ConversationId}/calculator-draft?expectedDraftVersion={selected.DraftVersion}" })
        {
            using var response = await other.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Le Grill", text);
            using var problem = JsonDocument.Parse(text);
            Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("Resource not found", problem.RootElement.GetProperty("title").GetString());
            Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("detail").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        }
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/meals/{meal.Id}/carb-verification", new VerifyMealCarbsRequest(0), AcceptanceJson)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/delivery-meals/{order.Id}/items-draft", new ReuseDeliveryMealRequest([order.Items[0].Id], 0), AcceptanceJson)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/meals/calculate", new CalculateMealRequest(MealType.Lunch, 5.5m, [new(Guid.Empty, 150m, SourceDeliveryMealItemId: order.Items[0].Id)]), AcceptanceJson)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/meal-assistant/messages", new MealAssistantRequest(null, "Choose foreign item", Guid.NewGuid(), new(order.Items[0].Id, 150m, FoodMeasurementType.Grams, "DeliveryMealItem")), AcceptanceJson)).StatusCode);
        model.Plans.Enqueue(new([new("Unrecorded recipe", 100m, FoodMeasurementType.Grams, FoodItemId: food.Id, DeliveryMealItemId: order.Items[0].Id)], "draft"));
        var unknown = await Post<MealAssistantResponse>(other, "/api/meal-assistant/messages", new MealAssistantRequest(null, "Plan my unrecorded recipe", Guid.NewGuid()));
        Assert.Empty(unknown.Draft!.Items); Assert.NotEmpty(unknown.Draft.Issues); Assert.Empty(unknown.Knowledge!.Foods); Assert.Empty(unknown.Knowledge.Items!);
        Assert.Empty(await Get<List<MealSummaryDto>>(other, "/api/meals"));
        Assert.Equal(0, (await Get<PaginatedResult<FoodItemDto>>(other, "/api/foods")).TotalCount);
        Assert.Equal(2, model.Calls);
    }

    [AssistantPostgresFact]
    public async Task Acceptance_StructuredConcurrentEditsAndPrunedReplay_CommitOnceAndReturnOriginalReceipt()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await SeedAsync(database);
        Guid itemId;
        await using (var setup = database.Open())
        {
            var order = await new DeliveryMealService(setup, TimeProvider.System, new CurrentUser(seed.Owner)).CreateAsync(OrderRequest("Le Grill", 75m, [new("Sushi", 5m, FoodMeasurementType.Piece, CarbsPerUnit: 15m)]), CancellationToken.None);
            itemId = order.Items![0].Id;
        }
        var gate = new ConcurrentSave();
        await using var first = database.Open(gate);
        await using var second = database.Open(gate);
        var requests = new[] {
            new MealAssistantRequest(seed.ConversationId, "Add 3 sushi", Guid.NewGuid(), new(itemId, 3m, FoodMeasurementType.Piece, "DeliveryMealItem"), 1),
            new MealAssistantRequest(seed.ConversationId, "Add 2 sushi", Guid.NewGuid(), new(itemId, 2m, FoodMeasurementType.Piece, "DeliveryMealItem"), 1) };
        var results = await Task.WhenAll(SendOrConflict(Service(first, seed.Owner), requests[0]), SendOrConflict(Service(second, seed.Owner), requests[1]));
        Assert.Single(results, result => result.Response is not null); Assert.Single(results, result => result.Error is not null);
        var index = results[0].Response is not null ? 0 : 1;
        var winner = results[index].Response!;
        Assert.Equal(2, winner.Draft?.Items.Count); Assert.Equal(2, winner.DraftVersion);
        await using (var prune = database.Open())
        {
            prune.AssistantMessages.RemoveRange(await prune.AssistantMessages.Where(message => message.ConversationId == seed.ConversationId).ToListAsync());
            var item = await prune.DeliveryMealItems.AsNoTracking().SingleAsync(item => item.Id == itemId);
            await new DeliveryMealService(prune, TimeProvider.System, new CurrentUser(seed.Owner)).UpdateAsync(item.DeliveryMealId,
                OrderRequest("Le Grill", 495m, [new("Sushi", 5m, FoodMeasurementType.Piece, CarbsPerUnit: 99m, Id: itemId)]) with { ExpectedRevision = 0, AcceptCalculatedTotal = true }, CancellationToken.None);
        }
        await using var verify = database.Open();
        var replay = await Service(verify, seed.Owner).SendAsync(requests[index], CancellationToken.None);
        Assert.True(replay.Replayed); Assert.Equal(winner.Draft!.TotalCarbs, replay.Draft?.TotalCarbs);
        Assert.Empty(await verify.AssistantMessages.Where(message => message.ConversationId == seed.ConversationId).ToListAsync());
        var conflict = await Assert.ThrowsAsync<AssistantConflictException>(() => Service(verify, seed.Owner).SendAsync(requests[index] with { Message = "Different payload" }, CancellationToken.None));
        Assert.Equal("assistant_request_reused", conflict.Code); Assert.Equal(2, await verify.AssistantRequestReceipts.CountAsync());
    }

    [AssistantPostgresFact]
    public async Task Acceptance_PrecisionInputs_CannotCreateSnapshotsThatDriftOnUnchangedRepeat()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var factory = new StartupFactory(database.ConnectionString, new AcceptanceModel());
        using var client = await AcceptanceLogin(factory);
        var food = await Post<FoodItemDto>(client, "/api/foods", FoodRequest("Portion", FoodMeasurementType.Portion, 20m));
        using var quantity = await client.PostAsJsonAsync("/api/meals", MealRequest([new(food.Id, 0.125m)]), AcceptanceJson);
        using var snapshot = await client.PostAsJsonAsync("/api/meals", MealRequest([new(Guid.Empty, 150m, MeasurementType: FoodMeasurementType.Grams, FoodNameSnapshot: "Raw snapshot", CarbsPer100gSnapshot: 20.125m)]), AcceptanceJson);
        using var reference = await client.PostAsJsonAsync("/api/foods", FoodRequest("Too precise", FoodMeasurementType.Grams, 23.375m), AcceptanceJson);
        var diagnostic = "";
        if (quantity.IsSuccessStatusCode)
        {
            var created = (await quantity.Content.ReadFromJsonAsync<MealDetailDto>(AcceptanceJson))!;
            var stored = await Get<MealDetailDto>(client, $"/api/meals/{created.Id}");
            var repeated = await Post<MealCalculationDto>(client, "/api/meals/calculate", new CalculateMealRequest(MealType.Lunch, 5.5m, Repeat(stored)));
            diagnostic = $"Stored quantity {stored.Items[0].Quantity}, saved carbs {stored.TotalCarbs}, unchanged repeat {repeated.TotalCarbs}.";
        }
        Assert.True(new[] { quantity.StatusCode, snapshot.StatusCode, reference.StatusCode }.All(status => status == HttpStatusCode.BadRequest),
            $"Expected 400 for excess precision: quantity={quantity.StatusCode}, snapshot={snapshot.StatusCode}, food={reference.StatusCode}. {diagnostic}");
        using var validation = JsonDocument.Parse(await quantity.Content.ReadAsStringAsync());
        Assert.Equal(400, validation.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Validation failed", validation.RootElement.GetProperty("title").GetString());
        Assert.Contains("two decimal places", validation.RootElement.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(validation.RootElement.GetProperty("traceId").GetString()));
        Assert.Empty(await Get<List<MealSummaryDto>>(client, "/api/meals"));
        var valid = await Post<MealDetailDto>(client, "/api/meals", MealRequest([new(food.Id, 0.25m)]));
        Assert.Equal(5m, valid.TotalCarbs);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/meals/calculate", new CalculateMealRequest(MealType.Lunch, 5.5m, [new(food.Id, 0.125m)]), AcceptanceJson)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/meals/{valid.Id}/items", new AddMealItemsRequest([new(food.Id, 0.125m)]), AcceptanceJson)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/meals/{valid.Id}/items/{valid.Items[0].Id}", new UpdateMealItemRequest(0.125m), AcceptanceJson)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/foods/{food.Id}", FoodRequest("Portion", FoodMeasurementType.Portion, 20.125m), AcceptanceJson)).StatusCode);
        var kept = await Get<MealDetailDto>(client, $"/api/meals/{valid.Id}");
        Assert.Single(kept.Items); Assert.Equal(0.25m, kept.Items[0].Quantity); Assert.Equal(5m, kept.TotalCarbs);
        Assert.Equal(5m, (await Post<MealCalculationDto>(client, "/api/meals/calculate", new CalculateMealRequest(MealType.Lunch, 5.5m, Repeat(kept)))).TotalCarbs);
    }

    private static CreateMealRequest MealRequest(IReadOnlyList<MealItemInputDto> items, decimal adjustment = 0m) => new(MealType.Lunch, null, 5.5m, null, "Synthetic acceptance fixture", items, CarbAdjustment: adjustment);
    private static UpsertFoodItemRequest FoodRequest(string name, FoodMeasurementType unit, decimal carbs) => new(name, unit, unit == FoodMeasurementType.Grams ? carbs : null, unit == FoodMeasurementType.Grams ? null : carbs, 0m, 0m, 0m, false);
    private static UpsertDeliveryMealRequest OrderRequest(string place, decimal carbs, IReadOnlyList<DeliveryMealItemInput>? items) => new(place, "Acceptance order", "Original description", carbs, null, null, ResultRating.Unknown, "", null, false, items);
    private static MealItemInputDto[] Inputs(IEnumerable<CalculatedMealItemDto> items) => items.Select(item => new MealItemInputDto(item.FoodItemId, item.Quantity, CarbOverride: item.CarbOverride, SourceMealItemId: item.SourceMealItemId, SourceDeliveryMealId: item.SourceDeliveryMealId, SourceDeliveryMealItemId: item.SourceDeliveryMealItemId)).ToArray();
    private static MealItemInputDto[] Repeat(MealDetailDto meal) => meal.Items.Select(item => new MealItemInputDto(item.FoodItemId, item.Quantity, CarbOverride: item.CarbOverride, SourceMealItemId: item.Id)).ToArray();
    private static async Task<HttpClient> AcceptanceLogin(StartupFactory factory)
    {
        var client = factory.CreateClient(); var username = "accept-" + Guid.NewGuid().ToString("N")[..14];
        await Post<AuthUserDto>(client, "/api/auth/register", new { username, password = "acceptance-only-password" });
        var token = await Post<AuthTokenDto>(client, "/api/auth/login", new { username, password = "acceptance-only-password" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken); return client;
    }
    private static Task<T> Post<T>(HttpClient client, string route, object body) => Send<T>(client, HttpMethod.Post, route, body);
    private static async Task<T> Send<T>(HttpClient client, HttpMethod method, string route, object body)
    {
        using var request = new HttpRequestMessage(method, route) { Content = JsonContent.Create(body, options: AcceptanceJson) };
        using var response = await client.SendAsync(request, CancellationToken.None);
        Assert.True(response.IsSuccessStatusCode, $"{route}: {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(AcceptanceJson, CancellationToken.None))!;
    }
    private static async Task<T> Get<T>(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route, CancellationToken.None);
        Assert.True(response.IsSuccessStatusCode, $"{route}: {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(AcceptanceJson, CancellationToken.None))!;
    }
    private sealed class AcceptanceModel : ILlmClient
    {
        public int Calls { get; private set; }
        public Queue<AssistantSearch> Plans { get; } = new();
        public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken)
        {
            Calls++;
            if (!Plans.TryDequeue(out var plan)) throw new InvalidOperationException("Unexpected interpretation: acceptance tests never use a paid provider.");
            return Task.FromResult(plan);
        }
    }
}
