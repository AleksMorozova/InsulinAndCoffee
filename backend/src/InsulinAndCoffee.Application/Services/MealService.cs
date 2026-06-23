using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class MealService(IAppDbContext db)
{
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var tomorrow = today.AddDays(1);

        var todaysMeals = await db.Meals
            .AsNoTracking()
            .Where(m => m.UserId == DefaultUser.Id && m.MealTime >= today && m.MealTime < tomorrow)
            .ToListAsync(cancellationToken);

        var lastMeal = await db.Meals
            .AsNoTracking()
            .Include(m => m.Items)
            .Where(m => m.UserId == DefaultUser.Id)
            .OrderByDescending(m => m.MealTime)
            .FirstOrDefaultAsync(cancellationToken);

        return new DashboardDto(
            todaysMeals.Sum(m => m.TotalCarbs),
            todaysMeals.Sum(m => m.ConfirmedBolus),
            lastMeal is null ? null : ToSummary(lastMeal));
    }

    public async Task<MealCalculationDto> CalculateMealAsync(CalculateMealRequest request, CancellationToken cancellationToken)
    {
        ValidateMealInputs(request.PreMealGlucose, request.Items, request.DirectCarbs);

        var settings = await db.DiabetesSettings.AsNoTracking().FirstAsync(s => s.UserId == DefaultUser.Id, cancellationToken);
        var calculatedItems = await CalculateItemsAsync(request.Items, request.DirectCarbs, request.DirectFoodName, cancellationToken);
        var totalCarbs = Math.Round(calculatedItems.Sum(i => i.CalculatedCarbs), 2);
        var mealBolus = Math.Round(totalCarbs / settings.CarbRatio, 2);
        var correctionBolus = request.PreMealGlucose > settings.TargetGlucose
            ? Math.Round((request.PreMealGlucose - settings.TargetGlucose) / settings.CorrectionFactor, 2)
            : 0;
        var suggestedBolus = Math.Round(mealBolus + correctionBolus, 2);

        return new MealCalculationDto(totalCarbs, mealBolus, correctionBolus, suggestedBolus, calculatedItems);
    }

    public async Task<MealDetailDto> CreateMealAsync(CreateMealRequest request, CancellationToken cancellationToken)
    {
        if (request.ConfirmedBolus < 0)
        {
            throw new ValidationException("Confirmed bolus cannot be negative.");
        }

        var calculation = await CalculateMealAsync(new CalculateMealRequest(request.MealType, request.PreMealGlucose, request.Items, request.DirectCarbs, request.DirectFoodName), cancellationToken);
        var meal = new Meal
        {
            Id = Guid.NewGuid(),
            UserId = DefaultUser.Id,
            MealType = request.MealType,
            MealTime = request.MealTime ?? DateTimeOffset.UtcNow,
            PreMealGlucose = request.PreMealGlucose,
            TotalCarbs = calculation.TotalCarbs,
            SuggestedBolus = calculation.SuggestedBolus,
            ConfirmedBolus = request.ConfirmedBolus,
            Notes = request.Notes,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = calculation.Items.Select(item => new MealItem
            {
                Id = Guid.NewGuid(),
                FoodItemId = item.FoodItemId,
                FoodNameSnapshot = item.FoodName,
                WeightGrams = item.WeightGrams,
                CarbsPer100gSnapshot = item.CarbsPer100g,
                CalculatedCarbs = item.CalculatedCarbs
            }).ToList(),
            GlucoseReadings =
            [
                new GlucoseReading
                {
                    Id = Guid.NewGuid(),
                    UserId = DefaultUser.Id,
                    Value = request.PreMealGlucose,
                    ReadingTime = request.MealTime ?? DateTimeOffset.UtcNow,
                    ReadingType = ReadingType.BeforeMeal,
                    Notes = "Captured with meal"
                }
            ]
        };

        db.Meals.Add(meal);
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(meal);
    }

    public async Task<IReadOnlyList<MealSummaryDto>> GetMealsAsync(string? search, MealType? mealType, CancellationToken cancellationToken)
    {
        var query = db.Meals
            .AsNoTracking()
            .Include(m => m.Items)
            .Where(m => m.UserId == DefaultUser.Id);

        if (mealType.HasValue)
        {
            query = query.Where(m => m.MealType == mealType.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(m => m.Items.Any(i => i.FoodNameSnapshot.ToLower().Contains(term)));
        }

        return await query
            .OrderByDescending(m => m.MealTime)
            .Select(m => ToSummary(m))
            .ToListAsync(cancellationToken);
    }

    public async Task<MealDetailDto> GetMealAsync(Guid id, CancellationToken cancellationToken)
    {
        var meal = await db.Meals
            .AsNoTracking()
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Meal was not found.");

        return ToDetail(meal);
    }

    private async Task<IReadOnlyList<CalculatedMealItemDto>> CalculateItemsAsync(IReadOnlyList<MealItemInputDto> inputItems, decimal? directCarbs, string? directFoodName, CancellationToken cancellationToken)
    {
        if (directCarbs.HasValue)
        {
            return
            [
                new CalculatedMealItemDto(
                    Guid.Empty,
                    string.IsNullOrWhiteSpace(directFoodName) ? "Ask Past Me meal" : directFoodName.Trim(),
                    100,
                    directCarbs.Value,
                    Math.Round(directCarbs.Value, 2))
            ];
        }

        var foodIds = inputItems.Select(i => i.FoodItemId).Distinct().ToList();
        var foods = await db.FoodItems
            .AsNoTracking()
            .Where(f => f.UserId == DefaultUser.Id && foodIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        if (foods.Count != foodIds.Count)
        {
            throw new ValidationException("One or more selected foods were not found.");
        }

        return inputItems.Select(input =>
        {
            var food = foods[input.FoodItemId];
            var calculatedCarbs = Math.Round(input.WeightGrams * food.CarbsPer100g / 100, 2);
            return new CalculatedMealItemDto(food.Id, food.Name, input.WeightGrams, food.CarbsPer100g, calculatedCarbs);
        }).ToList();
    }

    private static void ValidateMealInputs(decimal preMealGlucose, IReadOnlyList<MealItemInputDto> items, decimal? directCarbs)
    {
        if (preMealGlucose <= 0)
        {
            throw new ValidationException("Pre-meal glucose must be greater than zero.");
        }

        if (directCarbs.HasValue)
        {
            if (directCarbs <= 0)
            {
                throw new ValidationException("Direct carbs must be greater than zero.");
            }

            return;
        }

        if (items.Count == 0)
        {
            throw new ValidationException("Add at least one food item.");
        }

        if (items.Any(i => i.WeightGrams <= 0))
        {
            throw new ValidationException("Food weights must be greater than zero.");
        }
    }

    private static MealSummaryDto ToSummary(Meal meal) =>
        new(meal.Id, meal.MealType, meal.MealTime, meal.PreMealGlucose, meal.TotalCarbs, meal.SuggestedBolus, meal.ConfirmedBolus, meal.Notes, meal.Items.Select(i => i.FoodNameSnapshot).ToList());

    private static MealDetailDto ToDetail(Meal meal) =>
        new(meal.Id, meal.MealType, meal.MealTime, meal.PreMealGlucose, meal.TotalCarbs, meal.SuggestedBolus, meal.ConfirmedBolus, meal.Notes, meal.CreatedAt,
            meal.Items.OrderBy(i => i.FoodNameSnapshot).Select(i => new MealItemDto(i.Id, i.FoodItemId, i.FoodNameSnapshot, i.WeightGrams, i.CarbsPer100gSnapshot, i.CalculatedCarbs)).ToList());
}
