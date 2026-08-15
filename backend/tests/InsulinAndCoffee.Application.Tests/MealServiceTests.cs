using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Domain.Exceptions;
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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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
    public async Task CalculateMealAsync_WhenDiabetesSettingsAreMissing_ThrowsNotFound()
    {
        await using var db = CreateDbContext();
        var food = AddFood(db, "Bread", 40m);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CalculateMealAsync(
                new CalculateMealRequest(MealType.Breakfast, 6.5m, [new MealItemInputDto(food.Id, 100m)]),
                CancellationToken.None));

        Assert.Contains("Diabetes settings with id", exception.Message);
    }

    [Fact]
    public async Task CalculateMealAsync_ForGramsFood_CalculatesCarbsFromCarbsPer100g()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var food = AddFood(db, "Buckwheat", 19m);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

        var result = await service.CalculateMealAsync(
            new CalculateMealRequest(MealType.Lunch, 6.5m, [new MealItemInputDto(food.Id, 150m)]),
            CancellationToken.None);

        Assert.Equal(28.5m, result.TotalCarbs);
        var item = Assert.Single(result.Items);
        Assert.Equal(FoodMeasurementType.Grams, item.MeasurementType);
        Assert.Equal(150m, item.Quantity);
        Assert.Equal(150m, item.WeightGrams);
        Assert.Equal(19m, item.CarbsPer100g);
        Assert.Null(item.CarbsPerUnit);
    }

    [Theory]
    [InlineData(1, 15)]
    [InlineData(1.5, 22.5)]
    public async Task CalculateMealAsync_ForPortionFood_CalculatesCarbsFromCarbsPerUnit(decimal quantity, decimal expectedCarbs)
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var food = AddFood(db, "Borscht", carbsPer100g: null, FoodMeasurementType.Portion, carbsPerUnit: 15m);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

        var result = await service.CalculateMealAsync(
            new CalculateMealRequest(MealType.Lunch, 6.5m, [new MealItemInputDto(food.Id, quantity)]),
            CancellationToken.None);

        Assert.Equal(expectedCarbs, result.TotalCarbs);
        var item = Assert.Single(result.Items);
        Assert.Equal(FoodMeasurementType.Portion, item.MeasurementType);
        Assert.Equal(quantity, item.Quantity);
        Assert.Null(item.WeightGrams);
        Assert.Equal(15m, item.CarbsPerUnit);
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(2, 16)]
    public async Task CalculateMealAsync_ForPieceFood_CalculatesCarbsFromCarbsPerUnit(decimal quantity, decimal expectedCarbs)
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var food = AddFood(db, "Cheesecake", carbsPer100g: null, FoodMeasurementType.Piece, carbsPerUnit: 8m);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

        var result = await service.CalculateMealAsync(
            new CalculateMealRequest(MealType.Snack, 6.5m, [new MealItemInputDto(food.Id, quantity)]),
            CancellationToken.None);

        Assert.Equal(expectedCarbs, result.TotalCarbs);
        var item = Assert.Single(result.Items);
        Assert.Equal(FoodMeasurementType.Piece, item.MeasurementType);
        Assert.Equal(quantity, item.Quantity);
        Assert.Null(item.WeightGrams);
        Assert.Equal(8m, item.CarbsPerUnit);
    }

    [Fact]
    public async Task CalculateMealAsync_ForFractionalPieceQuantity_ThrowsValidation()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var food = AddFood(db, "Cheesecake", carbsPer100g: null, FoodMeasurementType.Piece, carbsPerUnit: 8m);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CalculateMealAsync(
                new CalculateMealRequest(MealType.Snack, 6.5m, [new MealItemInputDto(food.Id, 1.5m)]),
                CancellationToken.None));

        Assert.Equal("Piece quantity must be a whole number.", exception.Message);
    }

    [Fact]
    public async Task CalculateMealAsync_ForMixedMeasurementMeal_CalculatesTotalCarbs()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var buckwheat = AddFood(db, "Buckwheat", 19m);
        var borscht = AddFood(db, "Borscht", carbsPer100g: null, FoodMeasurementType.Portion, carbsPerUnit: 15m);
        var cheesecake = AddFood(db, "Cheesecake", carbsPer100g: null, FoodMeasurementType.Piece, carbsPerUnit: 8m);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

        var result = await service.CalculateMealAsync(
            new CalculateMealRequest(
                MealType.Dinner,
                6.5m,
                [
                    new MealItemInputDto(buckwheat.Id, 150m),
                    new MealItemInputDto(borscht.Id, 1m),
                    new MealItemInputDto(cheesecake.Id, 2m)
                ]),
            CancellationToken.None);

        Assert.Equal(59.5m, result.TotalCarbs);
        Assert.Equal(3, result.Items.Count);
    }


    [Fact]
    public async Task CalculateMealAsync_WithItemOverrideAndMealAdjustment_UsesEffectiveFinalCarbsForBolus()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var borscht = AddFood(db, "Borscht", carbsPer100g: null, FoodMeasurementType.Portion, carbsPerUnit: 15m);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

        var result = await service.CalculateMealAsync(
            new CalculateMealRequest(
                MealType.Lunch,
                6.5m,
                [new MealItemInputDto(borscht.Id, 1m, CarbOverride: 12m)],
                CarbAdjustment: 10m),
            CancellationToken.None);

        Assert.Equal(12m, result.FoodCarbs);
        Assert.Equal(10m, result.CarbAdjustment);
        Assert.Equal(22m, result.TotalCarbs);
        Assert.Equal(2.2m, result.SuggestedBolus);
        var item = Assert.Single(result.Items);
        Assert.Equal(15m, item.CalculatedCarbs);
        Assert.Equal(12m, item.CarbOverride);
        Assert.Equal(12m, item.EffectiveCarbs);
    }

    [Fact]
    public async Task CalculateMealAsync_WhenAdjustmentMakesFinalCarbsNegative_ThrowsValidation()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var bread = AddFood(db, "Bread", 40m);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CalculateMealAsync(
                new CalculateMealRequest(MealType.Breakfast, 6.5m, [new MealItemInputDto(bread.Id, 50m)], CarbAdjustment: -25m),
                CancellationToken.None));

        Assert.Equal("Final meal carbs cannot be negative.", exception.Message);
    }

    [Fact]
    public async Task CreateMealAsync_WithAdjustments_PersistsOriginalEffectiveAndFinalCarbs()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var borscht = AddFood(db, "Borscht", carbsPer100g: null, FoodMeasurementType.Portion, carbsPerUnit: 15m);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

        var result = await service.CreateMealAsync(
            new CreateMealRequest(
                MealType.Lunch,
                MealTime: null,
                PreMealGlucose: 6.5m,
                ConfirmedBolus: 2.2m,
                Notes: null,
                Items: [new MealItemInputDto(borscht.Id, 1m, CarbOverride: 12m)],
                CarbAdjustment: 10m),
            CancellationToken.None);

        Assert.Equal(22m, result.TotalCarbs);
        Assert.Equal(10m, result.CarbAdjustment);
        var item = Assert.Single(result.Items);
        Assert.Equal(15m, item.CalculatedCarbs);
        Assert.Equal(12m, item.CarbOverride);
        Assert.Equal(12m, item.EffectiveCarbs);

        var savedMeal = await db.Meals.Include(m => m.Items).SingleAsync();
        Assert.Equal(22m, savedMeal.TotalCarbs);
        Assert.Equal(10m, savedMeal.CarbAdjustment);
        var savedItem = Assert.Single(savedMeal.Items);
        Assert.Equal(15m, savedItem.CalculatedCarbs);
        Assert.Equal(12m, savedItem.CarbOverride);
    }
    [Fact]
    public async Task CreateMealAsync_ForPortionAndPieceItems_PersistsQuantitySemantics()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var borscht = AddFood(db, "Borscht", carbsPer100g: null, FoodMeasurementType.Portion, carbsPerUnit: 15m);
        var cheesecake = AddFood(db, "Cheesecake", carbsPer100g: null, FoodMeasurementType.Piece, carbsPerUnit: 8m);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

        var result = await service.CreateMealAsync(
            new CreateMealRequest(
                MealType.Dinner,
                MealTime: null,
                PreMealGlucose: 6.5m,
                ConfirmedBolus: null,
                Notes: null,
                Items:
                [
                    new MealItemInputDto(borscht.Id, 1.5m),
                    new MealItemInputDto(cheesecake.Id, 2m)
                ]),
            CancellationToken.None);

        Assert.Equal(38.5m, result.TotalCarbs);
        Assert.Contains(result.Items, item =>
            item.FoodItemId == borscht.Id &&
            item.MeasurementType == FoodMeasurementType.Portion &&
            item.Quantity == 1.5m &&
            item.CarbsPerUnitSnapshot == 15m &&
            item.WeightGrams is null);
        Assert.Contains(result.Items, item =>
            item.FoodItemId == cheesecake.Id &&
            item.MeasurementType == FoodMeasurementType.Piece &&
            item.Quantity == 2m &&
            item.CarbsPerUnitSnapshot == 8m &&
            item.WeightGrams is null);
    }

    [Fact]
    public async Task GetMealAsync_AfterFoodChangesFromGramsToPortion_KeepsHistoricalGramsSnapshot()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var soup = AddFood(db, "Soup", 5m);
        await db.SaveChangesAsync();
        var mealService = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));
        var foodService = new FoodService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));
        var savedMeal = await mealService.CreateMealAsync(
            new CreateMealRequest(MealType.Lunch, null, 6.5m, null, null, [new MealItemInputDto(soup.Id, 300m)]),
            CancellationToken.None);

        await foodService.UpdateFoodAsync(
            soup.Id,
            new UpsertFoodItemRequest("Soup", FoodMeasurementType.Portion, null, 20m, 4m, 2m, 60m, false),
            CancellationToken.None);

        var historicalMeal = await mealService.GetMealAsync(savedMeal.Id, CancellationToken.None);

        var historicalItem = Assert.Single(historicalMeal.Items);
        Assert.Equal(FoodMeasurementType.Grams, historicalItem.MeasurementType);
        Assert.Equal(300m, historicalItem.Quantity);
        Assert.Equal(300m, historicalItem.WeightGrams);
        Assert.Equal(5m, historicalItem.CarbsPer100gSnapshot);
        Assert.Null(historicalItem.CarbsPerUnitSnapshot);
        Assert.Equal(15m, historicalItem.CalculatedCarbs);
        Assert.Equal(15m, historicalMeal.TotalCarbs);
    }

    [Fact]
    public async Task GetMealAsync_AfterFoodChangesFromPieceToGrams_KeepsHistoricalPieceSnapshot()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var egg = AddFood(db, "Egg", carbsPer100g: null, FoodMeasurementType.Piece, carbsPerUnit: 0.5m);
        await db.SaveChangesAsync();
        var mealService = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));
        var foodService = new FoodService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));
        var savedMeal = await mealService.CreateMealAsync(
            new CreateMealRequest(MealType.Breakfast, null, 6.5m, null, null, [new MealItemInputDto(egg.Id, 2m)]),
            CancellationToken.None);

        await foodService.UpdateFoodAsync(
            egg.Id,
            new UpsertFoodItemRequest("Egg", FoodMeasurementType.Grams, 1m, null, 13m, 11m, 155m, false),
            CancellationToken.None);

        var historicalMeal = await mealService.GetMealAsync(savedMeal.Id, CancellationToken.None);

        var historicalItem = Assert.Single(historicalMeal.Items);
        Assert.Equal(FoodMeasurementType.Piece, historicalItem.MeasurementType);
        Assert.Equal(2m, historicalItem.Quantity);
        Assert.Null(historicalItem.WeightGrams);
        Assert.Null(historicalItem.CarbsPer100gSnapshot);
        Assert.Equal(0.5m, historicalItem.CarbsPerUnitSnapshot);
        Assert.Equal(1m, historicalItem.CalculatedCarbs);
    }

    [Fact]
    public async Task CreateMealAsync_WhenReusingHistoricalSnapshot_DoesNotReinterpretQuantityWithCurrentFoodMeasurement()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        var soup = AddFood(db, "Soup", 5m);
        await db.SaveChangesAsync();
        var mealService = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));
        var foodService = new FoodService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone));
        var originalMeal = await mealService.CreateMealAsync(
            new CreateMealRequest(MealType.Lunch, null, 6.5m, null, null, [new MealItemInputDto(soup.Id, 300m)]),
            CancellationToken.None);
        var originalItem = Assert.Single(originalMeal.Items);

        await foodService.UpdateFoodAsync(
            soup.Id,
            new UpsertFoodItemRequest("Soup", FoodMeasurementType.Portion, null, 20m, 4m, 2m, 60m, false),
            CancellationToken.None);

        var reusedMeal = await mealService.CreateMealAsync(
            new CreateMealRequest(
                MealType.Lunch,
                null,
                6.5m,
                null,
                null,
                [
                    new MealItemInputDto(
                        originalItem.FoodItemId,
                        originalItem.Quantity,
                        originalItem.WeightGrams,
                        originalItem.MeasurementType,
                        originalItem.FoodNameSnapshot,
                        originalItem.CarbsPer100gSnapshot,
                        originalItem.CarbsPerUnitSnapshot)
                ]),
            CancellationToken.None);
        var newFoodCalculation = await mealService.CalculateMealAsync(
            new CalculateMealRequest(MealType.Lunch, 6.5m, [new MealItemInputDto(soup.Id, 1m)]),
            CancellationToken.None);

        var reusedItem = Assert.Single(reusedMeal.Items);
        Assert.Equal(FoodMeasurementType.Grams, reusedItem.MeasurementType);
        Assert.Equal(300m, reusedItem.Quantity);
        Assert.Equal(15m, reusedItem.CalculatedCarbs);
        Assert.Equal(15m, reusedMeal.TotalCarbs);

        var newFoodItem = Assert.Single(newFoodCalculation.Items);
        Assert.Equal(FoodMeasurementType.Portion, newFoodItem.MeasurementType);
        Assert.Equal(1m, newFoodItem.Quantity);
        Assert.Equal(20m, newFoodItem.CalculatedCarbs);
        Assert.Equal(20m, newFoodCalculation.TotalCarbs);
    }

    [Fact]
    public async Task CreateMealAsync_AllowsMissingConfirmedBolusAndDoesNotCopySuggestedBolus()
    {
        await using var db = CreateDbContext();
        AddDefaultSettings(db);
        await db.SaveChangesAsync();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

        var result = await service.ClearConfirmedBolusAsync(meal.Id, CancellationToken.None);

        Assert.Null(result.ConfirmedBolus);
        Assert.Equal(42m, result.TotalCarbs);
        Assert.Equal(4.2m, result.SuggestedBolus);
        Assert.Null((await db.Meals.SingleAsync()).ConfirmedBolus);
    }

    [Fact]
    public async Task ClearConfirmedBolusAsync_WhenMealDoesNotExist_ThrowsNotFound()
    {
        await using var db = CreateDbContext();
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ClearConfirmedBolusAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Contains("Meal with id", exception.Message);
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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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
        var service = new MealService(db, new FixedTimeProvider(LocalNoonUtc, LocalTimeZone), new MealCalculationService(db));

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

    private static FoodItem AddFood(
        AppDbContext db,
        string name,
        decimal? carbsPer100g,
        FoodMeasurementType measurementType = FoodMeasurementType.Grams,
        decimal? carbsPerUnit = null)
    {
        var food = new FoodItem
        {
            Id = Guid.NewGuid(),
            UserId = DefaultUser.Id,
            Name = name,
            MeasurementType = measurementType,
            CarbsPer100g = carbsPer100g,
            CarbsPerUnit = carbsPerUnit,
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

