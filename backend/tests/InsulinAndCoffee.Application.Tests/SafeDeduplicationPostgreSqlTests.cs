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
    public async Task SafeDedup_HistoricalSushiDifferentOrdersAndUnits_ClarifiesWithoutChangingSourceRecords()
    {
        await using var database = await TestDatabase.CreateAsync(); var owner = Guid.NewGuid();
        await using var db = database.Open(); db.Users.Add(User(owner));
        var first = new DeliveryMeal { Id = Guid.NewGuid(), UserId = owner, PlaceName = "Le Gril", DishName = "Суши", Carbs = 75m };
        var second = new DeliveryMeal { Id = Guid.NewGuid(), UserId = owner, PlaceName = "Another saved order", DishName = "Суши", Carbs = 60m };
        var sushi = new DeliveryMealItem { Id = Guid.NewGuid(), DeliveryMealId = first.Id, Name = "Суши", Quantity = 5m, MeasurementType = FoodMeasurementType.Piece,
            CarbsPerUnitSnapshot = 15m, CalculatedCarbs = 75m, CarbOverride = 75m, CarbReferenceSource = CarbReferenceSource.Manual };
        var other = new DeliveryMealItem { Id = Guid.NewGuid(), DeliveryMealId = second.Id, Name = "Суши", Quantity = 1m, MeasurementType = FoodMeasurementType.Portion,
            CarbsPerUnitSnapshot = 60m, CalculatedCarbs = 60m, CarbReferenceSource = CarbReferenceSource.Manual };
        first.Items.Add(sushi); second.Items.Add(other); db.DeliveryMeals.AddRange(first, second); await db.SaveChangesAsync(CancellationToken.None);
        var builder = new AssistantDraftBuilder(db, new CurrentUser(owner), new MealCalculationService(db, new CurrentUser(owner)));
        var draft = await builder.BuildAsync([new("Суши", 5m, FoodMeasurementType.Piece, DeliveryMealItemId: sushi.Id),
            new("Суши", 1m, FoodMeasurementType.Portion, DeliveryMealItemId: other.Id)], CancellationToken.None);
        Assert.NotNull(draft); Assert.Empty(draft.DuplicateGroups!);
        var chat = new AssistantConversation { Id = Guid.NewGuid(), UserId = owner, DraftVersion = 1, DraftJson = JsonSerializer.Serialize(draft) };
        db.AssistantConversations.Add(chat); await db.SaveChangesAsync(CancellationToken.None);
        var answer = await Service(db, owner).SendAsync(new(chat.Id, "убери все суши", Guid.NewGuid(), ExpectedDraftVersion: 1), CancellationToken.None);
        Assert.Equal(2, answer.Draft!.Items.Count); Assert.Equal(135m, answer.Draft.TotalCarbs);
        Assert.Contains("Le Gril", answer.Draft.EditFeedback); Assert.Contains("Another saved order", answer.Draft.EditFeedback);
        Assert.DoesNotContain(sushi.Id.ToString(), answer.Draft.EditFeedback); Assert.DoesNotContain(other.Id.ToString(), answer.Draft.EditFeedback);
        Assert.Equal(75m, (await db.DeliveryMealItems.AsNoTracking().SingleAsync(item => item.Id == sushi.Id)).CarbOverride);
        var calculator = await Service(db, owner).PrepareDraftAsync(chat.Id, (await Service(db, owner).GetDraftStateAsync(chat.Id, CancellationToken.None)).DraftVersion, CancellationToken.None);
        Assert.Equal(135m, calculator.Sum(item => item.EffectiveCarbs)); Assert.Equal(2, calculator.Count);
        db.ChangeTracker.Clear(); Assert.Equal(2, (await Service(db, owner).GetDraftStateAsync(chat.Id, CancellationToken.None)).Draft!.Items.Count);
    }

    [AssistantPostgresFact]
    public async Task SafeDedup_ConcurrentSameCombineOperation_CommitsOnceWithUnchangedTotal()
    {
        await using var database = await TestDatabase.CreateAsync(); var (owner, _, conversation) = await SeedAsync(database);
        Guid entry;
        await using (var setup = database.Open())
        {
            var chat = await setup.AssistantConversations.SingleAsync(item => item.Id == conversation);
            var state = await Service(setup, owner).GetDraftStateAsync(conversation, CancellationToken.None); var item = state.Draft!.Items[0];
            entry = item.DraftItemId!.Value;
            chat.DraftJson = JsonSerializer.Serialize(state.Draft with { Items = [item, item with { DraftItemId = Guid.NewGuid() }], TotalCarbs = item.EffectiveCarbs * 2m });
            await setup.SaveChangesAsync(CancellationToken.None);
        }
        var gate = new ConcurrentSave(); await using var first = database.Open(gate); await using var second = database.Open(gate);
        var request = new MealAssistantRequest(conversation, "Combine", Guid.NewGuid(), ExpectedDraftVersion: 1, Edit: new(0, "combine", DraftItemId: entry));
        var results = await Task.WhenAll(Service(first, owner).SendAsync(request, CancellationToken.None), Service(second, owner).SendAsync(request, CancellationToken.None));
        Assert.Single(results, result => result.Replayed); Assert.All(results, result => { Assert.Single(result.Draft!.Items); Assert.Equal(20m, result.Draft.TotalCarbs); });
        await using var check = database.Open(); var loaded = await Service(check, owner).GetDraftStateAsync(conversation, CancellationToken.None);
        Assert.Single(loaded.Draft!.Items); Assert.Equal(2m, loaded.Draft.Items[0].Quantity); Assert.Equal(20m, loaded.Draft.TotalCarbs);
        Assert.Equal(1, await check.AssistantRequestReceipts.CountAsync(receipt => receipt.RequestId == request.RequestId));
    }
}
