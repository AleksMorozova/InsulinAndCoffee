using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Tests;

public class MealServiceTests
{
    private static readonly TimeZoneInfo LocalTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "Dashboard Test Time",
        TimeSpan.FromHours(3),
        "Dashboard Test Time",
        "Dashboard Test Time");

    private static readonly DateTimeOffset LocalNoonUtc = new(2026, 7, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetDashboardAsync_WhenNoMealsToday_ReturnsZeroTotalsAndEmptyMeals()
    {
        await using var db = CreateDbContext();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var result = await service.GetDashboardAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 12), result.Date);
        Assert.Equal(0, result.TotalCarbs);
        Assert.Equal(0, result.ConfirmedInsulin);
        Assert.Equal(0, result.MealCount);
        Assert.Empty(result.Meals);
    }

    [Fact]
    public async Task GetDashboardAsync_UsesLocalDayAndExcludesMealsOutsideToday()
    {
        await using var db = CreateDbContext();
        var previousLocalDay = new DateTimeOffset(2026, 7, 11, 20, 59, 0, TimeSpan.Zero);
        var todayInLocalTime = new DateTimeOffset(2026, 7, 11, 21, 30, 0, TimeSpan.Zero);
        db.Meals.AddRange(
            Meal(MealType.Dinner, 80, 8, mealTime: previousLocalDay, createdAt: previousLocalDay),
            Meal(MealType.Breakfast, 42, 4, mealTime: todayInLocalTime, createdAt: todayInLocalTime));
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var result = await service.GetDashboardAsync(CancellationToken.None);

        var meal = Assert.Single(result.Meals);
        Assert.Equal(MealType.Breakfast, meal.MealType);
        Assert.Equal(42, result.TotalCarbs);
        Assert.Equal(4, result.ConfirmedInsulin);
        Assert.Equal(1, result.MealCount);
    }

    [Fact]
    public async Task GetDashboardAsync_CalculatesTotalsMarksPendingMealsAndOrdersNewestFirst()
    {
        await using var db = CreateDbContext();
        var olderMealTime = new DateTimeOffset(2026, 7, 12, 6, 15, 0, TimeSpan.Zero);
        var newerMealTime = new DateTimeOffset(2026, 7, 12, 17, 10, 0, TimeSpan.Zero);
        db.Meals.AddRange(
            Meal(MealType.Breakfast, 30, 3.5m, mealTime: olderMealTime, createdAt: olderMealTime),
            Meal(MealType.Dinner, 65, null, mealTime: newerMealTime, createdAt: newerMealTime));
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var result = await service.GetDashboardAsync(CancellationToken.None);

        Assert.Equal(95, result.TotalCarbs);
        Assert.Equal(3.5m, result.ConfirmedInsulin);
        Assert.Equal(2, result.MealCount);
        Assert.Equal([MealType.Dinner, MealType.Breakfast], result.Meals.Select(meal => meal.MealType));

        var pendingMeal = result.Meals[0];
        Assert.True(pendingMeal.RequiresInsulinConfirmation);
        Assert.Null(pendingMeal.ConfirmedInsulin);

        var confirmedMeal = result.Meals[1];
        Assert.False(confirmedMeal.RequiresInsulinConfirmation);
        Assert.Equal(3.5m, confirmedMeal.ConfirmedInsulin);
    }

    [Fact]
    public async Task CreateMealAsync_AllowsMissingConfirmedBolusAndDoesNotCopySuggestedBolus()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var result = await service.CreateMealAsync(
            new CreateMealRequest(
                MealType.Lunch,
                MealTime: null,
                PreMealGlucose: 9.5m,
                ConfirmedBolus: null,
                Notes: null,
                Items: [],
                DirectCarbs: 50m,
                DirectFoodName: "Cafe bowl"),
            CancellationToken.None);

        Assert.Equal(6m, result.SuggestedBolus);
        Assert.Null(result.ConfirmedBolus);

        var savedMeal = await db.Meals.SingleAsync();
        Assert.Equal(6m, savedMeal.SuggestedBolus);
        Assert.Null(savedMeal.ConfirmedBolus);
    }

    [Fact]
    public async Task GetDashboardAsync_TreatsNullAsPendingAndConfirmedZeroAsComplete()
    {
        await using var db = CreateDbContext();
        var pendingMealTime = new DateTimeOffset(2026, 7, 12, 17, 10, 0, TimeSpan.Zero);
        var zeroConfirmedMealTime = new DateTimeOffset(2026, 7, 12, 6, 15, 0, TimeSpan.Zero);
        db.Meals.AddRange(
            Meal(MealType.Dinner, 65, null, mealTime: pendingMealTime, createdAt: pendingMealTime),
            Meal(MealType.Breakfast, 30, 0m, mealTime: zeroConfirmedMealTime, createdAt: zeroConfirmedMealTime));
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var result = await service.GetDashboardAsync(CancellationToken.None);

        Assert.Equal(95, result.TotalCarbs);
        Assert.Equal(0, result.ConfirmedInsulin);
        Assert.Equal(2, result.MealCount);

        var pendingMeal = Assert.Single(result.Meals, meal => meal.RequiresInsulinConfirmation);
        Assert.Equal(MealType.Dinner, pendingMeal.MealType);
        Assert.Null(pendingMeal.ConfirmedInsulin);

        var zeroConfirmedMeal = Assert.Single(result.Meals, meal => meal.MealType == MealType.Breakfast);
        Assert.False(zeroConfirmedMeal.RequiresInsulinConfirmation);
        Assert.Equal(0m, zeroConfirmedMeal.ConfirmedInsulin);
    }

    [Fact]
    public async Task GetDashboardAsync_ExcludesPreviousLocalDayPendingMeals()
    {
        await using var db = CreateDbContext();
        var previousLocalDay = new DateTimeOffset(2026, 7, 11, 20, 59, 0, TimeSpan.Zero);
        db.Meals.Add(Meal(MealType.Dinner, 80, null, mealTime: previousLocalDay, createdAt: previousLocalDay));
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var result = await service.GetDashboardAsync(CancellationToken.None);

        Assert.Empty(result.Meals);
        Assert.Equal(0, result.ConfirmedInsulin);
    }

    [Fact]
    public async Task ConfirmMealBolusAsync_AllowsExplicitZeroAndClearsPendingDashboardAction()
    {
        await using var db = CreateDbContext();
        var mealTime = new DateTimeOffset(2026, 7, 12, 17, 10, 0, TimeSpan.Zero);
        var meal = Meal(MealType.Dinner, 65, null, mealTime: mealTime, createdAt: mealTime);
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var result = await service.ConfirmMealBolusAsync(meal.Id, new ConfirmMealBolusRequest(0m), CancellationToken.None);
        var dashboard = await service.GetDashboardAsync(CancellationToken.None);

        Assert.Equal(0m, result.ConfirmedBolus);
        var dashboardMeal = Assert.Single(dashboard.Meals);
        Assert.False(dashboardMeal.RequiresInsulinConfirmation);
        Assert.Equal(0m, dashboardMeal.ConfirmedInsulin);
    }

    [Fact]
    public async Task ClearConfirmedBolusAsync_ForConfirmedMeal_ClearsBolusAndReturnsMealToPendingState()
    {
        await using var db = CreateDbContext();
        var mealTime = new DateTimeOffset(2026, 7, 12, 17, 10, 0, TimeSpan.Zero);
        var createdAt = new DateTimeOffset(2026, 7, 12, 17, 11, 0, TimeSpan.Zero);
        var meal = Meal(MealType.Dinner, 65m, 6.5m, mealTime: mealTime, createdAt: createdAt);
        meal.Items.Add(MealItem(meal.Id, "Bread", weightGrams: 100m, carbsPer100g: 40m));
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var result = await service.ClearConfirmedBolusAsync(meal.Id, CancellationToken.None);
        var dashboard = await service.GetDashboardAsync(CancellationToken.None);

        Assert.Null(result.ConfirmedBolus);
        Assert.Equal(65m, result.TotalCarbs);
        Assert.Equal(6.5m, result.SuggestedBolus);
        Assert.Equal(mealTime, result.MealTime);
        Assert.Equal(createdAt, result.CreatedAt);
        var item = Assert.Single(result.Items);
        Assert.Equal(100m, item.WeightGrams);
        Assert.Equal(40m, item.CalculatedCarbs);

        var savedMeal = await db.Meals.Include(m => m.Items).SingleAsync();
        Assert.Null(savedMeal.ConfirmedBolus);
        Assert.Equal(65m, savedMeal.TotalCarbs);
        Assert.Equal(6.5m, savedMeal.SuggestedBolus);
        Assert.Equal(mealTime, savedMeal.MealTime);
        Assert.Equal(createdAt, savedMeal.CreatedAt);
        Assert.Single(savedMeal.Items);

        var dashboardMeal = Assert.Single(dashboard.Meals);
        Assert.True(dashboardMeal.RequiresInsulinConfirmation);
        Assert.Null(dashboardMeal.ConfirmedInsulin);
    }

    [Fact]
    public async Task ClearConfirmedBolusAsync_WhenAlreadyEmpty_IsIdempotent()
    {
        await using var db = CreateDbContext();
        var mealTime = new DateTimeOffset(2026, 7, 12, 17, 10, 0, TimeSpan.Zero);
        var meal = Meal(MealType.Lunch, 42m, null, mealTime: mealTime, createdAt: mealTime);
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var result = await service.ClearConfirmedBolusAsync(meal.Id, CancellationToken.None);

        Assert.Null(result.ConfirmedBolus);
        Assert.Equal(42m, result.TotalCarbs);
        Assert.Equal(4.2m, result.SuggestedBolus);
        Assert.Null((await db.Meals.SingleAsync()).ConfirmedBolus);
    }

    [Fact]
    public async Task ClearConfirmedBolusAsync_WhenMealDoesNotExist_ThrowsKeyNotFound()
    {
        await using var db = CreateDbContext();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ClearConfirmedBolusAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("Meal was not found.", exception.Message);
    }

    [Fact]
    public async Task AddMealItemsAsync_ForUnconfirmedMeal_AppendsFoodRecalculatesTotalsAndKeepsMealPending()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var food = AddFood(db, "Bread", 40m);
        var mealTime = new DateTimeOffset(2026, 7, 12, 17, 10, 0, TimeSpan.Zero);
        var meal = Meal(MealType.Dinner, 30, null, mealTime: mealTime, createdAt: mealTime);
        meal.Items.Add(new MealItem
        {
            Id = Guid.NewGuid(),
            MealId = meal.Id,
            FoodItemId = Guid.Empty,
            FoodNameSnapshot = "Existing meal",
            WeightGrams = 100,
            CarbsPer100gSnapshot = 30,
            CalculatedCarbs = 30
        });
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var result = await service.AddMealItemsAsync(
            meal.Id,
            new AddMealItemsRequest([new MealItemInputDto(food.Id, 50m)]),
            CancellationToken.None);
        var dashboard = await service.GetDashboardAsync(CancellationToken.None);

        Assert.Equal(meal.Id, result.Id);
        Assert.Equal(50m, result.TotalCarbs);
        Assert.Equal(5m, result.SuggestedBolus);
        Assert.Null(result.ConfirmedBolus);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, await db.Meals.CountAsync());

        var dashboardMeal = Assert.Single(dashboard.Meals);
        Assert.True(dashboardMeal.RequiresInsulinConfirmation);
        Assert.Equal(50m, dashboardMeal.TotalCarbs);
        Assert.Null(dashboardMeal.ConfirmedInsulin);
    }

    [Fact]
    public async Task AddMealItemsAsync_ForConfirmedZeroMeal_IsRejected()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var food = AddFood(db, "Bread", 40m);
        var mealTime = new DateTimeOffset(2026, 7, 12, 17, 10, 0, TimeSpan.Zero);
        var meal = Meal(MealType.Dinner, 30, 0m, mealTime: mealTime, createdAt: mealTime);
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddMealItemsAsync(
                meal.Id,
                new AddMealItemsRequest([new MealItemInputDto(food.Id, 50m)]),
                CancellationToken.None));

        Assert.Equal("Cannot add food after the insulin dose has been confirmed.", exception.Message);
        Assert.Equal(30m, (await db.Meals.SingleAsync()).TotalCarbs);
    }

    [Fact]
    public async Task UpdateMealItemAsync_ForUnconfirmedMeal_RecalculatesCarbsAndSuggestedBolus()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var mealTime = new DateTimeOffset(2026, 7, 12, 17, 10, 0, TimeSpan.Zero);
        var meal = Meal(MealType.Dinner, 20, null, mealTime: mealTime, createdAt: mealTime);
        var item = MealItem(meal.Id, "Bread", weightGrams: 50m, carbsPer100g: 40m);
        meal.Items.Add(item);
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var result = await service.UpdateMealItemAsync(meal.Id, item.Id, new UpdateMealItemRequest(100m), CancellationToken.None);

        Assert.Equal(meal.Id, result.Id);
        Assert.Equal(40m, result.TotalCarbs);
        Assert.Equal(4m, result.SuggestedBolus);
        Assert.Null(result.ConfirmedBolus);
        var updatedItem = Assert.Single(result.Items);
        Assert.Equal(100m, updatedItem.WeightGrams);
        Assert.Equal(40m, updatedItem.CalculatedCarbs);
    }

    [Fact]
    public async Task RemoveMealItemAsync_ForUnconfirmedMeal_RecalculatesTotalsAndKeepsMealPending()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var mealTime = new DateTimeOffset(2026, 7, 12, 17, 10, 0, TimeSpan.Zero);
        var meal = Meal(MealType.Dinner, 50, null, mealTime: mealTime, createdAt: mealTime);
        var bread = MealItem(meal.Id, "Bread", weightGrams: 100m, carbsPer100g: 30m);
        var rice = MealItem(meal.Id, "Rice", weightGrams: 100m, carbsPer100g: 20m);
        meal.Items.Add(bread);
        meal.Items.Add(rice);
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var result = await service.RemoveMealItemAsync(meal.Id, rice.Id, CancellationToken.None);
        var dashboard = await service.GetDashboardAsync(CancellationToken.None);

        Assert.Equal(30m, result.TotalCarbs);
        Assert.Equal(3m, result.SuggestedBolus);
        Assert.Null(result.ConfirmedBolus);
        var remainingItem = Assert.Single(result.Items);
        Assert.Equal(bread.Id, remainingItem.Id);

        var dashboardMeal = Assert.Single(dashboard.Meals);
        Assert.True(dashboardMeal.RequiresInsulinConfirmation);
        Assert.Equal(30m, dashboardMeal.TotalCarbs);
    }

    [Fact]
    public async Task UpdateAndRemoveMealItemsAsync_ForConfirmedZeroMeal_AreRejected()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var mealTime = new DateTimeOffset(2026, 7, 12, 17, 10, 0, TimeSpan.Zero);
        var meal = Meal(MealType.Dinner, 20, 0m, mealTime: mealTime, createdAt: mealTime);
        var item = MealItem(meal.Id, "Bread", weightGrams: 50m, carbsPer100g: 40m);
        meal.Items.Add(item);
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));

        var updateException = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMealItemAsync(meal.Id, item.Id, new UpdateMealItemRequest(100m), CancellationToken.None));
        var removeException = await Assert.ThrowsAsync<ValidationException>(() =>
            service.RemoveMealItemAsync(meal.Id, item.Id, CancellationToken.None));

        Assert.Equal("Cannot edit food after the insulin dose has been confirmed.", updateException.Message);
        Assert.Equal("Cannot edit food after the insulin dose has been confirmed.", removeException.Message);
    }

    private static Meal Meal(
        MealType mealType,
        decimal totalCarbs,
        decimal? confirmedBolus,
        DateTimeOffset mealTime,
        DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = DefaultUser.Id,
        MealType = mealType,
        MealTime = mealTime,
        PreMealGlucose = 6.5m,
        TotalCarbs = totalCarbs,
        SuggestedBolus = totalCarbs / 10,
        ConfirmedBolus = confirmedBolus,
        CreatedAt = createdAt
    };

    private static void AddDefaultSettings(AppDbContext db) =>
        db.DiabetesSettings.Add(new DiabetesSettings
        {
            Id = Guid.NewGuid(),
            UserId = DefaultUser.Id,
            TargetGlucose = 6.5m,
            CarbRatio = 10m,
            CorrectionFactor = 3m,
            InsulinDurationHours = 4m,
            UpdatedAt = LocalNoonUtc
        });

    private static FoodItem AddFood(AppDbContext db, string name, decimal carbsPer100g)
    {
        var food = new FoodItem
        {
            Id = Guid.NewGuid(),
            UserId = DefaultUser.Id,
            Name = name,
            CarbsPer100g = carbsPer100g,
            ProteinPer100g = 0,
            FatPer100g = 0,
            CaloriesPer100g = 0,
            IsFavorite = false,
            CreatedAt = LocalNoonUtc
        };

        db.FoodItems.Add(food);
        return food;
    }

    private static MealItem MealItem(Guid mealId, string foodName, decimal weightGrams, decimal carbsPer100g) => new()
    {
        Id = Guid.NewGuid(),
        MealId = mealId,
        FoodItemId = Guid.NewGuid(),
        FoodNameSnapshot = foodName,
        WeightGrams = weightGrams,
        CarbsPer100gSnapshot = carbsPer100g,
        CalculatedCarbs = Math.Round(weightGrams * carbsPer100g / 100, 2)
    };

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow, TimeZoneInfo localTimeZone) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => localTimeZone;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
