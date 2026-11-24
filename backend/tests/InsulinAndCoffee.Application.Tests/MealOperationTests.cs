using System.Text.Json;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsulinAndCoffee.Application.Tests;

public class MealOperationTests
{
    private static readonly Guid Owner = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly CancellationToken Token = CancellationToken.None;
    private static AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static AssistantDraftBuilder Builder(AppDbContext db) => new(db, new UserContext(), new MealCalculationService(db, new UserContext()));
    private static MealAssistantService Service(AppDbContext db) => new(db, new UserContext(), new NoModel(), new MealHistorySearchService(db, new UserContext()),
        TimeProvider.System, NullLogger<MealAssistantService>.Instance, Builder(db), new FoodKnowledgeSearchService(db, new UserContext()));
    private static async Task<AssistantConversation> Seed(AppDbContext db, bool duplicates = true)
    {
        db.Users.Add(new User { Id = Owner, Username = "operations", NormalizedUsername = "OPERATIONS", CreatedAt = TimeProvider.System.GetUtcNow() });
        var foods = new[] { Food("Калифорния", 60m), Food("Пюре", 15m, FoodMeasurementType.Grams), Food("Жареная картошка", 29.17m, FoodMeasurementType.Grams) };
        db.FoodItems.AddRange(foods); await db.SaveChangesAsync(Token);
        var initial = await Builder(db).BuildAsync(foods.Select(food => new AssistantFoodQuery(food.Name,
            food.MeasurementType == FoodMeasurementType.Grams ? 120m : 1m, food.MeasurementType, FoodItemId: food.Id)).ToArray(), Token);
        Assert.NotNull(initial);
        var items = initial.Items.ToList();
        if (duplicates) items.Insert(1, items[0] with { DraftItemId = Guid.NewGuid() });
        var draft = initial with { Items = items, TotalCarbs = duplicates ? 173m : 113m };
        var conversation = new AssistantConversation { Id = Guid.NewGuid(), UserId = Owner, DraftVersion = 1, DraftJson = JsonSerializer.Serialize(draft) };
        db.AssistantConversations.Add(conversation); await db.SaveChangesAsync(Token);
        return conversation;
    }
    private static FoodItem Food(string name, decimal carbs, FoodMeasurementType unit = FoodMeasurementType.Portion) => new()
    { Id = Guid.NewGuid(), UserId = Owner, Name = name, MeasurementType = unit, CarbsPer100g = unit == FoodMeasurementType.Grams ? carbs : null,
        CarbsPerUnit = unit == FoodMeasurementType.Grams ? null : carbs, CarbReferenceSource = CarbReferenceSource.Manual };

    [Fact]
    public async Task CompoundEdit_AccidentalCaliforniaDuplicates_ConsolidatesTo78AndPersistsAcrossReloadAndRetry()
    {
        await using var db = Open(); var chat = await Seed(db);
        var request = new MealAssistantRequest(chat.Id, "убери жареную картошку и калифорнию оставь одну порцию", Guid.NewGuid(), ExpectedDraftVersion: 1);
        var service = Service(db); var answer = await service.SendAsync(request, Token);
        Assert.NotNull(answer.Draft); Assert.Equal(78m, answer.Draft.TotalCarbs); Assert.Equal(2, answer.Draft.Items.Count);
        Assert.Equal(1m, answer.Draft.Items[0].Quantity); Assert.Equal(60m, answer.Draft.Items[0].EffectiveCarbs);
        Assert.Equal("Пюре", answer.Draft.Items[1].FoodName); Assert.Equal(120m, answer.Draft.Items[1].Quantity); Assert.Equal(18m, answer.Draft.Items[1].EffectiveCarbs);
        Assert.Equal("Убрала жареную картошку. Калифорнию оставила одну порцию. Всего: 78 г углеводов.", answer.Message);
        Assert.Equal(answer.Message, answer.OperationSummary);
        Assert.All(answer.Draft.Items, item => { Assert.NotNull(item.DraftItemId); Assert.NotNull(item.SourceItemId); Assert.Equal(CarbReferenceSource.Manual, item.Provenance?.ReferenceSource); });
        db.ChangeTracker.Clear();
        var loaded = await Service(db).GetDraftStateAsync(chat.Id, Token);
        Assert.Equal(answer.Draft.Items, loaded.Draft?.Items); Assert.Equal(78m, loaded.Draft?.TotalCarbs);
        var turns = await Service(db).ReadAsync(chat.Id, Token); Assert.Equal(78m, turns[^1].Answer.Draft?.TotalCarbs);
        var prepared = await Service(db).PrepareDraftAsync(chat.Id, Token);
        Assert.Equal(2, prepared.Count); Assert.Equal(78m, prepared.Sum(item => item.EffectiveCarbs));
        var retry = await Service(db).SendAsync(request, Token); Assert.True(retry.Replayed); Assert.Equal(answer.DraftVersion, retry.DraftVersion);
        Assert.Single(await db.AssistantRequestReceipts.ToListAsync(Token));
        Assert.Equal(3, await db.FoodItems.CountAsync(Token)); Assert.Empty(await db.Meals.ToListAsync(Token));
    }

