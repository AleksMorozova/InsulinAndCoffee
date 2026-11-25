using System.Text.Json;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsulinAndCoffee.Application.Tests;

public partial class MealOperationTests
{
    [Fact]
    public async Task RepeatedAiExtraction_SingleSushiMention_CreatesOneStablePendingRequest()
    {
        await using var db = Open(); var chat = await Seed(db, false); var user = new UserContext();
        var model = new SushiExtraction();
        var service = new MealAssistantService(db, user, model, new MealHistorySearchService(db, user), TimeProvider.System,
            NullLogger<MealAssistantService>.Instance, Builder(db), new FoodKnowledgeSearchService(db, user));
        var request = new MealAssistantRequest(chat.Id, "Хочу суши", Guid.NewGuid(), ExpectedDraftVersion: 1);
        var answer = await service.SendAsync(request, Token);
        var pending = Assert.Single(answer.Draft!.Unresolved!);
        Assert.Equal(request.RequestId, pending.OperationId); Assert.Equal(pending.Id, pending.RequestedItemId);
        var replay = await service.SendAsync(request, Token); Assert.True(replay.Replayed);
        Assert.Equal(pending.Id, Assert.Single(replay.Draft!.Unresolved!).Id); Assert.Equal(1, model.Calls);
        var search = new MealAssistantRequest(chat.Id, "Search again", Guid.NewGuid(), ExpectedDraftVersion: answer.DraftVersion,
            Resolution: new(pending.Id, "search", SearchName: "  SUSHI  "));
        var searched = await service.SendAsync(search, Token); var stillPending = Assert.Single(searched.Draft!.Unresolved!);
        Assert.Equal(pending.Id, stillPending.Id); Assert.Equal(pending.OperationId, stillPending.OperationId);
        var resolved = await service.SendAsync(new(chat.Id, "My carbs", Guid.NewGuid(), ExpectedDraftVersion: searched.DraftVersion,
            Resolution: new(pending.Id, "manual", Quantity: 1m, Unit: FoodMeasurementType.Portion, Carbs: 75m)), Token);
        Assert.Empty(resolved.Draft!.Unresolved!); Assert.Single(resolved.Draft.Items, item => item.DraftItemId == pending.Id);
    }

    [Fact]
    public async Task IndependentlyRequestedSushi_CaseAndTransliteration_DoNotDiscardEitherPendingItem()
    {
        await using var db = Open(); var chat = await Seed(db, false); var service = Service(db);
        var first = await service.SendAsync(new(chat.Id, "добавь Суши", Guid.NewGuid(), ExpectedDraftVersion: 1), Token);
        var second = await service.SendAsync(new(chat.Id, "добавь   SUSHI", Guid.NewGuid(), ExpectedDraftVersion: first.DraftVersion), Token);
        Assert.Equal(2, second.Draft!.Unresolved!.Count);
        Assert.Equal(2, second.Draft.Unresolved.Select(item => item.RequestedItemId).Distinct().Count());
        Assert.Equal(2, second.Draft.Unresolved.Select(item => item.OperationId).Distinct().Count());
        var pending = second.Draft.Unresolved[0];
        var removed = await service.SendAsync(new(chat.Id, "Remove pending", Guid.NewGuid(), ExpectedDraftVersion: second.DraftVersion,
            Resolution: new(pending.Id, "remove")), Token);
        Assert.Single(removed.Draft!.Unresolved!); db.ChangeTracker.Clear();
        Assert.DoesNotContain((await Service(db).GetDraftStateAsync(chat.Id, Token)).Draft!.Unresolved!, item => item.Id == pending.Id);
    }

