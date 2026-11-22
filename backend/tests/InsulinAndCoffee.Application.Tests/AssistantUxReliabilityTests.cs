using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsulinAndCoffee.Application.Tests;

public class AssistantUxReliabilityTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly CancellationToken Token = CancellationToken.None;
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static MealAssistantService Service(AppDbContext db) => new(db, new UserContext(), new NoProvider(), new(db, new UserContext()), TimeProvider.System,
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
        var prepared = await service.PrepareDraftAsync(result.ConversationId, Token); Assert.Equal(2, prepared.Count);
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

    private sealed class UserContext : ICurrentUser { public Guid UserId => Owner; }
    private sealed class NoProvider : ILlmClient
    { public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken) => throw new InvalidOperationException("Deterministic scenarios must not call OpenAI."); }
}
