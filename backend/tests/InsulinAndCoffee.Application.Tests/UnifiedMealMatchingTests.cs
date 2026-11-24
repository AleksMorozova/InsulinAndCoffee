using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsulinAndCoffee.Application.Tests;

public class UnifiedMealMatchingTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly DateTimeOffset Time = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("  RED\t  Lentil—Soup! ", "Red lentil soup")]
    [InlineData("борщ", "Borscht")]
    [InlineData("ПШЕНИЧНА КАША", "Wheat porridge")]
    [InlineData("Калифорния", "California")]
    [InlineData("Crème brûlée", "Crème brûlée")]
    [InlineData("Ｃａｌｉｆｏｒｎｉａ", "California")]
    public async Task Build_NormalizedOrConfiguredAlias_ResolvesDiscoveredFood(string query, string saved)
    {
        await using var db = Db();
        var food = Food(saved);
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var knowledge = await Knowledge(db).SearchAsync([new(query, 2m, FoodMeasurementType.Portion)], CancellationToken.None);
        Assert.Equal(food.Id, Assert.Single(knowledge.Foods).Id);
        var draft = await Builder(db).BuildAsync([new(query, 2m, FoodMeasurementType.Portion)], CancellationToken.None);
        Assert.NotNull(draft);
        Assert.Empty(draft.Issues);
        Assert.Equal(food.Id, Assert.Single(draft.Items).FoodItemId);
        Assert.Equal(40m, draft.TotalCarbs);
    }

    [Fact]
    public async Task Build_ExactName_WinsOverAliasesAndQualifiedNames()
    {
        await using var db = Db();
        var exact = Food("Pizza");
        db.FoodItems.AddRange(exact, Food("Пицца"), Food("Four cheese pizza"));
        await db.SaveChangesAsync(CancellationToken.None);
        var draft = await Builder(db).BuildAsync([new("pizza", 1m, FoodMeasurementType.Portion)], CancellationToken.None);
        Assert.Equal(exact.Id, Assert.Single(Assert.IsType<AssistantDraft>(draft).Items).FoodItemId);
        Assert.Equal(exact.Id, (await Knowledge(db).SearchAsync([new("pizza", null, null)], CancellationToken.None)).Foods[0].Id);
    }

    [Theory]
    [InlineData("Red", "Red lentil soup")]
    [InlineData("rice", "Licorice")]
    [InlineData("Kaliforniya", "California")]
    [InlineData("pizza", "Four cheese pizza")]
    [InlineData("bread", "Banana bread")]
    public async Task Build_WeakOrArbitraryTransliteration_DoesNotResolveUniqueFood(string query, string saved)
    {
        await using var db = Db();
        db.FoodItems.Add(Food(saved));
        await db.SaveChangesAsync(CancellationToken.None);
        var draft = Assert.IsType<AssistantDraft>(await Builder(db).BuildAsync([new(query, 1m, null)], CancellationToken.None));
        Assert.Empty(draft.Items);
        Assert.Single(draft.Issues);
    }

    [Theory]
    [InlineData("Хули Хули")]
    [InlineData("Шаурма Хули Хули")]
    [InlineData("Шаурма из Хули Хули")]
    public async Task Build_RestaurantContext_PreservesWholeOrderAndOtherRestaurants(string query)
    {
        await using var db = Db();
        var order = Order("Huli Huli", "Shawarma", 95m);
        db.DeliveryMeals.AddRange(order, Order("Other restaurant", "Shawarma", 70m), Order("Huli Huli", "Shawarma", 50m, Other));
        await db.SaveChangesAsync(CancellationToken.None);
        var knowledge = await Knowledge(db).SearchAsync([new(query, null, null)], CancellationToken.None);
        Assert.Equal(order.Id, Assert.Single(knowledge.Orders).Id);
        var draft = Assert.IsType<AssistantDraft>(await Builder(db).BuildAsync([new(query, null, null)], CancellationToken.None));
        Assert.Equal(95m, draft.TotalCarbs);
        Assert.Equal(FoodMeasurementType.Portion, Assert.Single(draft.Items).MeasurementType);
        Assert.Equal(order.Id, Assert.Single(draft.Sources ?? []).RecordId);
        Assert.Empty(draft.Issues);
        Assert.Single(await Builder(db).PrepareAsync(draft.Items, CancellationToken.None, draft.Sources));
    }

    [Fact]
    public async Task Build_MultipleRestaurantCompositions_RequiresSelectionAndNeverExtractsIndividualItems()
    {
        await using var db = Db();
        db.DeliveryMeals.AddRange(Order("Huli Huli", "Shawarma", 95m), Order("Huli Huli", "Shawarma", 120m),
            Order("Le Grill", "California and five sushi", 135m));
        await db.SaveChangesAsync(CancellationToken.None);
        foreach (var query in new[] { new AssistantFoodQuery("Shawarma", null, null, "Huli Huli"), new("California", 60m, FoodMeasurementType.Grams, "Le Grill") })
        {
            var draft = Assert.IsType<AssistantDraft>(await Builder(db).BuildAsync([query], CancellationToken.None));
            Assert.Empty(draft.Items);
            Assert.Single(draft.Issues);
        }
    }

    [Theory]
    [InlineData("California 60 г")]
    [InlineData("Калифорния 60 грамм")]
    [InlineData("60 grams California")]
    public async Task Build_GramExpression_PreservesMeasurementAndExistingFormula(string query)
    {
        await using var db = Db();
        var food = Food("California", unit: FoodMeasurementType.Grams);
        food.CarbsPer100g = 23m;
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var draft = Assert.IsType<AssistantDraft>(await Builder(db).BuildAsync([new(query, null, null)], CancellationToken.None));
        Assert.Equal(60m, Assert.Single(draft.Items).Quantity);
        Assert.Equal(13.8m, draft.TotalCarbs);
    }

    [Fact]
    public async Task Build_RestaurantExactContext_WinsOverAliasWhileUnscopedDiscoveryKeepsBoth()
    {
        await using var db = Db();
        var exact = Order("Huli Huli", "Shawarma", 95m);
        db.DeliveryMeals.AddRange(exact, Order("Хули Хули", "Shawarma", 100m), Order("Other place", "Shawarma", 80m));
        await db.SaveChangesAsync(CancellationToken.None);
        var found = await Knowledge(db).SearchAsync([new("Shawarma", null, null)], CancellationToken.None);
        Assert.Equal(3, found.Orders.Count);
        var unscoped = Assert.IsType<AssistantDraft>(await Builder(db).BuildAsync([new("Shawarma", 1m, null)], CancellationToken.None));
        Assert.Empty(unscoped.Items);
        var scoped = Assert.IsType<AssistantDraft>(await Builder(db).BuildAsync([new("Shawarma", 1m, null, "Huli Huli")], CancellationToken.None));
        Assert.Equal(exact.Id, Assert.Single(scoped.Sources ?? []).RecordId);
        Assert.Equal(95m, scoped.TotalCarbs);
    }

    [Fact]
    public async Task Build_ZeroCarbOrder_PreservesZeroAsValid()
    {
        await using var db = Db();
        db.DeliveryMeals.Add(Order("Huli Huli", "Chicken", 0m));
        await db.SaveChangesAsync(CancellationToken.None);
        var draft = Assert.IsType<AssistantDraft>(await Builder(db).BuildAsync([new("Chicken", null, null, "Huli Huli")], CancellationToken.None));
        Assert.Single(draft.Items);
        Assert.Empty(draft.Issues);
        Assert.Equal(0m, draft.TotalCarbs);
    }

    [Fact]
    public async Task Selection_Retry_ReusesPersistedDraftAndDoesNotDuplicateItems()
    {
        await using var db = Db();
        var food = Food("Salad");
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var model = new Model(new([]));
        var service = Service(db, model);
        var request = new MealAssistantRequest(null, "Use salad", Guid.NewGuid(), new(food.Id, 2m));
        var first = await service.SendAsync(request, CancellationToken.None);
        var second = await service.SendAsync(request, CancellationToken.None);
        Assert.Equal(first.ConversationId, second.ConversationId);
        Assert.Single(Assert.IsType<AssistantDraft>(second.Draft).Items);
        Assert.Equal(1, await db.AssistantConversations.CountAsync(CancellationToken.None));
        Assert.Equal(2, await db.AssistantMessages.CountAsync(CancellationToken.None));
        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public async Task Selection_NameContainingGramText_DoesNotReinterpretSavedPortionBasis()
    {
        await using var db = Db();
        var food = Food("California 60 g");
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var result = await Service(db, new Model(new([]))).SendAsync(
            new(null, "Choose saved California", Guid.NewGuid(), Selection: new(food.Id)), CancellationToken.None);
        var item = Assert.Single(Assert.IsType<AssistantDraft>(result.Draft).Items);
        Assert.Equal("California 60 g", item.FoodName);
        Assert.Equal(FoodMeasurementType.Portion, item.MeasurementType);
        Assert.Equal(1m, item.Quantity);
        Assert.Equal(20m, item.EffectiveCarbs);
    }

    [Theory]
    [InlineData("Bread -30 pieces")]
    [InlineData("Bread - 30 pieces")]
    [InlineData("Bread 0 pieces")]
    public async Task Build_NonPositiveQuantitySuffix_DoesNotBecomePositiveOrDefaultToOne(string name)
    {
        await using var db = Db();
        db.FoodItems.Add(Food("Bread", unit: FoodMeasurementType.Piece));
        await db.SaveChangesAsync(CancellationToken.None);
        var draft = Assert.IsType<AssistantDraft>(await Builder(db).BuildAsync([new(name, null, null)], CancellationToken.None));
        Assert.Empty(draft.Items);
        Assert.Single(draft.Issues);
    }

    [Theory]
    [InlineData(FoodMeasurementType.Piece, 2, 40)]
    [InlineData(FoodMeasurementType.Portion, 0.5, 10)]
    public async Task Build_SelectedDiscoveryId_UsesOwnedBasisAndRequestedQuantity(FoodMeasurementType unit, decimal quantity, decimal total)
    {
        await using var db = Db();
        var selected = Food("Salad", unit: unit);
        db.FoodItems.AddRange(selected, Food("Salad"));
        await db.SaveChangesAsync(CancellationToken.None);
        var model = new Model(new([], "history"));
        var result = await Service(db, model).SendAsync(new(null, "Use salad", Guid.NewGuid(), new(selected.Id, quantity, unit)), CancellationToken.None);
        Assert.Equal(0, model.Calls);
        Assert.Equal(selected.Id, Assert.Single(Assert.IsType<AssistantDraft>(result.Draft).Items).FoodItemId);
        Assert.Equal(total, result.Draft.TotalCarbs);
        Assert.Empty(db.Meals);
    }

    [Fact]
    public async Task Selection_DeletedOrOtherUserId_RejectsWithoutPersistenceOrProviderCall()
    {
        await using var db = Db();
        var otherFood = Food("Salad", Other);
        var deletedFood = Food("Deleted salad");
        db.FoodItems.AddRange(otherFood, deletedFood);
        await db.SaveChangesAsync(CancellationToken.None);
        db.FoodItems.Remove(deletedFood);
        await db.SaveChangesAsync(CancellationToken.None);
        var model = new Model(new([]));
        foreach (var id in new[] { otherFood.Id, deletedFood.Id, Guid.NewGuid() })
            await Assert.ThrowsAsync<ValidationException>(() => Service(db, model).SendAsync(
                new(null, "Use salad", Guid.NewGuid(), new(id, 1m)), CancellationToken.None));
        Assert.Equal(0, model.Calls);
        Assert.Empty(db.AssistantConversations);
        Assert.Empty(db.AssistantMessages);
    }

    [Fact]
    public async Task Selection_DiscoveredAmbiguousCandidate_ClarifiesExactIdWithPendingQuantity()
    {
        await using var db = Db();
        var selected = Food("Salad");
        db.FoodItems.AddRange(selected, Food("Salad"));
        await db.SaveChangesAsync(CancellationToken.None);
        var model = new Model(new([new("Salad", 2m, FoodMeasurementType.Portion)], "draft"));
        var service = Service(db, model);
        var first = await service.SendAsync(new(null, "Plan two portions of salad", Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(2, first.Knowledge?.Foods.Count);
        Assert.Empty(Assert.IsType<AssistantDraft>(first.Draft).Items);
        var result = await service.SendAsync(new(first.ConversationId, "Choose this salad", Guid.NewGuid(), Selection: new(selected.Id), ExpectedDraftVersion: first.DraftVersion), CancellationToken.None);
        Assert.Empty(Assert.IsType<AssistantDraft>(result.Draft).Issues);
        Assert.Equal(40m, result.Draft.TotalCarbs);
        Assert.Equal(1, model.Calls);
    }

    [Fact]
    public async Task Selection_PendingGrams_DoesNotBecomeTheSameNumberOfPortions()
    {
        await using var db = Db();
        var selected = Food("Salad");
        db.FoodItems.AddRange(selected, Food("Salad"));
        await db.SaveChangesAsync(CancellationToken.None);
        var model = new Model(new([new("Salad", 60m, FoodMeasurementType.Grams)], "draft"));
        var service = Service(db, model);
        var first = await service.SendAsync(new(null, "Plan 60 grams of salad", Guid.NewGuid()), CancellationToken.None);
        await Assert.ThrowsAsync<ValidationException>(() => service.SendAsync(
            new(first.ConversationId, "Choose this salad", Selection: new(selected.Id)), CancellationToken.None));
        Assert.Equal(2, await db.AssistantMessages.CountAsync(CancellationToken.None));
        var result = await service.SendAsync(new(first.ConversationId, "Choose two portions", Guid.NewGuid(), Selection: new(selected.Id, 2m), ExpectedDraftVersion: first.DraftVersion), CancellationToken.None);
        Assert.Equal(40m, Assert.IsType<AssistantDraft>(result.Draft).TotalCarbs);
        Assert.Equal(1, model.Calls);
    }

    [Fact]
    public async Task Update_MultipleUnknownItems_PreservesPendingItemsWhileAddingResolvedFood()
    {
        await using var db = Db();
        db.FoodItems.AddRange(Food("Bread"), Food("Brown bread"));
        await db.SaveChangesAsync(CancellationToken.None);
        var builder = Builder(db);
        var draft = Assert.IsType<AssistantDraft>(await builder.BuildAsync([new("Bread", 1m, null), new("Unknown", 1m, null), new("Missing", 1m, null)], CancellationToken.None));
        Assert.Equal(2, draft.Issues.Count);
        Assert.Null(draft.Pending);
        var edited = Assert.IsType<AssistantDraft>(await builder.UpdateAsync(new([new("Bread", 2m, null)], "draft", "add"), draft, CancellationToken.None));
        Assert.Equal(2, edited.Items.Count);
        Assert.Equal(draft.Items[0], edited.Items[0]);
        Assert.Equal(60m, edited.TotalCarbs);
        Assert.Equal(draft.Issues, edited.Issues);
        Assert.Null(edited.EditFeedback);
        Assert.Equal(draft.Unresolved, edited.Unresolved);
    }

    [Fact]
    public async Task Update_AmbiguousTarget_DoesNotChangeQuantityOrReplaceItems()
    {
        await using var db = Db();
        db.FoodItems.AddRange(Food("White bread"), Food("Brown bread"));
        await db.SaveChangesAsync(CancellationToken.None);
        var builder = Builder(db);
        var draft = Assert.IsType<AssistantDraft>(await builder.BuildAsync([new("White bread", 1m, null), new("Brown bread", 1m, null)], CancellationToken.None));
        foreach (var operation in new[] { "increase", "replace" })
        {
            var edited = Assert.IsType<AssistantDraft>(await builder.UpdateAsync(new([new("Bread", 2m, null)], "draft", operation, "bread"), draft, CancellationToken.None));
            Assert.Equal(draft.Items, edited.Items);
            Assert.NotNull(edited.EditFeedback);
        }
    }

    [Fact]
    public async Task History_OlderExactComposition_IsRankedBeforeRecentPartialWindow()
    {
        await using var db = Db();
        var exact = Meal(Time.AddYears(-2), Owner, "Chicken", "Rice");
        db.Meals.Add(exact);
        for (var index = 0; index < 300; index++) db.Meals.Add(Meal(Time.AddMinutes(index), Owner, "Chicken"));
        db.Meals.Add(Meal(Time.AddDays(1), Other, "Chicken", "Rice"));
        await db.SaveChangesAsync(CancellationToken.None);
        var result = await new MealHistorySearchService(db, new User(Owner)).SearchAsync(
            new([new("Chicken", null, null), new("Rice", null, null)]), CancellationToken.None);
        Assert.Equal(exact.Id, result[0].Id);
        Assert.Equal("Exact composition", result[0].Match);
        Assert.DoesNotContain(result, match => match.MealTime == Time.AddDays(1));
    }

    [Fact]
    public void History_PriorityQuery_TranslatesOnPostgreSqlBeforeLimit()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=translation_test;Username=test").Options);
        var sql = db.Meals.Where(meal => meal.UserId == Owner)
            .OrderByDescending(MealHistorySearchService.CandidatePriority([new("Chicken", null, null), new("Rice", null, null)]))
            .ThenByDescending(meal => meal.MealTime).Take(250).Include(meal => meal.Items).ToQueryString();
        Assert.Contains("regexp_replace", sql);
        Assert.Contains("CASE", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("UserId", sql);
    }

    [Fact]
    public async Task History_WholeWordDiscovery_PreservesQualifiedNamesWithoutSubstringFalsePositives()
    {
        await using var db = Db();
        var meal = Meal(Time, Owner, "Chicken breast", "Rice");
        db.Meals.AddRange(meal, Meal(Time.AddMinutes(1), Owner, "Licorice"));
        await db.SaveChangesAsync(CancellationToken.None);
        var result = await new MealHistorySearchService(db, new User(Owner)).SearchAsync(
            new([new("Chicken", null, null), new("Rice", null, null)]), CancellationToken.None);
        Assert.Equal(meal.Id, Assert.Single(result).Id);
        Assert.Equal("Similar composition", result[0].Match);
    }

    [Fact]
    public async Task History_RepeatedFoodQuery_DoesNotLabelDifferentCompositionExact()
    {
        await using var db = Db();
        db.Meals.Add(Meal(Time, Owner, "Chicken", "Rice"));
        await db.SaveChangesAsync(CancellationToken.None);
        var result = await new MealHistorySearchService(db, new User(Owner)).SearchAsync(
            new([new("Chicken", null, null), new("Chicken", null, null)]), CancellationToken.None);
        Assert.NotEqual("Exact composition", Assert.Single(result).Match);
    }

    [Fact]
    public async Task Send_StandaloneOwnedExactName_SkipsProviderButConversationalCommandDoesNot()
    {
        await using var db = Db();
        db.FoodItems.Add(Food("Red lentil soup"));
        await db.SaveChangesAsync(CancellationToken.None);
        var model = new Model(new([new("Red lentil soup", 1m, null)], "draft", "add"));
        var service = Service(db, model);
        var result = await service.SendAsync(new(null, "  RED  LENTIL SOUP  "), CancellationToken.None);
        Assert.Single(Assert.IsType<AssistantKnowledge>(result.Knowledge).Foods);
        Assert.Equal(0, model.Calls);
        await service.SendAsync(new(result.ConversationId, "add another 30 g", Guid.NewGuid(), ExpectedDraftVersion: result.DraftVersion), CancellationToken.None);
        Assert.Equal(1, model.Calls);
    }

    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static FoodItem Food(string name, Guid? owner = null, FoodMeasurementType unit = FoodMeasurementType.Portion) =>
        new() { Id = Guid.NewGuid(), UserId = owner ?? Owner, Name = name, MeasurementType = unit, CarbsPerUnit = 20m };
    private static DeliveryMeal Order(string place, string dish, decimal carbs, Guid? owner = null) =>
        new() { Id = Guid.NewGuid(), UserId = owner ?? Owner, PlaceName = place, DishName = dish, Carbs = carbs };
    private static Meal Meal(DateTimeOffset time, Guid owner, params string[] names) => new()
    {
        Id = Guid.NewGuid(), UserId = owner, MealTime = time,
        Items = names.Select(name => new MealItem { Id = Guid.NewGuid(), FoodNameSnapshot = name,
            MeasurementType = FoodMeasurementType.Grams, Quantity = 100m, CarbsPer100gSnapshot = 20m, CalculatedCarbs = 20m }).ToList()
    };
    private static AssistantDraftBuilder Builder(AppDbContext db) => new(db, new User(Owner), new MealCalculationService(db, new User(Owner)));
    private static FoodKnowledgeSearchService Knowledge(AppDbContext db) => new(db, new User(Owner));
    private static MealAssistantService Service(AppDbContext db, Model model) => new(db, new User(Owner), model,
        new MealHistorySearchService(db, new User(Owner)), TimeProvider.System, NullLogger<MealAssistantService>.Instance, Builder(db), Knowledge(db));
    private sealed class User(Guid id) : ICurrentUser { public Guid UserId => id; }
    private sealed class Model(AssistantSearch result) : ILlmClient
    {
        public int Calls { get; private set; }
        public Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(result); }
    }
}
