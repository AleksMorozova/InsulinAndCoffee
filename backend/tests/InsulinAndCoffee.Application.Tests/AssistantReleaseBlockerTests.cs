using System.Text.Json;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Tests;

public partial class MealOperationTests
{
    [Theory]
    [InlineData("I want 2 sushi and 1 California")]
    [InlineData("Хочу 2 суши и 1 Калифорнию")]
    [InlineData("Додай 2 суші та 1 Каліфорнію")]
    [InlineData("Add 2 pieces sushi and one serving California")]
    public async Task QuantifiedLists_AssembleSeparateOwnedItemsWithoutProvider(string message)
    {
        await using var db = Open(); await Seed(db, false);
        db.FoodItems.Add(Food("Sushi", 15m, FoodMeasurementType.Piece)); await db.SaveChangesAsync(Token);
        var request = new MealAssistantRequest(null, message, Guid.NewGuid());
        var result = await Service(db).SendAsync(request, Token);
        Assert.Equal(2, result.Draft!.Items.Count); Assert.Empty(result.Draft.Unresolved!);
        Assert.Equal(2, result.Draft.Items.Select(item => item.DraftItemId).Distinct().Count());
        Assert.Collection(result.Draft.Items,
            item => { Assert.Equal("Sushi", item.FoodName); Assert.Equal(2m, item.Quantity); Assert.Equal(FoodMeasurementType.Piece, item.MeasurementType); Assert.Equal(30m, item.EffectiveCarbs); },
            item => { Assert.Equal("Калифорния", item.FoodName); Assert.Equal(1m, item.Quantity); Assert.Equal(60m, item.EffectiveCarbs); });
        Assert.Equal(90m, result.Draft.TotalCarbs);
        var replay = await Service(db).SendAsync(request, Token); Assert.True(replay.Replayed);
        Assert.Equal(result.Draft.Items, replay.Draft!.Items);
    }

    [Theory]
    [InlineData("Добавь Калифорнию и 120 г гречки")]
    [InlineData("Додай Каліфорнію та 120 г гречки")]
    [InlineData("Add California and 120 g buckwheat")]
    public async Task MixedLists_PreservePerItemUnitsAndGramScaling(string message)
    {
        await using var db = Open(); await Seed(db, false);
        db.FoodItems.Add(Food("Buckwheat", 20m, FoodMeasurementType.Grams)); await db.SaveChangesAsync(Token);
        var result = await Service(db).SendAsync(new(null, message, Guid.NewGuid()), Token);
        Assert.Equal(2, result.Draft!.Items.Count); Assert.Empty(result.Draft.Unresolved!);
        Assert.Equal(84m, result.Draft.TotalCarbs);
        var grams = result.Draft.Items[1]; Assert.Equal(120m, grams.Quantity);
        Assert.Equal(FoodMeasurementType.Grams, grams.MeasurementType); Assert.Equal(24m, grams.EffectiveCarbs);
    }

    [Theory]
    [InlineData("Remove fried potatoes and set California to one serving")]
    [InlineData("Убери жареную картошку и калифорнию оставь одну порцию")]
    public async Task MultipleActions_AreAppliedAtomicallyWithoutBecomingFoodNames(string message)
    {
        await using var db = Open(); var chat = await Seed(db);
        var result = await Service(db).SendAsync(new(chat.Id, message, Guid.NewGuid(), ExpectedDraftVersion: 1), Token);
        Assert.Equal(78m, result.Draft!.TotalCarbs); Assert.Equal(2, result.Draft.Items.Count);
        Assert.DoesNotContain(result.Draft.Items, item => item.FoodName == "Жареная картошка");
        Assert.Single(result.Draft.Items, item => item.FoodName == "Калифорния" && item.Quantity == 1m);
        Assert.Equal(120m, result.Draft.Items.Single(item => item.FoodName == "Пюре").Quantity);
    }

