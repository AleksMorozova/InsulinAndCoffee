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