    [Fact]
    public async Task Add_SameOperationReplays_SeparateOperationIntentionallyAddsAnotherEntry()
    {
        await using var db = Open(); var chat = await Seed(db, false); var service = Service(db);
        var request = new MealAssistantRequest(chat.Id, "добавь Калифорнию", Guid.NewGuid(), ExpectedDraftVersion: 1);
        var first = await service.SendAsync(request, Token);
        var retry = await service.SendAsync(request, Token); Assert.True(retry.Replayed); Assert.Equal(first.Draft?.Items.Count, retry.Draft?.Items.Count);
        var next = await service.SendAsync(request with { RequestId = Guid.NewGuid(), ExpectedDraftVersion = first.DraftVersion }, Token);
        Assert.Equal(3, next.Draft?.Items.Count(item => item.FoodName == "Калифорния"));
        Assert.Equal(next.Draft?.Items.Count, next.Draft?.Items.Select(item => item.DraftItemId).Distinct().Count());
        Assert.Equal(2, await db.AssistantRequestReceipts.CountAsync(Token));
    }

    [Fact]
    public async Task Remove_ExactIdentity_IgnoresStaleIndexAndDeletedSourceAndIsRetrySafe()
    {
        await using var db = Open(); var chat = await Seed(db); var service = Service(db);
        var initial = await service.GetDraftStateAsync(chat.Id, Token); var item = initial.Draft!.Items[1];
        db.FoodItems.Remove(await db.FoodItems.SingleAsync(food => food.Id == item.FoodItemId, Token)); await db.SaveChangesAsync(Token);
        var request = new MealAssistantRequest(chat.Id, "Remove exact entry", Guid.NewGuid(), ExpectedDraftVersion: 1,
            Edit: new(999, "remove", DraftItemId: item.DraftItemId));
        var result = await service.SendAsync(request, Token);
        Assert.Equal(3, result.Draft?.Items.Count); Assert.DoesNotContain(result.Draft!.Items, candidate => candidate.DraftItemId == item.DraftItemId);
        Assert.Contains(result.Draft.Items, candidate => candidate.FoodName == item.FoodName);
        var replay = await service.SendAsync(request, Token); Assert.True(replay.Replayed);
        var again = await service.SendAsync(request with { RequestId = Guid.NewGuid(), ExpectedDraftVersion = result.DraftVersion }, Token);
        Assert.Equal(result.Draft.Items, again.Draft?.Items);
        Assert.DoesNotContain((await service.GetDraftStateAsync(chat.Id, Token)).Draft!.Items, candidate => candidate.DraftItemId == item.DraftItemId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetQuantity_DifferentSourcesOrOverrides_ClarifiesWithoutCollapsing(bool differentOverride)
    {
        await using var db = Open(); var chat = await Seed(db); var service = Service(db);
        var draft = (await service.GetDraftStateAsync(chat.Id, Token)).Draft!; var items = draft.Items.ToArray();
        if (differentOverride) items[1] = items[1] with { CarbOverride = 55m, EffectiveCarbs = 55m };
        else { var other = Food("Калифорния", 60m); db.FoodItems.Add(other); await db.SaveChangesAsync(Token); items[1] = items[1] with { FoodItemId = other.Id }; }
        chat.DraftJson = JsonSerializer.Serialize(draft with { Items = items }); await db.SaveChangesAsync(Token);
        var answer = await service.SendAsync(new(chat.Id, "калифорнию оставь одну порцию", Guid.NewGuid(), ExpectedDraftVersion: 1), Token);
        Assert.NotNull(answer.Draft?.EditFeedback); Assert.Contains("source", answer.Draft.EditFeedback); Assert.Contains("entry", answer.Draft.EditFeedback);
        Assert.Equal(4, answer.Draft.Items.Count);
    }

    [Fact]
    public async Task Pending_RemoveDoesNotRequireQuantity_ResolveTwiceCannotDuplicateOrResurrect()
    {
        await using var db = Open(); var chat = await Seed(db, false); var service = Service(db);
        var draft = (await service.GetDraftStateAsync(chat.Id, Token)).Draft!;
        var missing = new AssistantUnresolvedItem(Guid.NewGuid(), "Жареная картошка", new("Жареная картошка", null, null), AssistantResolutionReason.QuantityRequired, "Specify quantity", []);
        chat.DraftJson = JsonSerializer.Serialize(draft with { Items = draft.Items.Take(2).ToArray(), TotalCarbs = 78m, Issues = [missing.Detail], Unresolved = [missing] }); await db.SaveChangesAsync(Token);
        var removed = await service.SendAsync(new(chat.Id, "убери жареную картошку", Guid.NewGuid(), ExpectedDraftVersion: 1), Token);
        Assert.Empty(removed.Draft!.Unresolved!); Assert.Empty(removed.Draft.Issues); Assert.Equal(78m, removed.Draft.TotalCarbs);
        var addPending = await service.SendAsync(new(chat.Id, "добавь неизвестный гарнир", Guid.NewGuid(), ExpectedDraftVersion: removed.DraftVersion), Token);
        var unresolved = Assert.Single(addPending.Draft!.Unresolved!);
        var request = new MealAssistantRequest(chat.Id, "Use my carbs", Guid.NewGuid(), ExpectedDraftVersion: addPending.DraftVersion,
            Resolution: new(unresolved.Id, "manual", Quantity: 120m, Unit: FoodMeasurementType.Grams, Carbs: 18m));
        var resolved = await service.SendAsync(request, Token); var repeated = await service.SendAsync(request, Token);
        Assert.True(repeated.Replayed); Assert.Equal(3, resolved.Draft!.Items.Count); Assert.Empty(resolved.Draft.Unresolved!);
        Assert.Equal(unresolved.Id, resolved.Draft.Items[^1].DraftItemId);
        await Assert.ThrowsAsync<ValidationException>(() => service.SendAsync(request with { RequestId = Guid.NewGuid(), ExpectedDraftVersion = resolved.DraftVersion }, Token));
        Assert.Empty((await service.GetDraftStateAsync(chat.Id, Token)).Draft!.Unresolved!);
    }

    [Fact]
    public async Task MultipleRemovals_ClearAndStaleVersion_PreserveAtomicState()
    {
        await using var db = Open(); var chat = await Seed(db, false); var service = Service(db);
        var result = await service.SendAsync(new(chat.Id, "убери жареную картошку и убери пюре", Guid.NewGuid(), ExpectedDraftVersion: 1), Token);
        Assert.Single(result.Draft!.Items); Assert.Equal(60m, result.Draft.TotalCarbs);
        await Assert.ThrowsAsync<AssistantConflictException>(() => service.SendAsync(new(chat.Id, "добавь Калифорнию", Guid.NewGuid(), ExpectedDraftVersion: 1), Token));
        var clear = new MealAssistantRequest(chat.Id, "очисти приём пищи", Guid.NewGuid(), ExpectedDraftVersion: result.DraftVersion);
        var empty = await service.SendAsync(clear, Token); Assert.Empty(empty.Draft!.Items);
        Assert.True((await service.SendAsync(clear, Token)).Replayed);
    }

    [Fact]
    public async Task SetQuantity_AfterReconciliation_PreservesIdentityOverrideProvenanceAndScalesTotal()
    {
        await using var db = Open(); var chat = await Seed(db); var service = Service(db);
        var draft = (await service.GetDraftStateAsync(chat.Id, Token)).Draft!;
        var items = draft.Items.Select(item => item.FoodName == "Калифорния" ? item with { CarbOverride = 50m, EffectiveCarbs = 50m } : item).ToArray();
        chat.DraftJson = JsonSerializer.Serialize(draft with { Items = items, TotalCarbs = 153m }); await db.SaveChangesAsync(Token);
        var result = await service.SendAsync(new(chat.Id, "калифорнию оставь одну порцию", Guid.NewGuid(), ExpectedDraftVersion: 1), Token);
        var item = result.Draft!.Items[0]; Assert.Equal(50m, item.CarbOverride); Assert.Equal(items[0].DraftItemId, item.DraftItemId);
        var updated = await service.SendAsync(new(chat.Id, "Set exact entry", Guid.NewGuid(), ExpectedDraftVersion: result.DraftVersion,
            Edit: new(0, "set", 2m, item.DraftItemId)), Token);
        Assert.Equal(100m, updated.Draft!.Items[0].CarbOverride); Assert.Equal(120m, updated.Draft.Items[0].CalculatedCarbs);
        Assert.Equal(item.Provenance, updated.Draft.Items[0].Provenance); Assert.Equal(item.DraftItemId, updated.Draft.Items[0].DraftItemId);
    }

    [Fact]
    public async Task CompoundEdit_AmbiguousSecondAction_DoesNotPartiallyRemoveFirstTarget()
    {
        await using var db = Open(); var chat = await Seed(db); var service = Service(db);
        var draft = (await service.GetDraftStateAsync(chat.Id, Token)).Draft!;
        var other = Food("Калифорния", 75m); db.FoodItems.Add(other); await db.SaveChangesAsync(Token);
        chat.DraftJson = JsonSerializer.Serialize(draft with { Items = draft.Items.Select((item, index) => index == 1 ? item with { FoodItemId = other.Id } : item).ToArray() });
        await db.SaveChangesAsync(Token);
        var result = await service.SendAsync(new(chat.Id, "убери жареную картошку и калифорнию оставь одну порцию", Guid.NewGuid(), ExpectedDraftVersion: 1), Token);
        Assert.NotNull(result.Draft!.EditFeedback); Assert.Equal(4, result.Draft.Items.Count);
        Assert.Contains(result.Draft.Items, item => item.FoodName == "Жареная картошка");
        Assert.Null(result.OperationSummary);
    }

    [Fact]
    public async Task ResolvedQuantityEdit_MultiplePendingItems_DoesNotBlockOrResurrectRemovedPending()
    {
        await using var db = Open(); var chat = await Seed(db, false); var service = Service(db);
        var draft = (await service.GetDraftStateAsync(chat.Id, Token)).Draft!;
        var pending = new[] { "Суп", "Хлеб" }.Select(name => new AssistantUnresolvedItem(Guid.NewGuid(), name, new(name, null, null),
            AssistantResolutionReason.NoMatches, "No match " + name, [])).ToArray();
        chat.DraftJson = JsonSerializer.Serialize(draft with { Unresolved = pending, Issues = pending.Select(item => item.Detail).ToArray() }); await db.SaveChangesAsync(Token);
        var updated = await service.SendAsync(new(chat.Id, "калифорнию оставь одну порцию", Guid.NewGuid(), ExpectedDraftVersion: 1), Token);
        Assert.Equal(2, updated.Draft!.Unresolved!.Count);
        var remove = new MealAssistantRequest(chat.Id, "Remove unresolved", Guid.NewGuid(), ExpectedDraftVersion: updated.DraftVersion,
            Resolution: new(pending[0].Id, "remove", Quantity: -1m));
        var removed = await service.SendAsync(remove, Token); Assert.Single(removed.Draft!.Unresolved!);
        var changed = await service.SendAsync(new(chat.Id, "калифорнию оставь одну порцию", Guid.NewGuid(), ExpectedDraftVersion: removed.DraftVersion), Token);
        Assert.Single(changed.Draft!.Unresolved!); Assert.DoesNotContain(changed.Draft.Unresolved!, item => item.Id == pending[0].Id);
    }

    [Fact]
    public async Task ReplaceItem_Retry_ReplacesOnceAndKeepsUnrelatedEntries()
    {
        await using var db = Open(); var chat = await Seed(db, false); var user = new UserContext();
        var model = new ReplacementModel();
        var service = new MealAssistantService(db, user, model, new MealHistorySearchService(db, user), TimeProvider.System,
            NullLogger<MealAssistantService>.Instance, Builder(db), new FoodKnowledgeSearchService(db, user));
        var request = new MealAssistantRequest(chat.Id, "замени Калифорнию на пюре", Guid.NewGuid(), ExpectedDraftVersion: 1);
        var result = await service.SendAsync(request, Token);
        Assert.DoesNotContain(result.Draft!.Items, item => item.FoodName == "Калифорния");
        Assert.Equal(3, result.Draft.Items.Count); Assert.Equal(71m, result.Draft.TotalCarbs);
        Assert.True((await service.SendAsync(request, Token)).Replayed); Assert.Equal(1, model.Calls);
    }

    [Fact]
    public async Task ReplaceResolvedItem_UnresolvedOtherFoods_StaysIndependentAndRetainsUncertainty()
    {
        await using var db = Open(); var chat = await Seed(db, false); var builder = Builder(db);
        var draft = (await Service(db).GetDraftStateAsync(chat.Id, Token)).Draft!;
        var pending = new AssistantUnresolvedItem(Guid.NewGuid(), "Unknown", new("Unknown", null, null), AssistantResolutionReason.NoMatches, "No match", []);
        var result = await builder.UpdateAsync(new([new("Пюре", 120m, FoodMeasurementType.Grams)], "draft", "replace", "Калифорния"),
            draft with { Unresolved = [pending], Issues = [pending.Detail] }, Token);
        Assert.NotNull(result); Assert.Null(result.EditFeedback); Assert.Single(result.Unresolved!); Assert.Single(result.Issues);
        Assert.DoesNotContain(result.Items, item => item.FoodName == "Калифорния"); Assert.Equal(71m, result.TotalCarbs);
    }

    [Fact]
    public async Task LegacyIndexEdit_OptionalIdentity_DoesNotChangeExistingReceiptFingerprint()
    {
        await using var db = Open(); var chat = await Seed(db, false);
        var request = new MealAssistantRequest(chat.Id, "Remove legacy", Guid.NewGuid(), ExpectedDraftVersion: 1, Edit: new(0, "remove"));
        await Service(db).SendAsync(request, Token);
        var legacyPayload = $$"""{"ConversationId":"{{chat.Id}}","Message":"Remove legacy","ExpectedDraftVersion":1,"Selection":null}{"ItemIndex":0,"Action":"remove","Quantity":null}""";
        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(legacyPayload)));
        Assert.Equal(expected, (await db.AssistantRequestReceipts.SingleAsync(receipt => receipt.RequestId == request.RequestId, Token)).PayloadHash);
    }

