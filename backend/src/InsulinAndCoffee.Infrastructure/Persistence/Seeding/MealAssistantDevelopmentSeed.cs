using System.Data;
using InsulinAndCoffee.Domain.Calculations;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Infrastructure.Persistence.Seeding;

public static class MealAssistantDevelopmentSeed
{
    public static readonly Guid UserId = Guid.Parse("5f91e464-e7d3-43fb-9e2d-2f1dd96cfd93");
    private const string Marker = "[Meal Assistant local fixture v1]";
    private static Guid FixtureId(int number) => Guid.Parse($"5f91e464-e7d3-43fb-9e2d-{number:D12}");

    public static async Task RunAsync(AppDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var connection = LocalDevelopmentDatabase.Verify(db);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await db.Users.AnyAsync(user => user.Id == UserId, cancellationToken))
            throw new InvalidOperationException($"Target user {UserId} does not exist in the local database.");

        var now = clock.GetUtcNow();
        var foods = new[]
        {
            Food(1, "Borscht", FoodMeasurementType.Portion, 20, now),
            Food(2, "Wheat porridge", FoodMeasurementType.Grams, 23, now),
            Food(3, "Fried potatoes", FoodMeasurementType.Grams, 20, now),
            Food(4, "Chicken breast", FoodMeasurementType.Grams, 0, now),
            Food(5, "Four cheese pizza", FoodMeasurementType.Piece, 30, now),
            Food(6, "Shawarma", FoodMeasurementType.Portion, 90, now)
        };
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, clock.LocalTimeZone).DateTime);
        var meals = new[]
        {
            Meal(1, 2, 8, MealType.Breakfast, 4m, 4m, (1, 180m)),
            Meal(2, 5, 13, MealType.Lunch, 5m, 4.5m, (0, 1m), (2, 125m), (3, 150m)),
            Meal(3, 9, 19, MealType.Dinner, 6m, null, (4, 2m)),
            Meal(4, 14, 13, MealType.Lunch, 4.5m, 4m, (0, 1.5m), (2, 85m), (3, 115m)),
            Meal(5, 21, 19, MealType.Dinner, 9m, 8.5m, (5, 1m))
        };
        var foodIds = foods.Select(food => food.Id).ToArray();
        var mealIds = meals.Select(meal => meal.Id).ToArray();
        var existingFoods = await db.FoodItems.AsNoTracking().Where(food => foodIds.Contains(food.Id)).ToListAsync(cancellationToken);
        var existingMeals = await db.Meals.AsNoTracking().Where(meal => mealIds.Contains(meal.Id))
            .Include(meal => meal.Items).ToListAsync(cancellationToken);
        if (existingFoods.Any(food => food.UserId != UserId || !food.Name.StartsWith(Marker, StringComparison.Ordinal))
            || existingMeals.Any(meal => meal.UserId != UserId || meal.Notes != Marker))
            throw new InvalidOperationException("Fixture ID collision. Existing records were left untouched.");

        var newFoods = foods.Where(food => existingFoods.All(existing => existing.Id != food.Id)).ToArray();
        var newMeals = meals.Where(meal => existingMeals.All(existing => existing.Id != meal.Id)).ToArray();
        db.FoodItems.AddRange(newFoods);
        db.Meals.AddRange(newMeals);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        Console.WriteLine($"Target {UserId} | {connection.Host}:{connection.Port}/{connection.Database}");
        Console.WriteLine($"Created {newFoods.Length} foods, {newMeals.Length} meals, {newMeals.Sum(meal => meal.Items.Count)} items.");
        var savedMeals = existingMeals.Concat(newMeals).OrderByDescending(meal => meal.MealTime).ToArray();
        Console.WriteLine($"Fixture meals: {savedMeals.Count(meal => meal.ConfirmedBolus.HasValue)} confirmed, {savedMeals.Count(meal => !meal.ConfirmedBolus.HasValue)} pending.");
        foreach (var meal in savedMeals)
            Console.WriteLine($"{meal.Id} | {meal.MealTime:yyyy-MM-dd HH:mm zzz} | {meal.MealType} | {meal.TotalCarbs} g | {string.Join(", ", meal.Items.Select(item => item.FoodNameSnapshot))}");

        Meal Meal(int number, int daysAgo, int hour, MealType type, decimal suggested, decimal? confirmed,
            params (int FoodIndex, decimal Quantity)[] portions)
        {
            var localTime = today.AddDays(-daysAgo).ToDateTime(new TimeOnly(hour, 0));
            var items = portions.Select(portion =>
            {
                var food = foods[portion.FoodIndex];
                return new MealItem
                {
                    FoodItemId = food.Id,
                    FoodNameSnapshot = food.Name[(Marker.Length + 1)..],
                    Quantity = portion.Quantity,
                    MeasurementType = food.MeasurementType,
                    WeightGrams = food.MeasurementType == FoodMeasurementType.Grams ? portion.Quantity : null,
                    CarbsPer100gSnapshot = food.CarbsPer100g,
                    CarbsPerUnitSnapshot = food.CarbsPerUnit,
                    CalculatedCarbs = FoodCarbCalculator.Calculate(food.MeasurementType, portion.Quantity,
                        food.CarbsPer100g, food.CarbsPerUnit)
                };
            }).ToList();
            return new Meal
            {
                Id = FixtureId(100 + number),
                UserId = UserId,
                MealType = type,
                MealTime = TimeZoneInfo.ConvertTimeToUtc(localTime, clock.LocalTimeZone),
                PreMealGlucose = 6.5m,
                TotalCarbs = items.Sum(item => item.CalculatedCarbs),
                SuggestedBolus = suggested,
                ConfirmedBolus = confirmed,
                Notes = Marker,
                CreatedAt = now,
                Items = items
            };
        }
    }

    private static FoodItem Food(int number, string name, FoodMeasurementType unit, decimal carbs, DateTimeOffset now) =>
        new()
        {
            Id = FixtureId(number),
            UserId = UserId,
            Name = $"{Marker} {name}",
            MeasurementType = unit,
            CarbsPer100g = unit == FoodMeasurementType.Grams ? carbs : null,
            CarbsPerUnit = unit == FoodMeasurementType.Grams ? null : carbs,
            CreatedAt = now
        };
}
