using System.Net;
using System.Net.Http.Json;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Tests;

public partial class AssistantPostgreSqlSafetyTests
{
    [AssistantPostgresFact]
    public async Task PartialMeal_ApiLookupAssembleResolveReplayAndCalculator_PreserveIndependentSources()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var factory = new StartupFactory(database.ConnectionString, new UnexpectedModel());
        using var client = await AcceptanceLogin(factory);
        var order = await Post<DeliveryMealDto>(client, "/api/delivery-meals", OrderRequest("Le Gril", 135m,
            [new("Калифорния", 1m, FoodMeasurementType.Portion, CarbsPerUnit: 60m, CarbOverride: 60m),
             new("Суши", 5m, FoodMeasurementType.Portion, CarbsPerUnit: 15m)]) with { DishName = "Суши", PortionDescription = null });
        var lookup = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", new MealAssistantRequest(null, "сколько брала на калифорнию", Guid.NewGuid()));
        Assert.Equal(60m, Assert.Single(lookup.Knowledge!.Items!).EffectiveCarbs); Assert.Null(lookup.Draft);
        var request = new MealAssistantRequest(lookup.ConversationId, "хочу съесть калифорнию и 120 жареной картошки", Guid.NewGuid(), ExpectedDraftVersion: 0);
        var partial = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", request);
        Assert.Equal("PartiallyCompleted", partial.ResultStatus); Assert.Equal(60m, partial.Draft!.TotalCarbs);
        var unresolved = Assert.Single(partial.Draft.Unresolved!);
        Assert.Equal(120m, unresolved.Query.Quantity); Assert.True(unresolved.Query.UnitInferred);
        Assert.Equal("120 жареной картошки", unresolved.OriginalText);
        Assert.True((await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", request)).Replayed);
        var reopened = await Get<List<AssistantTurn>>(client, $"/api/meal-assistant/conversations/{lookup.ConversationId}");
        Assert.Equal(unresolved.Id, Assert.Single(reopened.Last().Answer.Draft!.Unresolved!).Id);
        var resolve = new MealAssistantRequest(lookup.ConversationId, "Мои углеводы для картошки", Guid.NewGuid(), ExpectedDraftVersion: partial.DraftVersion,
            Resolution: new(unresolved.Id, "manual", Quantity: 120m, Unit: FoodMeasurementType.Grams, Carbs: 25m));
        var complete = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", resolve);
        Assert.Equal(85m, complete.Draft!.TotalCarbs); Assert.Empty(complete.Draft.Unresolved!);
        var replay = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", resolve);
        Assert.True(replay.Replayed); Assert.Equal(2, replay.Draft!.Items.Count);
        using var tampered = await client.PostAsJsonAsync("/api/meal-assistant/messages", resolve with { Resolution = resolve.Resolution! with { Carbs = 30m } }, AcceptanceJson);
        Assert.Equal(HttpStatusCode.Conflict, tampered.StatusCode);
        var prepared = await Get<List<CalculatedMealItemDto>>(client, $"/api/meal-assistant/conversations/{lookup.ConversationId}/calculator-draft?expectedDraftVersion={complete.DraftVersion}");
        Assert.Equal(order.Items![0].Id, prepared[0].SourceDeliveryMealItemId);
        Assert.Equal(120m, prepared[1].Quantity); Assert.Equal(25m, prepared[1].CarbOverride);
        Assert.Equal(CarbReferenceSource.Manual, prepared[1].Provenance!.ReferenceSource);
        var inputs = prepared.Select(item => new MealItemInputDto(item.FoodItemId, item.Quantity, MeasurementType: item.MeasurementType,
            FoodNameSnapshot: item.FoodName, CarbsPer100gSnapshot: item.CarbsPer100g, CarbsPerUnitSnapshot: item.CarbsPerUnit,
            CarbOverride: item.CarbOverride, SourceDeliveryMealItemId: item.SourceDeliveryMealItemId)).ToArray();
        var calculation = await Post<MealCalculationDto>(client, "/api/meals/calculate", new CalculateMealRequest(MealType.Lunch, 5.5m, inputs));
        Assert.Equal(85m, calculation.TotalCarbs);
        var saved = await Post<MealDetailDto>(client, "/api/meals", MealRequest(inputs));
        Assert.Equal(85m, saved.TotalCarbs); Assert.Null(saved.ConfirmedBolus); Assert.Equal(25m, saved.Items.Single(item => item.Quantity == 120m).CarbOverride);
        Assert.Equal(0, (await Get<PaginatedResult<FoodItemDto>>(client, "/api/foods")).TotalCount);
        Assert.Equal(135m, (await Get<DeliveryMealDto>(client, $"/api/delivery-meals/{order.Id}")).Carbs);
        await using var db = database.Open();
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