    [Fact]
    public void HydrateLegacy_ClonedEntryIds_RepairsCollisionStablyWithoutChangingCarbs()
    {
        var item = new CalculatedMealItemDto(Guid.NewGuid(), "Калифорния", 1m, FoodMeasurementType.Portion, null, null, 60m, 60m, null, 60m, DraftItemId: Guid.NewGuid());
        var legacy = new AssistantDraft([item, item], [], 120m);
        var hydrated = AssistantDraftBuilder.WithIdentities(legacy);
        Assert.Equal(2, hydrated.Items.Select(row => row.DraftItemId).Distinct().Count());
        Assert.Equal(hydrated.Items, AssistantDraftBuilder.WithIdentities(legacy).Items);
        Assert.All(hydrated.Items, row => Assert.Equal(60m, row.EffectiveCarbs)); Assert.Equal(120m, hydrated.TotalCarbs);
    }

    private sealed class ReplacementModel : ILlmClient
    {
        public int Calls { get; private set; }
        public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(new AssistantSearch([new("Пюре", 120m, FoodMeasurementType.Grams)], "draft", "replace", "Калифорнию")); }
    }

    [Theory]
    [InlineData(0, 3, 2, 0)]
    [InlineData(10, 3, 2, 6.67)]
    [InlineData(1.005, 1, 1, 1.00)]
    public void ScaleTotal_UsesDecimalExistingRoundingAndZero(decimal total, decimal original, decimal quantity, decimal expected) =>
        Assert.Equal(expected, InsulinAndCoffee.Domain.Calculations.FoodCarbCalculator.ScaleTotal(total, original, quantity));

    [Theory]
    [InlineData(-1, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, -1)]
    public void ScaleTotal_InvalidData_Rejects(decimal total, decimal original, decimal quantity) =>
        Assert.Throws<InsulinAndCoffee.Domain.Exceptions.CarbCalculationException>(() => InsulinAndCoffee.Domain.Calculations.FoodCarbCalculator.ScaleTotal(total, original, quantity));

    private sealed class UserContext : ICurrentUser { public Guid UserId => Owner; }
    private sealed class NoModel : ILlmClient
    { public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken) => throw new InvalidOperationException("Meal operations must not invoke AI."); }
}
