using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Calculations;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class MealService(IAppDbContext db, TimeProvider timeProvider, MealCalculationService mealCalculationService)
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

    public Task<MealCalculationDto> CalculateMealAsync(CalculateMealRequest request, CancellationToken cancellationToken) =>
        mealCalculationService.CalculateMealAsync(request, cancellationToken);

    public async Task<MealDetailDto> CreateMealAsync(CreateMealRequest request, CancellationToken cancellationToken)
    {
        if (request.ConfirmedBolus < 0)
        {
            throw new ValidationException("Confirmed bolus cannot be negative.");
        }

        var calculation = await CalculateMealAsync(new CalculateMealRequest(request.MealType, request.PreMealGlucose, request.Items, request.DirectCarbs, request.DirectFoodName, request.CarbAdjustment), cancellationToken);
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
            CarbAdjustment = calculation.CarbAdjustment,
            SuggestedBolus = calculation.SuggestedBolus,
            ConfirmedBolus = request.ConfirmedBolus,
            Notes = request.Notes,
            CreatedAt = now,
            Items = calculation.Items.Select(item => new MealItem
            {
                Id = Guid.NewGuid(),
                FoodItemId = item.FoodItemId,
                FoodNameSnapshot = item.FoodName,
                Quantity = item.Quantity,
                MeasurementType = item.MeasurementType,
                WeightGrams = item.WeightGrams,
                CarbsPer100gSnapshot = item.CarbsPer100g,
                CarbsPerUnitSnapshot = item.CarbsPerUnit,
                CalculatedCarbs = item.CalculatedCarbs,
                CarbOverride = item.CarbOverride
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

        if (request.Items.Any(i => ResolveQuantity(i) <= 0))
        {
            throw new ValidationException("Food quantities must be greater than zero.");
        }

        var meal = await db.Meals
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new NotFoundException("Meal", id);

        if (meal.ConfirmedBolus is not null)
        {
            throw new ValidationException("Cannot add food after the insulin dose has been confirmed.");
        }

        var existingCarbs = meal.Items.Sum(EffectiveCarbs);
        var calculatedItems = await mealCalculationService.CalculateItemsAsync(request.Items, directCarbs: null, directFoodName: null, cancellationToken);
        var newItems = calculatedItems
            .Select(item => new MealItem
            {
                Id = Guid.NewGuid(),
                MealId = meal.Id,
                FoodItemId = item.FoodItemId,
                FoodNameSnapshot = item.FoodName,
                Quantity = item.Quantity,
                MeasurementType = item.MeasurementType,
                WeightGrams = item.WeightGrams,
                CarbsPer100gSnapshot = item.CarbsPer100g,
                CarbsPerUnitSnapshot = item.CarbsPerUnit,
                CalculatedCarbs = item.CalculatedCarbs,
                CarbOverride = item.CarbOverride
            })
            .ToList();

        db.MealItems.AddRange(newItems);

        await RecalculateMealTotalsAsync(meal, existingCarbs + newItems.Sum(EffectiveCarbs), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await GetMealAsync(id, cancellationToken);
    }

    public async Task<MealDetailDto> UpdateMealItemAsync(Guid mealId, Guid itemId, UpdateMealItemRequest request, CancellationToken cancellationToken)
    {
        var quantity = ResolveQuantity(request);
        if (quantity <= 0)
        {
            throw new ValidationException("Food quantity must be greater than zero.");
        }

        var meal = await GetEditableMealAsync(mealId, cancellationToken);
        var item = meal.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException("Meal item", itemId);

        if (item.MeasurementType == FoodMeasurementType.Piece && quantity != decimal.Truncate(quantity))
        {
            throw new ValidationException("Piece quantity must be a whole number.");
        }

        item.Quantity = quantity;
        item.WeightGrams = item.MeasurementType == FoodMeasurementType.Grams ? quantity : null;
        if (request.CarbOverride is < 0)
        {
            throw new ValidationException("Carb override must be zero or greater.");
        }

        item.CalculatedCarbs = FoodCarbCalculator.Calculate(item.MeasurementType, item.Quantity, item.CarbsPer100gSnapshot, item.CarbsPerUnitSnapshot);
        item.CarbOverride = request.CarbOverride;

        await RecalculateMealTotalsAsync(meal, meal.Items.Sum(EffectiveCarbs), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await GetMealAsync(mealId, cancellationToken);
    }

    public async Task<MealDetailDto> RemoveMealItemAsync(Guid mealId, Guid itemId, CancellationToken cancellationToken)
    {
        var meal = await GetEditableMealAsync(mealId, cancellationToken);
        var item = meal.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException("Meal item", itemId);

        if (meal.Items.Count <= 1)
        {
            throw new ValidationException("A meal must have at least one food item.");
        }

        db.MealItems.Remove(item);
        await RecalculateMealTotalsAsync(meal, meal.Items.Where(i => i.Id != itemId).Sum(EffectiveCarbs), cancellationToken);

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
            var pattern = $"%{EscapeLikePattern(search.Trim())}%";
            query = query.Where(m => m.Items.Any(i => EF.Functions.ILike(i.FoodNameSnapshot, pattern, "\\")));
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
            ?? throw new NotFoundException("Meal", id);

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
            ?? throw new NotFoundException("Meal", id);

        meal.ConfirmedBolus = request.ConfirmedBolus;
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(meal);
    }

    public async Task<MealDetailDto> ClearConfirmedBolusAsync(Guid id, CancellationToken cancellationToken)
    {
        var meal = await db.Meals
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new NotFoundException("Meal", id);

        meal.ConfirmedBolus = null;
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(meal);
    }

    private async Task<Meal> GetEditableMealAsync(Guid id, CancellationToken cancellationToken)
    {
        var meal = await db.Meals
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new NotFoundException("Meal", id);

        if (meal.ConfirmedBolus is not null)
        {
            throw new ValidationException("Cannot edit food after the insulin dose has been confirmed.");
        }

        return meal;
    }

    private async Task RecalculateMealTotalsAsync(Meal meal, decimal foodCarbs, CancellationToken cancellationToken)
    {
        var totals = await mealCalculationService.CalculateTotalsAsync(foodCarbs, meal.CarbAdjustment, meal.PreMealGlucose, cancellationToken);
        meal.TotalCarbs = totals.TotalCarbs;
        meal.SuggestedBolus = totals.SuggestedBolus;
        meal.ConfirmedBolus = null;
    }

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

    private static MealSummaryDto ToSummary(Meal meal) =>
        new(meal.Id, meal.MealType, meal.MealTime, meal.PreMealGlucose, meal.TotalCarbs, meal.CarbAdjustment, meal.SuggestedBolus, meal.ConfirmedBolus, meal.Notes, meal.Items.Select(i => i.FoodNameSnapshot).ToList());

    private static MealDetailDto ToDetail(Meal meal) =>
        new(meal.Id, meal.MealType, meal.MealTime, meal.PreMealGlucose, meal.TotalCarbs, meal.CarbAdjustment, meal.SuggestedBolus, meal.ConfirmedBolus, meal.Notes, meal.CreatedAt,
            meal.Items.OrderBy(i => i.FoodNameSnapshot).Select(i => new MealItemDto(
                i.Id,
                i.FoodItemId,
                i.FoodNameSnapshot,
                ResolveQuantity(i),
                i.MeasurementType,
                i.WeightGrams,
                i.CarbsPer100gSnapshot,
                i.CarbsPerUnitSnapshot,
                i.CalculatedCarbs,
                i.CarbOverride,
                EffectiveCarbs(i))).ToList());

    private static decimal EffectiveCarbs(MealItem item) =>
        item.CarbOverride ?? item.CalculatedCarbs;

    private static decimal ResolveQuantity(MealItemInputDto item) =>
        item.Quantity ?? item.WeightGrams ?? 0;

    private static decimal ResolveQuantity(UpdateMealItemRequest request) =>
        request.Quantity ?? request.WeightGrams ?? 0;

    private static decimal ResolveQuantity(MealItem item) =>
        item.Quantity > 0 ? item.Quantity : item.WeightGrams ?? 0;

    private DateTimeOffset GetLocalDayStartUtc(DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, timeProvider.LocalTimeZone), TimeSpan.Zero);
    }
}

