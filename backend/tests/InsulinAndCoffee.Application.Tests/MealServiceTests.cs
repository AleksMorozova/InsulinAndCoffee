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
            Meal(MealType.Dinner, 65, 0, mealTime: newerMealTime, createdAt: newerMealTime));
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

    private static Meal Meal(
        MealType mealType,
        decimal totalCarbs,
        decimal confirmedBolus,
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