    [Theory]
    [InlineData("Add 1 serving Mac and cheese and 120 g Rice")]
    [InlineData("I want 1 serving Mac and cheese and 120 g Rice")]
    public async Task SavedConjunctionName_IsNotSplitInsideAList(string message)
    {
        await using var db = Open(); await Seed(db, false);
        db.FoodItems.AddRange(Food("Mac and cheese", 25m), Food("Rice", 20m, FoodMeasurementType.Grams)); await db.SaveChangesAsync(Token);
        var result = await Service(db).SendAsync(new(null, message, Guid.NewGuid()), Token);
        Assert.Collection(result.Draft!.Items, item => Assert.Equal("Mac and cheese", item.FoodName), item => Assert.Equal("Rice", item.FoodName));
        Assert.Equal(49m, result.Draft.TotalCarbs); Assert.Empty(result.Draft.Unresolved!);
    }

    [Fact]
    public async Task QuotedUnknownConjunctionName_RemainsOnePendingClarification()
    {
        await using var db = Open(); await Seed(db, false);
        var result = await Service(db).SendAsync(new(null, "Add \"Beans and greens\"", Guid.NewGuid()), Token);
        Assert.Empty(result.Draft!.Items);
        Assert.Equal("Beans and greens", Assert.Single(result.Draft.Unresolved!).Query.Name);
    }

    [Fact]
    public async Task SavedConjunctionName_WithCompetingReferencesStillRequiresSelection()
    {
        await using var db = Open(); await Seed(db, false);
        db.FoodItems.AddRange(Food("Mac and cheese", 25m), Food("Mac and cheese", 30m)); await db.SaveChangesAsync(Token);
        var result = await Service(db).SendAsync(new(null, "Add 1 serving Mac and cheese", Guid.NewGuid()), Token);
        Assert.Empty(result.Draft!.Items);
        var pending = Assert.Single(result.Draft.Unresolved!);
        Assert.Equal(AssistantResolutionReason.Ambiguous, pending.Reason); Assert.Equal(2, pending.Candidates.Count);
    }

    [Fact]
    public async Task CalculatorExport_RequiresReviewedVersionAndPreservesValuesAndProvenance()
    {
        await using var db = Open(); var chat = await Seed(db, false); var service = Service(db);
        var state = await service.GetDraftStateAsync(chat.Id, Token);
        var draft = state.Draft!;
        var overridden = draft.Items[0] with { CarbOverride = 47m, EffectiveCarbs = 47m };
        draft = draft with { Items = [overridden, .. draft.Items.Skip(1)], TotalCarbs = draft.TotalCarbs - 13m };
        chat.DraftJson = JsonSerializer.Serialize(draft); await db.SaveChangesAsync(Token);
        await Assert.ThrowsAsync<ValidationException>(() => service.PrepareDraftAsync(chat.Id, null, Token));
        await Assert.ThrowsAsync<ValidationException>(() => service.PrepareDraftAsync(chat.Id, -1, Token));
        var prepared = await service.PrepareDraftAsync(chat.Id, 1, Token);
        Assert.Equal(47m, prepared[0].CarbOverride); Assert.Equal(47m, prepared[0].EffectiveCarbs);
        Assert.Equal(overridden.Provenance, prepared[0].Provenance);
        Assert.Equal(draft.Items.Select(item => item.Quantity), prepared.Select(item => item.Quantity));
        chat.DraftVersion++; await db.SaveChangesAsync(Token);
        var conflict = await Assert.ThrowsAsync<AssistantConflictException>(() => service.PrepareDraftAsync(chat.Id, 1, Token));
        Assert.Equal("draft_version_conflict", conflict.Code); Assert.Equal(2, conflict.CurrentDraftVersion);
        var refreshed = await service.GetDraftStateAsync(chat.Id, Token);
        Assert.Equal(prepared, await service.PrepareDraftAsync(chat.Id, refreshed.DraftVersion, Token));
        Assert.Equal(draft.TotalCarbs, refreshed.Draft!.TotalCarbs); Assert.Empty(await db.Meals.ToListAsync(Token));
    }
}
