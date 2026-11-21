using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Domain.Exceptions;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsulinAndCoffee.Application.Tests;

public class StructuredDeliveryMealTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly CancellationToken Token = CancellationToken.None;

    [Fact]
    public async Task CreateAsync_StructuredOrder_CalculatesItemsAndPreservesStableIdsOnEdit()
    {
        await using var db = Db();
        var service = Orders(db);
        var order = await service.CreateAsync(Request(135m, [new("California", 1m, FoodMeasurementType.Portion, CarbsPerUnit: 60m),
            new("Sushi", 5m, FoodMeasurementType.Piece, CarbsPerUnit: 15m)]), Token);
        Assert.Equal(135m, order.Carbs);
        Assert.Equal(2, order.Items?.Count);
        var sushi = Assert.IsType<DeliveryMealItemDto>(order.Items?.Last());
        Assert.Equal(75m, sushi.CalculatedCarbs);
        Assert.Equal(CarbReferenceSource.Manual, sushi.Provenance.ReferenceSource);
        Assert.Null(sushi.Provenance.SourceMealVerifiedAt);
        var reuse = await service.ReuseItemsAsync(order.Id, new([sushi.Id], order.Revision), Token);
        Assert.Equal(sushi.Id, Assert.Single(reuse).SourceDeliveryMealItemId);
        Assert.Equal(75m, reuse[0].EffectiveCarbs);
        var all = await service.CreateMealDraftFromDeliveryMealAsync(order.Id, Token);
        Assert.Equal(2, all.Items?.Count);
        Assert.Null(all.UsualInsulinUnits);
        var inputs = order.Items!.Select(item => Input(item) with { Quantity = item.Id == sushi.Id ? 3m : item.Quantity }).ToArray();
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(order.Id,
            Request(105m, inputs) with { ExpectedRevision = 0 }, Token));
        Assert.Equal(135m, (await service.GetByIdAsync(order.Id, Token)).Carbs);
        var edited = await service.UpdateAsync(order.Id, Request(105m, inputs) with { ExpectedRevision = 0, AcceptCalculatedTotal = true }, Token);
        Assert.Equal(sushi.Id, edited.Items?.Last().Id);
        Assert.Equal(45m, edited.Items?.Last().EffectiveCarbs);
        Assert.Equal(1, edited.Revision);
        await Assert.ThrowsAsync<DeliveryMealConflictException>(() => service.UpdateAsync(order.Id,
            Request(105m, inputs) with { ExpectedRevision = 0 }, Token));
    }

    [Theory]
    [InlineData(FoodMeasurementType.Grams, 150, 20, 30)]
    [InlineData(FoodMeasurementType.Portion, 0.5, 20, 10)]
    [InlineData(FoodMeasurementType.Piece, 3, 15, 45)]
    [InlineData(FoodMeasurementType.Portion, 1, 0, 0)]
    public async Task PreviewAsync_Units_UseExistingCarbMath(FoodMeasurementType unit, decimal quantity, decimal basis, decimal expected)
    {
        await using var db = Db();
        var item = new DeliveryMealItemInput("Item", quantity, unit,
            unit == FoodMeasurementType.Grams ? basis : null, unit == FoodMeasurementType.Grams ? null : basis);
        var preview = await Orders(db).PreviewAsync(new([item], 100m), Token);
        Assert.Equal(expected, preview.TotalCarbs);
        Assert.Equal(expected - 100m, preview.Difference);
        if (expected == 0m) Assert.Equal(0m, (await Orders(db).CreateAsync(Request(0m, [item]), Token)).Carbs);
    }

    [Fact]
    public async Task PreviewAsync_InvalidOrMissingReference_DoesNotInventZeroOrFractionalPieces()
    {
        await using var db = Db();
        foreach (var item in new[] { new DeliveryMealItemInput("Missing", 1m, FoodMeasurementType.Portion),
            new("Fraction", 1.5m, FoodMeasurementType.Piece, CarbsPerUnit: 15m),
            new("Negative", 1m, FoodMeasurementType.Grams, CarbsPer100g: -1m),
            new("Bad unit", 1m, (FoodMeasurementType)99, CarbsPerUnit: 1m) })
            await Assert.ThrowsAsync<ValidationException>(() => Orders(db).PreviewAsync(new([item]), Token));
    }

    [Fact]
    public async Task ReuseAsync_OverrideAndHistoricalSnapshot_SurviveSourceChanges()
    {
        await using var db = Db();
        db.DiabetesSettings.Add(DiabetesSettings.CreateDefaults(Guid.NewGuid(), Owner, TimeProvider.System.GetUtcNow()));
        await db.SaveChangesAsync(Token);
        var service = Orders(db);
        var order = await service.CreateAsync(Request(7m, [new("Rice", 150m, FoodMeasurementType.Grams, 20m, CarbOverride: 7m)]) with
            { UsualInsulinUnits = 99m, LastPreMealGlucose = 15m }, Token);
        var item = Assert.Single(order.Items!);
        var input = new MealItemInputDto(Guid.Empty, 150m, SourceDeliveryMealItemId: item.Id);
        var meal = await new MealService(db, TimeProvider.System, Calc(db), new CurrentUser(Owner)).CreateMealAsync(
            new(MealType.Lunch, null, 5.5m, null, null, [input], CarbAdjustment: -2m), Token);
        Assert.Equal(30m, meal.Items[0].CalculatedCarbs);
        Assert.Equal(7m, meal.Items[0].CarbOverride);
        Assert.Equal(5m, meal.TotalCarbs);
        Assert.Null(meal.ConfirmedBolus);
        Assert.Null(meal.CarbsVerifiedAt);
        Assert.Equal(5.5m, meal.PreMealGlucose);
        await service.UpdateAsync(order.Id, Request(60m, [Input(item) with { CarbsPer100g = 40m, CarbOverride = null }]) with
            { ExpectedRevision = order.Revision, AcceptCalculatedTotal = true }, Token);
        var historic = await new MealService(db, TimeProvider.System, Calc(db), new CurrentUser(Owner)).GetMealAsync(meal.Id, Token);
        Assert.Equal(20m, historic.Items[0].CarbsPer100gSnapshot);
        Assert.Equal(30m, historic.Items[0].CalculatedCarbs);
        Assert.Equal(7m, historic.Items[0].CarbOverride);
        Assert.Equal(-2m, historic.CarbAdjustment);
    }

    [Fact]
    public async Task Assistant_MixedSourcesAndQuantityEdits_AmbiguityAndReplayRemainSafe()
    {
        await using var db = Db();
        var food = new FoodItem { Id = Guid.NewGuid(), UserId = Owner, Name = "Rice", MeasurementType = FoodMeasurementType.Grams, CarbsPer100g = 20m, CarbReferenceSource = CarbReferenceSource.Manual };
        db.FoodItems.Add(food); await db.SaveChangesAsync(Token);
        var order = await Orders(db).CreateAsync(Request(75m, [new("Sushi", 5m, FoodMeasurementType.Piece, CarbsPerUnit: 15m)]), Token);
        var sushiId = order.Items![0].Id;
        var builder = new AssistantDraftBuilder(db, new CurrentUser(Owner), Calc(db));
        var mixed = Assert.IsType<AssistantDraft>(await builder.BuildAsync([new("Rice", 150m, FoodMeasurementType.Grams), new("Sushi", 3m, FoodMeasurementType.Piece, "Le Grill")], Token));
        Assert.Empty(mixed.Issues);
        Assert.Equal(75m, mixed.TotalCarbs);
        Assert.Equal(sushiId, mixed.Items[1].SourceDeliveryMealItemId);
        var changed = Assert.IsType<AssistantDraft>(await builder.UpdateAsync(new([new("Sushi", 2m, FoodMeasurementType.Piece)], "draft", "set", "Sushi"), mixed, Token));
        Assert.Equal(60m, changed.TotalCarbs);
        Assert.Equal(2, (await builder.PrepareAsync(changed.Items, Token, changed.Sources)).Count);
        await Orders(db).CreateAsync(Request(10m, [new("Sushi", 1m, FoodMeasurementType.Piece, CarbsPerUnit: 10m)]), Token);
        var ambiguous = Assert.IsType<AssistantDraft>(await builder.BuildAsync([new("Sushi", 1m, FoodMeasurementType.Piece)], Token));
        Assert.Empty(ambiguous.Items); Assert.NotEmpty(ambiguous.Issues);
        var assistant = new MealAssistantService(db, new CurrentUser(Owner), new NoModel(), new MealHistorySearchService(db, new CurrentUser(Owner)),
            TimeProvider.System, NullLogger<MealAssistantService>.Instance, builder, new FoodKnowledgeSearchService(db, new CurrentUser(Owner)));
        var request = new MealAssistantRequest(null, "Choose sushi", Guid.NewGuid(), new(sushiId, 3m, FoodMeasurementType.Piece, "DeliveryMealItem"));
        var answer = await assistant.SendAsync(request, Token);
        var replay = await assistant.SendAsync(request, Token);
        Assert.True(replay.Replayed); Assert.Equal(answer.ConversationId, replay.ConversationId);
        Assert.Equal(45m, replay.Draft?.TotalCarbs);
        Assert.Equal(1, await db.AssistantConversations.CountAsync(Token));
    }

    [Fact]
    public async Task ReuseAsync_ForeignIdsAndLegacyOrders_CannotBecomeIndividualItems()
    {
        await using var db = Db();
        var service = Orders(db);
        var order = await service.CreateAsync(Request(30m, [new("One", 1m, FoodMeasurementType.Portion, CarbsPerUnit: 30m)]), Token);
        var other = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(() => Orders(db, other).ReuseItemsAsync(order.Id, new([order.Items![0].Id], 0), Token));
        await Assert.ThrowsAsync<ValidationException>(() => Calc(db, other).CalculateItemsAsync([new(Guid.Empty, 1m, SourceDeliveryMealItemId: order.Items![0].Id)], null, null, Token));
        var legacy = await service.CreateAsync(Request(135m, null), Token);
        Assert.Empty(legacy.Items!);
        Assert.Null((await service.CreateMealDraftFromDeliveryMealAsync(legacy.Id, Token)).Items);
        await Assert.ThrowsAsync<ValidationException>(() => service.ReuseItemsAsync(legacy.Id, new([Guid.NewGuid()], 0), Token));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Request(1m, [new("Mismatch", 1m, FoodMeasurementType.Portion, CarbsPerUnit: 30m)]), Token));
    }

    [Fact]
    public async Task Assistant_DiscoveryAndOrdinalSelection_ResolveOnlyUnambiguousOwnedItems()
    {
        await using var db = Db();
        var order = await Orders(db).CreateAsync(Request(135m, [new("California", 1m, FoodMeasurementType.Portion, CarbsPerUnit: 60m), new("Sushi", 5m, FoodMeasurementType.Piece, CarbsPerUnit: 15m)]), Token);
        var user = new CurrentUser(Owner);
        var knowledge = new FoodKnowledgeSearchService(db, user);
        var found = await knowledge.SearchAsync([new("Sushi", null, null, "Le Grill")], Token);
        Assert.Equal(order.Items![1].Id, Assert.Single(found.Items!).Id);
        Assert.Empty((await new FoodKnowledgeSearchService(db, new CurrentUser(Guid.NewGuid())).SearchAsync([new("Sushi", null, null)], Token)).Items!);
        var builder = new AssistantDraftBuilder(db, user, Calc(db));
        var assistant = new MealAssistantService(db, user, new NoModel(), new MealHistorySearchService(db, user), TimeProvider.System,
            NullLogger<MealAssistantService>.Instance, builder, knowledge);
        var selected = await assistant.SendAsync(new(null, "Use only the second item from my previous order.", Guid.NewGuid()), Token);
        Assert.Equal(order.Items[1].Id, Assert.Single(selected.Draft!.Items).SourceDeliveryMealItemId);
        Assert.Equal(75m, selected.Draft.TotalCarbs);
        var whole = await assistant.SendAsync(new(null, "Whole saved order", Guid.NewGuid(), new(order.Id, Kind: "DeliveryMeal")), Token);
        Assert.Equal(2, whole.Draft?.Items.Count);
        Assert.Equal(135m, whole.Draft?.TotalCarbs);
        await Orders(db).CreateAsync(Request(20m, [new("Other", 1m, FoodMeasurementType.Portion, CarbsPerUnit: 20m)]) with { PlaceName = "Another cafe" }, Token);
        var unclear = await assistant.SendAsync(new(null, "Use only the second item from my previous order.", Guid.NewGuid()), Token);
        Assert.Null(unclear.Draft);
        Assert.Contains("Choose one saved order", unclear.Message);
    }

    [Fact]
    public async Task StructuredConversionAndLinkedProvenance_RequireAcceptanceAndKeepSourceSeparateFromOverride()
    {
        await using var db = Db();
        var service = Orders(db);
        var legacy = await service.CreateAsync(Request(135m, null), Token);
        var conversion = Request(60m, [new("California", 1m, FoodMeasurementType.Portion, CarbsPerUnit: 60m)]) with { ExpectedRevision = 0 };
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(legacy.Id, conversion, Token));
        Assert.Empty((await service.GetByIdAsync(legacy.Id, Token)).Items!);
        var converted = await service.UpdateAsync(legacy.Id, conversion with { AcceptCalculatedTotal = true }, Token);
        Assert.Equal(60m, converted.Carbs);
        Assert.Equal(legacy.PortionDescription, converted.PortionDescription);
        var food = new FoodItem { Id = Guid.NewGuid(), UserId = Owner, Name = "Reference", MeasurementType = FoodMeasurementType.Piece,
            CarbsPerUnit = 15m, CarbReferenceSource = CarbReferenceSource.AiEstimate };
        db.FoodItems.Add(food); await db.SaveChangesAsync(Token);
        var linked = await service.CreateAsync(Request(7m, [new("Reference", 2m, FoodMeasurementType.Piece, CarbsPerUnit: 15m, CarbOverride: 7m, FoodItemId: food.Id)]), Token);
        var item = Assert.Single(linked.Items!);
        Assert.Equal(30m, item.CalculatedCarbs); Assert.Equal(7m, item.EffectiveCarbs);
        Assert.Equal(CarbReferenceSource.AiEstimate, item.Provenance.ReferenceSource);
        Assert.Equal(CarbReferenceSource.Manual, linked.Provenance?.ReferenceSource);
        Assert.Null(item.Provenance.SourceMealVerifiedAt);
        var mixed = Assert.IsType<AssistantDraft>(await new AssistantDraftBuilder(db, new CurrentUser(Owner), Calc(db)).BuildAsync(
            [new("California", null, null, DeliveryMealItemId: converted.Items![0].Id), new("Reference", 3m, FoodMeasurementType.Piece, DeliveryMealItemId: item.Id)], Token));
        Assert.Equal(2, mixed.Items.Count); Assert.Equal(67m, mixed.TotalCarbs);
        Assert.Equal(45m, mixed.Items[1].CalculatedCarbs); Assert.Equal(7m, mixed.Items[1].CarbOverride);
        await Assert.ThrowsAsync<ValidationException>(() => Orders(db, Guid.NewGuid()).PreviewAsync(new([new("Foreign", 1m, FoodMeasurementType.Piece,
            CarbsPerUnit: 15m, FoodItemId: food.Id)]), Token));
    }

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static DeliveryMealService Orders(AppDbContext db, Guid? user = null) => new(db, TimeProvider.System, new CurrentUser(user ?? Owner), Calc(db, user));
    private static MealCalculationService Calc(AppDbContext db, Guid? user = null) => new(db, new CurrentUser(user ?? Owner));
    private static UpsertDeliveryMealRequest Request(decimal total, IReadOnlyList<DeliveryMealItemInput>? items) => new("Le Grill", "Test order", "Saved composition", total, null, null, ResultRating.Unknown, "", null, false, items);
    private static DeliveryMealItemInput Input(DeliveryMealItemDto item) => new(item.Name, item.Quantity, item.MeasurementType, item.CarbsPer100g, item.CarbsPerUnit, item.CarbOverride, item.Id, item.FoodItemId);
    private sealed class CurrentUser(Guid id) : ICurrentUser { public Guid UserId => id; }
    private sealed class NoModel : ILlmClient { public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken) => throw new InvalidOperationException("Direct selection must not call the LLM."); }
}
