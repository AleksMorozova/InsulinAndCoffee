using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class MealService(IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);
        var todayStartUtc = GetLocalDayStartUtc(today);
        var tomorrowStartUtc = GetLocalDayStartUtc(today.AddDays(1));

        var meals = await db.Meals
            .AsNoTracking()
            .Where(m => m.UserId == DefaultUser.Id && m.CreatedAt >= todayStartUtc && m.CreatedAt < tomorrowStartUtc)
            .OrderByDescending(m => m.MealTime)
            .Select(m => new DashboardMealDto(
                m.Id,
                m.MealType,
                m.MealTime,
                m.CreatedAt,
                m.TotalCarbs,
                m.ConfirmedBolus,
                m.ConfirmedBolus == null))
            .ToListAsync(cancellationToken);

        return new DashboardDto(
            today,
            meals.Sum(m => m.TotalCarbs),
            meals.Sum(m => m.ConfirmedInsulin ?? 0),
            meals.Count,
            meals);
    }

    public async Task<MealCalculationDto> CalculateMealAsync(CalculateMealRequest request, CancellationToken cancellationToken)
    {
        ValidateMealInputs(request.PreMealGlucose, request.Items, request.DirectCarbs);

        var settings = await db.DiabetesSettings.AsNoTracking().FirstAsync(s => s.UserId == DefaultUser.Id, cancellationToken);
        var calculatedItems = await CalculateItemsAsync(request.Items, request.DirectCarbs, request.DirectFoodName, cancellationToken);
        var totalCarbs = Math.Round(calculatedItems.Sum(i => i.CalculatedCarbs), 2);
        var (mealBolus, correctionBolus, suggestedBolus) = CalculateBolus(totalCarbs, request.PreMealGlucose, settings);

        return new MealCalculationDto(totalCarbs, mealBolus, correctionBolus, suggestedBolus, calculatedItems);
    }

    public async Task<MealDetailDto> CreateMealAsync(CreateMealRequest request, CancellationToken cancellationToken)
    {
        if (request.ConfirmedBolus < 0)
        {
            throw new ValidationException("Confirmed bolus cannot be negative.");
        }

        var calculation = await CalculateMealAsync(new CalculateMealRequest(request.MealType, request.PreMealGlucose, request.Items, request.DirectCarbs, request.DirectFoodName), cancellationToken);
        var now = timeProvider.GetUtcNow();
        var mealTime = request.MealTime ?? now;
        var meal = new Meal
        {
            Id = Guid.NewGuid(),
            UserId = DefaultUser.Id,
            MealType = request.MealType,
            MealTime = mealTime,
            PreMealGlucose = request.PreMealGlucose,
            TotalCarbs = calculation.TotalCarbs,
            SuggestedBolus = calculation.SuggestedBolus,
            ConfirmedBolus = request.ConfirmedBolus,
            Notes = request.Notes,
            CreatedAt = now,
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
                    ReadingTime = mealTime,
                    ReadingType = ReadingType.BeforeMeal,
                    Notes = "Captured with meal"
                }
            ]
        };

        db.Meals.Add(meal);
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(meal);
    }

    public async Task<MealDetailDto> AddMealItemsAsync(Guid id, AddMealItemsRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            throw new ValidationException("Add at least one food item.");
        }

        if (request.Items.Any(i => i.WeightGrams <= 0))
        {
            throw new ValidationException("Food weights must be greater than zero.");
        }

        var meal = await db.Meals
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Meal was not found.");

        if (meal.ConfirmedBolus is not null)
        {
            throw new ValidationException("Cannot add food after the insulin dose has been confirmed.");
        }

        var existingCarbs = meal.Items.Sum(i => i.CalculatedCarbs);
        var calculatedItems = await CalculateItemsAsync(request.Items, directCarbs: null, directFoodName: null, cancellationToken);
        var newItems = calculatedItems
            .Select(item => new MealItem
            {
                Id = Guid.NewGuid(),
                MealId = meal.Id,
                FoodItemId = item.FoodItemId,
                FoodNameSnapshot = item.FoodName,
                WeightGrams = item.WeightGrams,
                CarbsPer100gSnapshot = item.CarbsPer100g,
                CalculatedCarbs = item.CalculatedCarbs
            })
            .ToList();

        db.MealItems.AddRange(newItems);

        RecalculateMealTotals(meal, existingCarbs + newItems.Sum(i => i.CalculatedCarbs), await GetSettingsAsync(cancellationToken));

        await db.SaveChangesAsync(cancellationToken);
        return await GetMealAsync(id, cancellationToken);
    }

    public async Task<MealDetailDto> UpdateMealItemAsync(Guid mealId, Guid itemId, UpdateMealItemRequest request, CancellationToken cancellationToken)
    {
        if (request.WeightGrams <= 0)
        {
            throw new ValidationException("Food weight must be greater than zero.");
        }

        var meal = await GetEditableMealAsync(mealId, cancellationToken);
        var item = meal.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new KeyNotFoundException("Meal item was not found.");

        item.WeightGrams = request.WeightGrams;
        item.CalculatedCarbs = Math.Round(item.WeightGrams * item.CarbsPer100gSnapshot / 100, 2);

        RecalculateMealTotals(meal, meal.Items.Sum(i => i.CalculatedCarbs), await GetSettingsAsync(cancellationToken));

        await db.SaveChangesAsync(cancellationToken);
        return await GetMealAsync(mealId, cancellationToken);
    }

    public async Task<MealDetailDto> RemoveMealItemAsync(Guid mealId, Guid itemId, CancellationToken cancellationToken)
    {
        var meal = await GetEditableMealAsync(mealId, cancellationToken);
        var item = meal.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new KeyNotFoundException("Meal item was not found.");

        if (meal.Items.Count <= 1)
        {
            throw new ValidationException("A meal must have at least one food item.");
        }

        db.MealItems.Remove(item);
        RecalculateMealTotals(meal, meal.Items.Where(i => i.Id != itemId).Sum(i => i.CalculatedCarbs), await GetSettingsAsync(cancellationToken));

        await db.SaveChangesAsync(cancellationToken);
        return await GetMealAsync(mealId, cancellationToken);
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

    public async Task<MealDetailDto> ConfirmMealBolusAsync(Guid id, ConfirmMealBolusRequest request, CancellationToken cancellationToken)
    {
        if (request.ConfirmedBolus < 0)
        {
            throw new ValidationException("Confirmed bolus cannot be negative.");
        }

        var meal = await db.Meals
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Meal was not found.");

        meal.ConfirmedBolus = request.ConfirmedBolus;
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(meal);
    }

    private async Task<Meal> GetEditableMealAsync(Guid id, CancellationToken cancellationToken)
    {
        var meal = await db.Meals
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Meal was not found.");

        if (meal.ConfirmedBolus is not null)
        {
            throw new ValidationException("Cannot edit food after the insulin dose has been confirmed.");
        }

        return meal;
    }

    private async Task<DiabetesSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        await db.DiabetesSettings.AsNoTracking().FirstAsync(s => s.UserId == DefaultUser.Id, cancellationToken);

    private async Task<IReadOnlyList<CalculatedMealItemDto>> CalculateItemsAsync(IReadOnlyList<MealItemInputDto> inputItems, decimal? directCarbs, string? directFoodName, CancellationToken cancellationToken)
    {
        if (directCarbs.HasValue)
        {
            return
            [
                new CalculatedMealItemDto(
                    Guid.Empty,
                    string.IsNullOrWhiteSpace(directFoodName) ? "Delivery meal" : directFoodName.Trim(),
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

    private static (decimal MealBolus, decimal CorrectionBolus, decimal SuggestedBolus) CalculateBolus(decimal totalCarbs, decimal preMealGlucose, DiabetesSettings settings)
    {
        var mealBolus = Math.Round(totalCarbs / settings.CarbRatio, 2);
        var correctionBolus = preMealGlucose > settings.TargetGlucose
            ? Math.Round((preMealGlucose - settings.TargetGlucose) / settings.CorrectionFactor, 2)
            : 0;
        var suggestedBolus = Math.Round(mealBolus + correctionBolus, 2);

        return (mealBolus, correctionBolus, suggestedBolus);
    }

    private static void RecalculateMealTotals(Meal meal, decimal totalCarbs, DiabetesSettings settings)
    {
        meal.TotalCarbs = Math.Round(totalCarbs, 2);
        var (_, _, suggestedBolus) = CalculateBolus(meal.TotalCarbs, meal.PreMealGlucose, settings);
        meal.SuggestedBolus = suggestedBolus;
        meal.ConfirmedBolus = null;
    }

    private static MealSummaryDto ToSummary(Meal meal) =>
        new(meal.Id, meal.MealType, meal.MealTime, meal.PreMealGlucose, meal.TotalCarbs, meal.SuggestedBolus, meal.ConfirmedBolus, meal.Notes, meal.Items.Select(i => i.FoodNameSnapshot).ToList());

    private static MealDetailDto ToDetail(Meal meal) =>
        new(meal.Id, meal.MealType, meal.MealTime, meal.PreMealGlucose, meal.TotalCarbs, meal.SuggestedBolus, meal.ConfirmedBolus, meal.Notes, meal.CreatedAt,
            meal.Items.OrderBy(i => i.FoodNameSnapshot).Select(i => new MealItemDto(i.Id, i.FoodItemId, i.FoodNameSnapshot, i.WeightGrams, i.CarbsPer100gSnapshot, i.CalculatedCarbs)).ToList());

    private DateTimeOffset GetLocalDayStartUtc(DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, timeProvider.LocalTimeZone), TimeSpan.Zero);
    }
}
