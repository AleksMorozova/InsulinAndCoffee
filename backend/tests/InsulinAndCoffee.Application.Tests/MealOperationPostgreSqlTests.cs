using System.Text.Json;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Tests;

public partial class AssistantPostgreSqlSafetyTests
{
    [AssistantPostgresFact]
    public async Task Operations_ConcurrentSameAddOperation_CommitsOneAdditionAndOneReceipt()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (owner, _, conversation) = await SeedAsync(database);
        var gate = new ConcurrentSave();
        await using var firstDb = database.Open(gate);
        await using var secondDb = database.Open(gate);
        var request = new MealAssistantRequest(conversation, "add Bread", Guid.NewGuid(), ExpectedDraftVersion: 1);
        var answers = await Task.WhenAll(Service(firstDb, owner).SendAsync(request, CancellationToken.None), Service(secondDb, owner).SendAsync(request, CancellationToken.None));
        Assert.Equal(1, answers.Count(answer => answer.Replayed));
        Assert.All(answers, answer => { Assert.Equal(2, answer.DraftVersion); Assert.Equal(2, answer.Draft!.Items.Count); });
        await using var check = database.Open();
        Assert.Equal(1, await check.AssistantRequestReceipts.CountAsync(receipt => receipt.RequestId == request.RequestId));
        var state = await Service(check, owner).GetDraftStateAsync(conversation, CancellationToken.None);
        Assert.Equal(2, state.Draft!.Items.Count); Assert.Equal(2, state.Draft.Items.Select(item => item.DraftItemId).Distinct().Count());
    }

    [AssistantPostgresFact]
    public async Task Operations_ConcurrentDifferentAdds_ConflictThenExplicitNewVersionAddsSecondServing()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (owner, _, conversation) = await SeedAsync(database);
        var gate = new ConcurrentSave();
        await using var firstDb = database.Open(gate); await using var secondDb = database.Open(gate);
        var request = new MealAssistantRequest(conversation, "add Bread", Guid.NewGuid(), ExpectedDraftVersion: 1);
        var results = await Task.WhenAll(SendOrConflict(Service(firstDb, owner), request), SendOrConflict(Service(secondDb, owner), request with { RequestId = Guid.NewGuid() }));
        Assert.Single(results, result => result.Response is not null); Assert.Single(results, result => result.Error?.Code == "draft_version_conflict");
        await using var check = database.Open();
        var state = await Service(check, owner).GetDraftStateAsync(conversation, CancellationToken.None);
        Assert.Equal(2, state.Draft!.Items.Count);
        var explicitAdditional = await Service(check, owner).SendAsync(request with { RequestId = Guid.NewGuid(), ExpectedDraftVersion = state.DraftVersion }, CancellationToken.None);
        Assert.Equal(3, explicitAdditional.Draft!.Items.Count);
    }

    [AssistantPostgresFact]
    public async Task Operations_HttpLegacyDuplicateAcceptance_RefreshAndCalculatorKeepExactly78Carbs()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var factory = new StartupFactory(database.ConnectionString, new UnexpectedModel());
        using var client = await AcceptanceLogin(factory);
        var owner = (await Get<AuthUserDto>(client, "/api/auth/me")).Id;
        var conversation = Guid.NewGuid();
        await using (var setup = database.Open())
        {
            var california = new FoodItem { Id = Guid.NewGuid(), UserId = owner, Name = "Калифорния", MeasurementType = FoodMeasurementType.Portion, CarbsPerUnit = 60m, CarbReferenceSource = CarbReferenceSource.Manual };
            var mash = new FoodItem { Id = Guid.NewGuid(), UserId = owner, Name = "Пюре", MeasurementType = FoodMeasurementType.Grams, CarbsPer100g = 15m, CarbReferenceSource = CarbReferenceSource.Manual };
            var potatoes = new FoodItem { Id = Guid.NewGuid(), UserId = owner, Name = "Жареная картошка", MeasurementType = FoodMeasurementType.Grams, CarbsPer100g = 29.17m, CarbReferenceSource = CarbReferenceSource.Manual };
            setup.FoodItems.AddRange(california, mash, potatoes); await setup.SaveChangesAsync(CancellationToken.None);
            var user = new CurrentUser(owner);
            var builder = new AssistantDraftBuilder(setup, user, new MealCalculationService(setup, user));
            var draft = await builder.BuildAsync([new("Калифорния", 1m, FoodMeasurementType.Portion, FoodItemId: california.Id),
                new("Пюре", 120m, FoodMeasurementType.Grams, FoodItemId: mash.Id), new("Жареная картошка", 120m, FoodMeasurementType.Grams, FoodItemId: potatoes.Id)], CancellationToken.None);
            Assert.NotNull(draft);
            var legacy = draft.Items.Select(item => item with { DraftItemId = null }).ToList(); legacy.Insert(1, legacy[0]);
            setup.AssistantConversations.Add(new AssistantConversation { Id = conversation, UserId = owner, DraftVersion = 1,
                DraftJson = JsonSerializer.Serialize(draft with { Items = legacy, TotalCarbs = 173m }) });
            await setup.SaveChangesAsync(CancellationToken.None);
        }
        var route = $"/api/meal-assistant/conversations/{conversation}";
        var old = await Get<AssistantDraftState>(client, route + "/draft-state");
        var again = await Get<AssistantDraftState>(client, route + "/draft-state");
        Assert.Equal(old.Draft!.Items.Select(item => item.DraftItemId), again.Draft!.Items.Select(item => item.DraftItemId));
        Assert.Equal(4, old.Draft.Items.Select(item => item.DraftItemId).Distinct().Count());
        var request = new MealAssistantRequest(conversation, "убери жареную картошку и калифорнию оставь одну порцию", Guid.NewGuid(), ExpectedDraftVersion: 1);
        var answer = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", request);
        Assert.Equal(78m, answer.Draft!.TotalCarbs); Assert.Equal(2, answer.Draft.Items.Count);
        Assert.Equal("Убрала жареную картошку. Калифорнию оставила одну порцию. Всего: 78 г углеводов.", answer.Message);
        Assert.Equal(old.Draft.Items[0].DraftItemId, answer.Draft.Items[0].DraftItemId);
        Assert.True((await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", request)).Replayed);
        var refreshed = await Get<AssistantTurn[]>(client, route);
        Assert.Equal(78m, refreshed[^1].Answer.Draft!.TotalCarbs); Assert.Equal(2, refreshed[^1].Answer.Draft!.Items.Count);
        var calculator = await Get<CalculatedMealItemDto[]>(client, route + $"/calculator-draft?expectedDraftVersion={answer.DraftVersion}");
        Assert.Equal(2, calculator.Length); Assert.Equal(78m, calculator.Sum(item => item.EffectiveCarbs));
        // Verify exact entry removal ignores an obsolete positional hint and survives another HTTP read.
        var removed = await Post<MealAssistantResponse>(client, "/api/meal-assistant/messages", new MealAssistantRequest(conversation, "Remove exact California", Guid.NewGuid(),
            ExpectedDraftVersion: answer.DraftVersion, Edit: new(99, "remove", DraftItemId: answer.Draft.Items[0].DraftItemId)));
        Assert.Equal(18m, removed.Draft!.TotalCarbs);
        Assert.Single((await Get<AssistantDraftState>(client, route + "/draft-state")).Draft!.Items);
        await using var check = database.Open(); Assert.Equal(3, await check.FoodItems.CountAsync(food => food.UserId == owner)); Assert.Empty(await check.Meals.Where(meal => meal.UserId == owner).ToListAsync());
    }
}
