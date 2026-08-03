using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class MealCalculationService(IAppDbContext db)
{
    public async Task<MealCalculationDto> CalculateMealAsync(CalculateMealRequest request, CancellationToken cancellationToken)
    {
        ValidateMealInputs(request.PreMealGlucose, request.Items, request.DirectCarbs);

        var settings = await GetSettingsAsync(cancellationToken);
        var calculatedItems = await CalculateItemsAsync(request.Items, request.DirectCarbs, request.DirectFoodName, cancellationToken);
        var totalCarbs = Math.Round(calculatedItems.Sum(i => i.CalculatedCarbs), 2);
        var (mealBolus, correctionBolus, suggestedBolus) = CalculateBolus(totalCarbs, request.PreMealGlucose, settings);

        return new MealCalculationDto(totalCarbs, mealBolus, correctionBolus, suggestedBolus, calculatedItems);
    }

    public async Task<IReadOnlyList<CalculatedMealItemDto>> CalculateItemsAsync(IReadOnlyList<MealItemInputDto> inputItems, decimal? directCarbs, string? directFoodName, CancellationToken cancellationToken)
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

    public async Task<MealTotalsResult> CalculateTotalsAsync(decimal totalCarbs, decimal preMealGlucose, CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var roundedTotalCarbs = Math.Round(totalCarbs, 2);
        var (_, _, suggestedBolus) = CalculateBolus(roundedTotalCarbs, preMealGlucose, settings);
        return new MealTotalsResult(roundedTotalCarbs, suggestedBolus);
    }

    private async Task<DiabetesSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        await db.DiabetesSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == DefaultUser.Id, cancellationToken)
        ?? throw new NotFoundException("Diabetes settings", DefaultUser.Id);

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
}

public sealed record MealTotalsResult(decimal TotalCarbs, decimal SuggestedBolus);