    [Fact]
    public async Task ExplicitRepeatedMentions_SameOperation_HaveDifferentRequestedItemIds()
    {
        await using var db = Open(); var chat = await Seed(db, false); var user = new UserContext(); var model = new SushiExtraction();
        var service = new MealAssistantService(db, user, model, new MealHistorySearchService(db, user), TimeProvider.System,
            NullLogger<MealAssistantService>.Instance, Builder(db), new FoodKnowledgeSearchService(db, user));
        var request = new MealAssistantRequest(chat.Id, "Хочу суши и суши", Guid.NewGuid(), ExpectedDraftVersion: 1);
        var answer = await service.SendAsync(request, Token);
        Assert.Equal(2, answer.Draft!.Unresolved!.Count);
        Assert.All(answer.Draft.Unresolved, item => Assert.Equal(request.RequestId, item.OperationId));
        Assert.Equal(2, answer.Draft.Unresolved.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public async Task ExplicitExtraAndRemoveOneAndAll_UseStoredCompatibleUnitWithoutAi()
    {
        await using var db = Open(); var chat = await Seed(db, false); var service = Service(db);
        var added = await service.SendAsync(new(chat.Id, "Добавь ещё одну Калифорнию", Guid.NewGuid(), ExpectedDraftVersion: 1), Token);
        Assert.Equal(2m, added.Draft!.Items[0].Quantity); Assert.Equal(120m, added.Draft.Items[0].EffectiveCarbs);
        var less = await service.SendAsync(new(chat.Id, "Убери одну Калифорнию", Guid.NewGuid(), ExpectedDraftVersion: added.DraftVersion), Token);
        Assert.Equal(1m, less.Draft!.Items[0].Quantity); Assert.Equal(60m, less.Draft.Items[0].EffectiveCarbs);
        var removed = await service.SendAsync(new(chat.Id, "Убери все Калифорнии", Guid.NewGuid(), ExpectedDraftVersion: less.DraftVersion), Token);
        Assert.DoesNotContain(removed.Draft!.Items, item => item.FoodName == "Калифорния"); Assert.Equal(53m, removed.Draft.TotalCarbs);
    }

    [Fact]
    public async Task ExplicitRemoveOne_CompatibleSeparateEntries_RemovesOnlyOneServing()
    {
        await using var db = Open(); var chat = await Seed(db); var service = Service(db);
        var answer = await service.SendAsync(new(chat.Id, "Убери одну Калифорнию", Guid.NewGuid(), ExpectedDraftVersion: 1), Token);
        Assert.Single(answer.Draft!.Items, item => item.FoodName == "Калифорния"); Assert.Equal(113m, answer.Draft.TotalCarbs);
    }

    [Fact]
    public async Task ExplicitTwoShawarmaServings_PreservesRequestedQuantityWithoutAi()
    {
        await using var db = Open(); var chat = await Seed(db, false);
        db.FoodItems.Add(Food("Шаурма", 90m)); await db.SaveChangesAsync(Token);
        var result = await Service(db).SendAsync(new(chat.Id, "Хочу две порции шаурмы", Guid.NewGuid(), ExpectedDraftVersion: 1), Token);
        var shawarma = Assert.Single(result.Draft!.Items, item => item.FoodName == "Шаурма");
        Assert.Equal(2m, shawarma.Quantity); Assert.Equal(180m, shawarma.EffectiveCarbs);
    }

    [Fact]
    public async Task ProviderCannotInventRemoveAllIntent_PreservesExistingMeal()
    {
        await using var db = Open(); var chat = await Seed(db); var user = new UserContext();
        var service = new MealAssistantService(db, user, new InventedRemoval(), new MealHistorySearchService(db, user), TimeProvider.System,
            NullLogger<MealAssistantService>.Instance, Builder(db), new FoodKnowledgeSearchService(db, user));
        await Assert.ThrowsAsync<ValidationException>(() => service.SendAsync(new(chat.Id, "Explain my plan", Guid.NewGuid(), ExpectedDraftVersion: 1), Token));
        Assert.Equal(4, (await service.GetDraftStateAsync(chat.Id, Token)).Draft!.Items.Count);
    }

    [Fact]
    public async Task ExplicitCombine_PreservesTotalProvenanceSourceAndCalculatorValuesAcrossReload()
    {
        await using var db = Open(); var chat = await Seed(db); var service = Service(db);
        var before = (await service.GetDraftStateAsync(chat.Id, Token)).Draft!;
        var group = Assert.Single(before.DuplicateGroups!); Assert.True(group.CanCombine);
        Assert.Equal(4, before.Items.Count); // Detection never combines automatically on load.
        var request = new MealAssistantRequest(chat.Id, "Combine quantities", Guid.NewGuid(), ExpectedDraftVersion: 1,
            Edit: new(0, "combine", DraftItemId: group.DraftItemIds[0]));
        var result = await service.SendAsync(request, Token);
        Assert.Equal(3, result.Draft!.Items.Count); Assert.Equal(173m, result.Draft.TotalCarbs); Assert.Equal(2m, result.Draft.Items[0].Quantity);
        Assert.Equal(before.Items[0].Provenance, result.Draft.Items[0].Provenance); Assert.Equal(before.Items[0].SourceItemId, result.Draft.Items[0].SourceItemId);
        Assert.True((await service.SendAsync(request, Token)).Replayed);
        db.ChangeTracker.Clear(); var loaded = await Service(db).GetDraftStateAsync(chat.Id, Token);
        Assert.Equal(3, loaded.Draft!.Items.Count); Assert.Empty(loaded.Draft.DuplicateGroups!);
        var calculator = await Service(db).PrepareDraftAsync(chat.Id, Token); Assert.Equal(173m, calculator.Sum(item => item.EffectiveCarbs));
        Assert.Equal(60m, (await db.FoodItems.SingleAsync(food => food.Name == "Калифорния", Token)).CarbsPerUnit);
    }

    [Fact]
    public async Task KeepSeparate_PersistsDecisionWithoutChangingQuantitiesOrTotals()
    {
        await using var db = Open(); var chat = await Seed(db); var service = Service(db);
        var before = (await service.GetDraftStateAsync(chat.Id, Token)).Draft!;
        var answer = await service.SendAsync(new(chat.Id, "Keep separate", Guid.NewGuid(), ExpectedDraftVersion: 1,
            Edit: new(0, "keepSeparate", DraftItemId: before.Items[0].DraftItemId)), Token);
        Assert.Equal(before.Items, answer.Draft!.Items); Assert.Empty(answer.Draft.DuplicateGroups!);
        db.ChangeTracker.Clear(); Assert.Empty((await Service(db).GetDraftStateAsync(chat.Id, Token)).Draft!.DuplicateGroups!);
    }

    [Fact]
    public async Task ExplicitCombine_EqualConfirmedOverrides_PreservesTheirSumInsteadOfReplacingWithReferenceCarbs()
    {
        await using var db = Open(); var chat = await Seed(db); var service = Service(db);
        var before = (await service.GetDraftStateAsync(chat.Id, Token)).Draft!;
        var items = before.Items.Select(item => item.FoodName == "Калифорния" ? item with { CarbOverride = 50m, EffectiveCarbs = 50m } : item).ToArray();
        chat.DraftJson = JsonSerializer.Serialize(before with { Items = items, TotalCarbs = 153m }); await db.SaveChangesAsync(Token);
        var result = await service.SendAsync(new(chat.Id, "Combine", Guid.NewGuid(), ExpectedDraftVersion: 1,
            Edit: new(0, "combine", DraftItemId: items[0].DraftItemId)), Token);
        var combined = result.Draft!.Items[0]; Assert.Equal(100m, combined.CarbOverride); Assert.Equal(100m, combined.EffectiveCarbs);
        Assert.Equal(120m, combined.CalculatedCarbs); Assert.Equal(153m, result.Draft.TotalCarbs); Assert.Equal(items[0].Provenance, combined.Provenance);
        Assert.Equal(60m, (await db.FoodItems.SingleAsync(food => food.Name == "Калифорния", Token)).CarbsPerUnit);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConflictingOverrideOrUnit_CannotCombineOrChangeOnRead(bool overrideConflict)
    {
        await using var db = Open(); var chat = await Seed(db); var service = Service(db);
        var before = (await service.GetDraftStateAsync(chat.Id, Token)).Draft!; var items = before.Items.ToArray();
        items[1] = overrideConflict ? items[1] with { CarbOverride = 50m, EffectiveCarbs = 50m }
            : items[1] with { MeasurementType = FoodMeasurementType.Grams, Quantity = 100m, CarbsPer100g = 60m, CarbsPerUnit = null };
        chat.DraftJson = JsonSerializer.Serialize(before with { Items = items, TotalCarbs = items.Sum(item => item.EffectiveCarbs) }); await db.SaveChangesAsync(Token);
        var read = await service.GetDraftStateAsync(chat.Id, Token); Assert.False(Assert.Single(read.Draft!.DuplicateGroups!).CanCombine);
        await Assert.ThrowsAsync<ValidationException>(() => service.SendAsync(new(chat.Id, "Combine", Guid.NewGuid(), ExpectedDraftVersion: 1,
            Edit: new(0, "combine", DraftItemId: items[0].DraftItemId)), Token));
        Assert.Equal(items, (await service.GetDraftStateAsync(chat.Id, Token)).Draft!.Items);
    }

    [Fact]
    public void SameGuidDifferentSourceTypes_AreNotEquivalent()
    {
        var id = Guid.NewGuid();
        var first = new CalculatedMealItemDto(id, "Суши", 1m, FoodMeasurementType.Portion, null, null, 60m, 60m, null, 60m);
        var draft = AssistantDraftBuilder.WithIdentities(new([first, first with { SourceMealItemId = id }], [], 120m));
        Assert.Empty(draft.DuplicateGroups!); Assert.Equal(2, draft.Items.Count);
    }

    [Fact]
    public void CombineWouldChangeRoundingOrUnexplainedEffectiveCarbs_KeepsOriginalValues()
    {
        var id = Guid.NewGuid();
        var first = new CalculatedMealItemDto(id, "Saved food", 0.333m, FoodMeasurementType.Portion, null, null, 1m, 0.33m, null, 0.33m);
        var rounded = AssistantDraftBuilder.WithIdentities(new([first, first], [], 0.66m));
        Assert.False(Assert.Single(rounded.DuplicateGroups!).CanCombine); Assert.Equal(0.66m, rounded.TotalCarbs);
        var unexplained = AssistantDraftBuilder.WithIdentities(new([first, first with { EffectiveCarbs = 0.20m }], [], 0.53m));
        Assert.False(Assert.Single(unexplained.DuplicateGroups!).CanCombine); Assert.Equal(0.20m, unexplained.Items[1].EffectiveCarbs);
    }

    private sealed class SushiExtraction : ILlmClient
    {
        public int Calls { get; private set; }
        public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(new AssistantSearch([new("СУШИ", null, null), new(" sushi ", null, null)], "draft", "add")); }
    }
    private sealed class InventedRemoval : ILlmClient
    { public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken) =>
        Task.FromResult(new AssistantSearch([], "draft", "removeAll", "Калифорния")); }
}
