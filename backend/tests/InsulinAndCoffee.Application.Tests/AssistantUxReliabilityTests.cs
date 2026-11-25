using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace InsulinAndCoffee.Application.Tests;

public class AssistantUxReliabilityTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly CancellationToken Token = CancellationToken.None;
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static MealAssistantService Service(AppDbContext db, ILlmClient? model = null) => new(db, new UserContext(), model ?? new NoProvider(), new(db, new UserContext()), TimeProvider.System,
        NullLogger<MealAssistantService>.Instance, new(db, new UserContext(), new(db, new UserContext())), new(db, new UserContext()));
    private static async Task Seed(AppDbContext db)
    {
        db.FoodItems.Add(new() { Id = Guid.NewGuid(), UserId = Owner, Name = "Гречневая каша", MeasurementType = FoodMeasurementType.Grams, CarbsPer100g = 20m });
        db.DeliveryMeals.Add(new() { Id = Guid.NewGuid(), UserId = Owner, PlaceName = "Le Gril", DishName = "Суши", Carbs = 135m,
            Items = [new() { Id = Guid.NewGuid(), Name = "Калифорния", Quantity = 1m, MeasurementType = FoodMeasurementType.Portion, CarbsPerUnitSnapshot = 60m, CalculatedCarbs = 60m, CarbReferenceSource = CarbReferenceSource.Manual },
                new() { Id = Guid.NewGuid(), Name = "Суши", Quantity = 5m, MeasurementType = FoodMeasurementType.Portion, CarbsPerUnitSnapshot = 15m, CalculatedCarbs = 75m, DisplayOrder = 1 }] });
        await db.SaveChangesAsync(Token);
    }

    [Theory]
    [InlineData("Калифорния")]
    [InlineData("сколько брала на калифорнию")]
    [InlineData("Сколько углеводов я использовала для Калифорнии?")]
    [InlineData("найди Калифорнию")]
    [InlineData("сколько углеводов в моей Калифорнии")]
    public async Task Send_RussianCaliforniaLookup_UsesSavedItemWithoutMutatingDraft(string question)
    {
        await using var db = Db(); await Seed(db);
        var result = await Service(db).SendAsync(new(null, question, Guid.NewGuid()), Token);
        var item = Assert.Single(result.Knowledge!.Items!);
        Assert.Equal(60m, item.EffectiveCarbs); Assert.Equal("Le Gril", item.PlaceName);
        Assert.Null(result.Draft); Assert.Equal(0, result.DraftVersion); Assert.Equal("Completed", result.ResultStatus);
        Assert.Contains("сохранённых", result.Message); Assert.Empty(db.Meals);
    }

    [Theory]
    [InlineData("добавь 120 гречневой каши")]
    [InlineData("добавь 120 г гречки")]
    [InlineData("добавь 120 г гречневая каша")]
    [InlineData("120 г гречки")]
    [InlineData("120 гречневой каши")]
    public async Task Send_BuckwheatAliases_PreserveQuantityAndUseSavedBasis(string question)
    {
        await using var db = Db(); await Seed(db);
        var result = await Service(db).SendAsync(new(null, question, Guid.NewGuid()), Token);
        var item = Assert.Single(result.Draft!.Items); Assert.Equal(120m, item.Quantity); Assert.Equal(24m, item.EffectiveCarbs);
        Assert.Equal(FoodMeasurementType.Grams, item.MeasurementType); Assert.Empty(result.Draft.Issues);
    }

    [Fact]
    public async Task Send_MultipleRequestedItems_PreservesFoundSourcesAndReportsPartialInRussian()
    {
        await using var db = Db(); await Seed(db); var service = Service(db);
        var result = await service.SendAsync(new(null, "Добавь Калифорнию и 120 г гречки и драгонфрут", Guid.NewGuid()), Token);
        Assert.Equal(2, result.Draft!.Items.Count); Assert.Equal(84m, result.Draft.TotalCarbs);
        Assert.Equal("PartiallyCompleted", result.ResultStatus); Assert.Contains("частично", result.Message);
        Assert.Contains("драгонфрут", Assert.Single(result.Draft.Issues)); Assert.DoesNotContain("No saved", result.Draft.Issues[0]);
        var prepared = await service.PrepareDraftAsync(result.ConversationId, (await service.GetDraftStateAsync(result.ConversationId, Token)).DraftVersion, Token); Assert.Equal(2, prepared.Count);
        Assert.Equal(1, await db.FoodItems.CountAsync(row => row.UserId == Owner, Token));
    }

    [Fact]
    public async Task Send_StructuredEdits_AreVersionedRetrySafeAndDoNotChangeSource()
    {
        await using var db = Db(); await Seed(db); var service = Service(db);
        var initial = await service.SendAsync(new(null, "Добавь Калифорнию", Guid.NewGuid()), Token);
        var edit = new MealAssistantRequest(initial.ConversationId, "Изменить количество", Guid.NewGuid(), ExpectedDraftVersion: initial.DraftVersion, Edit: new(0, "set", 2m));
        var updated = await service.SendAsync(edit, Token); Assert.Equal(120m, updated.Draft!.TotalCarbs);
        var replay = await service.SendAsync(edit, Token); Assert.True(replay.Replayed); Assert.Equal(updated.DraftVersion, replay.DraftVersion);
        await Assert.ThrowsAsync<AssistantConflictException>(() => service.SendAsync(edit with { Edit = new(0, "set", 3m) }, Token));
        await Assert.ThrowsAsync<AssistantConflictException>(() => service.SendAsync(edit with { RequestId = Guid.NewGuid(), Edit = new(0, "remove") }, Token));
        var removed = await service.SendAsync(edit with { RequestId = Guid.NewGuid(), ExpectedDraftVersion = updated.DraftVersion, Edit = new(0, "remove") }, Token);
        Assert.Empty(removed.Draft!.Items); Assert.Equal(60m, (await db.DeliveryMealItems.SingleAsync(row => row.Name == "Калифорния", Token)).CalculatedCarbs);
        await service.RenameAsync(initial.ConversationId, "  Обед  ", Token);
        Assert.Equal("Обед", Assert.Single(await service.ListAsync(Token)).Title);
    }

    [Fact]
    public async Task Send_UkrainianLookupWithoutUniqueLetters_ReturnsUkrainianNoMatches()
    {
        await using var db = Db();
        var result = await Service(db).SendAsync(new(null, "знайди гречку", Guid.NewGuid()), Token);
        Assert.Equal("NoMatchesFound", result.ResultStatus); Assert.Contains("не знайдено", result.Message); Assert.Null(result.Draft);
    }

    [Theory]
    [InlineData("додай 120 г гречки")]
    [InlineData("add 120 g buckwheat")]
    public async Task Send_ConversationLanguage_KeepsNoMatchRepliesConsistent(string question)
    {
        await using var db = Db(); var result = await Service(db).SendAsync(new(null, question, Guid.NewGuid()), Token);
        Assert.Equal("NeedsClarification", result.ResultStatus);
        Assert.Contains(question.StartsWith("додай") ? "Уточни" : "Review", result.Message);
        if (question.StartsWith("додай")) Assert.DoesNotContain("No saved", result.Draft!.Issues[0]);
    }

    [Theory]
    [InlineData("добавь 150 грамм гречки", 30)]
    [InlineData("добавь 120 гречневой каши", 24)]
    [InlineData("добавь Калифорнию", 60)]
    [InlineData("хочу съесть Калифорнию", 60)]
    [InlineData("добавь одну порцию Калифорнии", 60)]
    public async Task ReportedRequests_ResolveSavedReferencesWithoutProvider(string message, decimal carbs)
    {
        await using var db = Db(); await Seed(db);
        var response = await Service(db).SendAsync(new(null, message, Guid.NewGuid()), Token);
        Assert.Equal(carbs, response.Draft!.TotalCarbs); Assert.Empty(response.Draft.Issues);
    }

    [Fact]
    public async Task PartialMeal_KeepsCaliforniaAndOriginalPotatoRequest_ThenResolvesWithoutRepeatingMeal()
    {
        await using var db = Db(); await Seed(db); var service = Service(db);
        var response = await service.SendAsync(new(null, "хочу съесть калифорнию и 120 жареной картошки", Guid.NewGuid()), Token);
        Assert.Equal("PartiallyCompleted", response.ResultStatus); Assert.Equal(60m, response.Draft!.TotalCarbs);
        Assert.Equal("Калифорния", Assert.Single(response.Draft.Items).FoodName);
        var missing = Assert.Single(response.Draft.Unresolved!);
        Assert.Equal("120 жареной картошки", missing.OriginalText); Assert.Equal(120m, missing.Query.Quantity);
        Assert.Equal(FoodMeasurementType.Grams, missing.Query.Unit); Assert.True(missing.Query.UnitInferred);
        Assert.Equal(AssistantResolutionReason.NoMatches, missing.Reason);
        var request = new MealAssistantRequest(response.ConversationId, "Уточнить картошку", Guid.NewGuid(), ExpectedDraftVersion: response.DraftVersion,
            Resolution: new(missing.Id, "manual", Quantity: 120m, Unit: FoodMeasurementType.Grams, Carbs: 25m));
        var resolved = await service.SendAsync(request, Token);
        Assert.Equal(85m, resolved.Draft!.TotalCarbs); Assert.Equal(2, resolved.Draft.Items.Count); Assert.Empty(resolved.Draft.Unresolved!);
        var manual = resolved.Draft.Items[1]; Assert.Equal(120m, manual.Quantity); Assert.Equal(25m, manual.CarbOverride);
        Assert.Equal(CarbReferenceSource.Manual, manual.Provenance!.ReferenceSource);
        Assert.Equal(25m, (await service.PrepareDraftAsync(response.ConversationId, (await service.GetDraftStateAsync(response.ConversationId, Token)).DraftVersion, Token))[1].EffectiveCarbs);
        var replay = await service.SendAsync(request, Token); Assert.True(replay.Replayed); Assert.Equal(85m, replay.Draft!.TotalCarbs);
        Assert.True((await service.SendAsync(request with { Resolution = request.Resolution! with { Quantity = 120.00m, Carbs = 25.00m } }, Token)).Replayed);
        Assert.Equal(2, (await service.GetDraftStateAsync(response.ConversationId, Token)).Draft!.Items.Count);
        Assert.Equal(1, await db.FoodItems.CountAsync(row => row.UserId == Owner, Token)); Assert.Empty(db.Meals);
        await Assert.ThrowsAsync<AssistantConflictException>(() => service.SendAsync(request with { Resolution = request.Resolution! with { Carbs = 30m } }, Token));
    }

    [Fact]
    public async Task PendingItems_CanBeSearchedSelectedAdjustedAndRemovedIndependently()
    {
        await using var db = Db(); await Seed(db); var service = Service(db);
        var first = await service.SendAsync(new(null, "добавь Калифорнию и 120 жареной картошки и неизвестный продукт", Guid.NewGuid()), Token);
        Assert.Equal(2, first.Draft!.Unresolved!.Count);
        var potato = first.Draft.Unresolved[0]; var other = first.Draft.Unresolved[1];
        var savedFood = new FoodItem { Id = Guid.NewGuid(), UserId = Owner, Name = "Жареная картошка", MeasurementType = FoodMeasurementType.Grams, CarbsPer100g = 20m };
        db.FoodItems.Add(savedFood); await db.SaveChangesAsync(Token);
        var found = await service.SendAsync(new(first.ConversationId, "Поискать картошку", Guid.NewGuid(), ExpectedDraftVersion: first.DraftVersion,
            Resolution: new(potato.Id, "search", Quantity: 150m)), Token);
        Assert.Equal(90m, found.Draft!.TotalCarbs); Assert.Equal(150m, found.Draft.Items[1].Quantity);
        Assert.Equal(other.Id, Assert.Single(found.Draft.Unresolved!).Id);
        var removed = await service.SendAsync(new(first.ConversationId, "Убрать неизвестное", Guid.NewGuid(), ExpectedDraftVersion: found.DraftVersion,
            Resolution: new(other.Id, "remove")), Token);
        Assert.Empty(removed.Draft!.Unresolved!); Assert.Equal(90m, removed.Draft.TotalCarbs);
        Assert.Equal("Completed", removed.ResultStatus);
    }

    [Fact]
    public async Task ExistingMeal_IsPreservedForProviderCreateAndExplicitAddFailures()
    {
        await using var db = Db(); await Seed(db); var service = Service(db);
        var original = await service.SendAsync(new(null, "добавь 150 грамм гречки", Guid.NewGuid()), Token);
        var partial = await service.SendAsync(new(original.ConversationId, "хочу съесть калифорнию и 120 жареной картошки", Guid.NewGuid(), ExpectedDraftVersion: original.DraftVersion), Token);
        Assert.Equal(90m, partial.Draft!.TotalCarbs); Assert.Equal(2, partial.Draft.Items.Count);
        var builder = new AssistantDraftBuilder(db, new UserContext(), new(db, new UserContext()));
        var providerCreate = await builder.UpdateAsync(new([new("another missing food", null, null)], "draft", "create"), partial.Draft, Token);
        Assert.Equal(90m, providerCreate!.TotalCarbs); Assert.Equal(2, providerCreate.Items.Count); Assert.Equal(2, providerCreate.Unresolved!.Count);
        var replaced = await builder.UpdateAsync(new([new("гречка", 100m, FoodMeasurementType.Grams)], "draft", "replaceMeal"), partial.Draft, Token);
        Assert.Single(replaced!.Items); Assert.Equal(20m, replaced.TotalCarbs);
    }

    [Fact]
    public async Task ClearMeal_RequiresExplicitRequestAndReturnsAccurateStatus()
    {
        await using var db = Db(); await Seed(db);
        var original = await Service(db).SendAsync(new(null, "добавь 150 грамм гречки", Guid.NewGuid()), Token);
        var service = Service(db, new ExtractedModel(new([], "draft", "clear")));
        foreach (var message in new[] { "Подбери ужин", "не очисти приём пищи", "do not clear my meal" })
            await Assert.ThrowsAsync<ValidationException>(() => service.SendAsync(new(original.ConversationId, message, Guid.NewGuid(), ExpectedDraftVersion: original.DraftVersion), Token));
        Assert.Equal(30m, (await service.GetDraftStateAsync(original.ConversationId, Token)).Draft!.TotalCarbs);
        var cleared = await service.SendAsync(new(original.ConversationId, "очисти приём пищи", Guid.NewGuid(), ExpectedDraftVersion: original.DraftVersion), Token);
        Assert.Empty(cleared.Draft!.Items); Assert.Equal("Completed", cleared.ResultStatus); Assert.Contains("очищен", cleared.Message);
    }

    [Fact]
    public async Task CompetingRestaurantItems_ExposeTypedChoicesAndProtectOwnershipAndOverrides()
    {
        await using var db = Db(); await Seed(db);
        db.DeliveryMeals.Add(new() { Id = Guid.NewGuid(), UserId = Owner, PlaceName = "Другой ресторан", DishName = "Калифорния",
            Items = [new() { Id = Guid.NewGuid(), Name = "Калифорния", Quantity = 1m, MeasurementType = FoodMeasurementType.Portion, CarbsPerUnitSnapshot = 95m, CalculatedCarbs = 95m }] });
        var california = await db.DeliveryMealItems.SingleAsync(item => item.Name == "Калифорния", Token);
        california.CarbOverride = 60m; await db.SaveChangesAsync(Token);
        var service = Service(db);
        var ambiguous = await service.SendAsync(new(null, "добавь Калифорнию", Guid.NewGuid()), Token);
        var missing = Assert.Single(ambiguous.Draft!.Unresolved!); Assert.Equal(AssistantResolutionReason.Ambiguous, missing.Reason);
        Assert.Equal(2, missing.Candidates.Count); Assert.All(missing.Candidates, choice => Assert.Equal(AssistantCandidateKind.DeliveryMealItem, choice.Kind));
        await Assert.ThrowsAsync<ValidationException>(() => service.SendAsync(new(ambiguous.ConversationId, "Выбрать недоступное", Guid.NewGuid(), ExpectedDraftVersion: ambiguous.DraftVersion,
            Resolution: new(missing.Id, "select", Guid.NewGuid(), "DeliveryMealItem")), Token));
        var resolved = await service.SendAsync(new(ambiguous.ConversationId, "Выбрать Le Gril", Guid.NewGuid(), ExpectedDraftVersion: ambiguous.DraftVersion,
            Resolution: new(missing.Id, "select", california.Id, "DeliveryMealItem")), Token);
        Assert.Equal(60m, resolved.Draft!.TotalCarbs); Assert.Equal(60m, resolved.Draft.Items[0].CarbOverride);
    }

    [Fact]
    public async Task SameNamedFoodChoices_ShowTheirDifferentSavedReferenceBases()
    {
        await using var db = Db();
        foreach (var carbs in new[] { 20m, 25m }) db.FoodItems.Add(new() { Id = Guid.NewGuid(), UserId = Owner, Name = "Test food", MeasurementType = FoodMeasurementType.Grams, CarbsPer100g = carbs });
        await db.SaveChangesAsync(Token);
        var result = await Service(db).SendAsync(new(null, "add 120 g Test food", Guid.NewGuid()), Token);
        var choices = Assert.Single(result.Draft!.Unresolved!).Candidates;
        Assert.Equal(2, choices.Count); Assert.Contains(choices, choice => choice.Context == "20 g / 100 g"); Assert.Contains(choices, choice => choice.Context == "25 g / 100 g");
    }

    [Fact]
    public async Task ProviderInventedRestaurant_DoesNotHideUniqueItem_ButExplicitPlaceIsRespected()
    {
        await using var db = Db(); await Seed(db);
        var model = new ExtractedModel(new([new("роллы калифорния", null, null, "Калифорния")], "draft", "create"));
        var response = await Service(db, model).SendAsync(new(null, "Подбери мой ужин", Guid.NewGuid()), Token);
        Assert.Equal(60m, response.Draft!.TotalCarbs); Assert.Single(response.Draft.Items);
        var explicitPlace = await Service(db, model).SendAsync(new(null, "Подбери роллы из Калифорния", Guid.NewGuid()), Token);
        Assert.Empty(explicitPlace.Draft!.Items); Assert.Equal(AssistantResolutionReason.NoMatches, Assert.Single(explicitPlace.Draft.Unresolved!).Reason);
        var differentPlace = await Service(db, model).SendAsync(new(null, "Подбери роллы из Le Gril", Guid.NewGuid()), Token);
        Assert.Empty(differentPlace.Draft!.Items);
    }

    [Fact]
    public async Task MatchingOrderTitle_IsNotMistakenForMatchingIndividualItem()
    {
        await using var db = Db(); await Seed(db);
        db.DeliveryMeals.Add(new() { Id = Guid.NewGuid(), UserId = Owner, PlaceName = "Калифорния", DishName = "Калифорния", Carbs = 100m });
        await db.SaveChangesAsync(Token);
        var response = await Service(db).SendAsync(new(null, "добавь Калифорнию", Guid.NewGuid()), Token);
        Assert.Equal(60m, response.Draft!.TotalCarbs); Assert.NotNull(Assert.Single(response.Draft.Items).SourceDeliveryMealItemId);
        var interpreted = await Service(db, new ExtractedModel(new([new("роллы калифорния", null, null, "Калифорния")], "draft", "create")))
            .SendAsync(new(null, "Подбери мой ужин", Guid.NewGuid()), Token);
        Assert.Equal(60m, interpreted.Draft!.TotalCarbs); Assert.NotNull(Assert.Single(interpreted.Draft.Items).SourceDeliveryMealItemId);
    }

    private sealed class ExtractedModel(AssistantSearch search) : ILlmClient
    { public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken) => Task.FromResult(search); }

    [Theory]
    [InlineData(0, 120, 0)]
    [InlineData(25, 120, 20.83)]
    [InlineData(7, 3, 233.33)]
    public void ManualTotal_DerivedReference_PreservesRoundingAndZero(decimal carbs, decimal quantity, decimal basis) =>
        Assert.Equal(basis, InsulinAndCoffee.Domain.Calculations.FoodCarbCalculator.ReferenceFromTotal(FoodMeasurementType.Grams, quantity, carbs));

    [Theory]
    [InlineData(FoodMeasurementType.Portion, 0.5, 7, 14)]
    [InlineData(FoodMeasurementType.Piece, 3, 9, 3)]
    public void ManualTotal_UnitReferences_UseTheRequestedQuantity(FoodMeasurementType unit, decimal quantity, decimal carbs, decimal basis) =>
        Assert.Equal(basis, InsulinAndCoffee.Domain.Calculations.FoodCarbCalculator.ReferenceFromTotal(unit, quantity, carbs));

    [Fact]
    public async Task InvalidManualResolution_DoesNotEraseCurrentDraftOrPendingQuantity()
    {
        await using var db = Db(); await Seed(db); var service = Service(db);
        var first = await service.SendAsync(new(null, "хочу съесть калифорнию и 120 жареной картошки", Guid.NewGuid()), Token);
        var missing = Assert.Single(first.Draft!.Unresolved!);
        foreach (var amount in new[] { -1m, .001m })
            await Assert.ThrowsAsync<ValidationException>(() => service.SendAsync(new(first.ConversationId, "Неверное значение", Guid.NewGuid(), ExpectedDraftVersion: first.DraftVersion,
                Resolution: new(missing.Id, "manual", Quantity: 120m, Unit: FoodMeasurementType.Grams, Carbs: amount)), Token));
        var state = await service.GetDraftStateAsync(first.ConversationId, Token);
        Assert.Equal(first.DraftVersion, state.DraftVersion); Assert.Equal(60m, state.Draft!.TotalCarbs); Assert.Equal(120m, Assert.Single(state.Draft.Unresolved!).Query.Quantity);
    }

    [Fact]
    public async Task SameNamedManualEntry_DoesNotRewriteSavedItemSourceVerification()
    {
        await using var db = Db(); await Seed(db);
        var verifiedAt = TimeProvider.System.GetUtcNow();
        (await db.DeliveryMealItems.SingleAsync(item => item.Name == "Калифорния", Token)).SourceMealVerifiedAt = verifiedAt;
        await db.SaveChangesAsync(Token); var service = Service(db);
        var first = await service.SendAsync(new(null, "добавь Калифорнию и неизвестный продукт", Guid.NewGuid()), Token);
        var pending = Assert.Single(first.Draft!.Unresolved!);
        var result = await service.SendAsync(new(first.ConversationId, "Мои углеводы", Guid.NewGuid(), ExpectedDraftVersion: first.DraftVersion,
            Resolution: new(pending.Id, "manual", Quantity: 1m, Unit: FoodMeasurementType.Portion, Carbs: 10m, SearchName: "Калифорния")), Token);
        var prepared = await service.PrepareDraftAsync(result.ConversationId, (await service.GetDraftStateAsync(result.ConversationId, Token)).DraftVersion, Token);
        Assert.Equal(verifiedAt, prepared[0].Provenance!.SourceMealVerifiedAt); Assert.NotNull(prepared[0].SourceDeliveryMealItemId);
        Assert.Null(prepared[1].Provenance!.SourceMealVerifiedAt); Assert.Null(prepared[1].SourceDeliveryMealItemId);
        Assert.Equal(70m, result.Draft!.TotalCarbs);
    }

    [Fact]
    public async Task GenericRollCategory_ResolvesAnArbitrarySavedItemWithoutFoodSpecificAliases()
    {
        await using var db = Db();
        db.DeliveryMeals.Add(new() { Id = Guid.NewGuid(), UserId = Owner, PlaceName = "Place", DishName = "Set", Items =
            [new() { Id = Guid.NewGuid(), Name = "Лос-Анджелес", Quantity = 1m, MeasurementType = FoodMeasurementType.Portion, CarbsPerUnitSnapshot = 42m, CalculatedCarbs = 42m }] });
        await db.SaveChangesAsync(Token);
        var result = await Service(db).SendAsync(new(null, "добавь ролл Лос-Анджелес", Guid.NewGuid()), Token);
        Assert.Equal(42m, result.Draft!.TotalCarbs); Assert.NotNull(Assert.Single(result.Draft.Items).SourceDeliveryMealItemId); Assert.Empty(result.Draft.Issues);
        var lookup = await Service(db).SendAsync(new(result.ConversationId, "Лос-Анджелес", Guid.NewGuid()), Token);
        Assert.Null(lookup.Draft); Assert.Single(lookup.Knowledge!.Items!); Assert.Equal(result.DraftVersion, lookup.DraftVersion);
    }

    [Fact]
    public void LegacyIssues_RecoverPerItemQueriesAndStableIdsWithoutOverwritingHistoricalData()
    {
        var legacy = new AssistantDraft([], ["Выбери сохранённый заказ роллы калифорния из Калифорния.", "Не найден сохранённый продукт: жареная картошка."], 0m);
        var hydrated = AssistantDraftBuilder.HydrateLegacy(legacy, "хочу съесть калифорнию и 120 жареной картошки");
        Assert.Equal(2, hydrated.Unresolved!.Count); Assert.Null(hydrated.Unresolved[0].Query.Restaurant);
        Assert.Equal(120m, hydrated.Unresolved[1].Query.Quantity);
        Assert.Equal(hydrated.Unresolved.Select(item => item.Id), AssistantDraftBuilder.HydrateLegacy(legacy).Unresolved!.Select(item => item.Id));
        Assert.Null(legacy.Unresolved); Assert.Empty(legacy.Items);
    }

    [Fact]
    public async Task MatchingDiagnostics_DefaultLevel_ContainsStableIdsButNotFoodNamesOrAmounts()
    {
        await using var db = Db(); await Seed(db);
        var logger = new MatchLogger(); var operation = Guid.NewGuid(); var conversation = Guid.NewGuid();
        var builder = new AssistantDraftBuilder(db, new UserContext(), new(db, new UserContext()), logger,
            new() { OperationId = operation, ConversationId = conversation, CorrelationId = "matching-correlation" });
        await builder.BuildAsync([new("Калифорния", null, null)], Token);
        var text = string.Join("\n", logger.Messages);
        Assert.Contains(operation.ToString(), text); Assert.Contains(conversation.ToString(), text);
        Assert.Contains("matching-correlation", text); Assert.Contains("DeliveryMealItem", text); Assert.Contains("Resolved", text);
        Assert.DoesNotContain("Калифорния", text); Assert.DoesNotContain("60 g", text);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    public void ManualReference_InvalidTotalOrQuantity_RejectsInput(decimal carbs, decimal quantity) =>
        Assert.Throws<InsulinAndCoffee.Domain.Exceptions.CarbCalculationException>(() => InsulinAndCoffee.Domain.Calculations.FoodCarbCalculator.ReferenceFromTotal(FoodMeasurementType.Grams, quantity, carbs));

    private sealed class MatchLogger : ILogger<AssistantDraftBuilder>
    {
        public List<string> Messages { get; } = [];
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (IsEnabled(level)) Messages.Add(formatter(state, exception)); }
    }

    private sealed class UserContext : ICurrentUser { public Guid UserId => Owner; }
    private sealed class NoProvider : ILlmClient
    { public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken) => throw new InvalidOperationException("Deterministic scenarios must not call OpenAI."); }
}
