using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Calculations;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Calculations;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class MealCalculationService(IAppDbContext db)
{
    public async Task<MealCalculationDto> CalculateMealAsync(CalculateMealRequest request, CancellationToken cancellationToken)
    {
        ValidateMealInputs(request.PreMealGlucose, request.Items, request.DirectCarbs, request.CarbAdjustment);

        var settings = await GetSettingsAsync(cancellationToken);
        var calculatedItems = await CalculateItemsAsync(request.Items, request.DirectCarbs, request.DirectFoodName, cancellationToken);
        var foodCarbs = Math.Round(calculatedItems.Sum(i => i.EffectiveCarbs), 2);
        var totalCarbs = CalculateFinalCarbs(foodCarbs, request.CarbAdjustment);
        var mealBolus = BolusCalculator.CalculateFoodBolus(totalCarbs, settings.CarbRatio);
        var correctionBolus = BolusCalculator.CalculateCorrectionBolus(request.PreMealGlucose, settings.TargetGlucose, settings.CorrectionFactor);
        var suggestedBolus = BolusCalculator.CalculateTotalBolus(mealBolus, correctionBolus);

        return new MealCalculationDto(foodCarbs, Math.Round(request.CarbAdjustment, 2), totalCarbs, mealBolus, correctionBolus, suggestedBolus, calculatedItems);
    }

    public async Task<IReadOnlyList<CalculatedMealItemDto>> CalculateItemsAsync(IReadOnlyList<MealItemInputDto> inputItems, decimal? directCarbs, string? directFoodName, CancellationToken cancellationToken)
    {
        if (directCarbs.HasValue)
        {
            return
            [
                CreateCalculatedItem(
                    Guid.Empty,
                    string.IsNullOrWhiteSpace(directFoodName) ? "Delivery meal" : directFoodName.Trim(),
                    1,
                    FoodMeasurementType.Portion,
                    null,
                    null,
                    null,
                    Math.Round(directCarbs.Value, 2),
                    null)
            ];
        }

        var currentFoodInputs = inputItems.Where(input => input.MeasurementType is null).ToList();
        var foodIds = currentFoodInputs.Select(i => i.FoodItemId).Distinct().ToList();
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
            ValidateCarbOverride(input.CarbOverride);

            if (input.MeasurementType is { } snapshotMeasurementType)
            {
                var snapshotQuantity = ResolveQuantity(input);
                ValidateSnapshotInput(input, snapshotMeasurementType, snapshotQuantity);
                var snapshotCalculatedCarbs = FoodCarbCalculator.Calculate(
                    snapshotMeasurementType,
                    snapshotQuantity,
                    input.CarbsPer100gSnapshot,
                    input.CarbsPerUnitSnapshot);

                return CreateCalculatedItem(
                    input.FoodItemId,
                    string.IsNullOrWhiteSpace(input.FoodNameSnapshot) ? "Saved food" : input.FoodNameSnapshot.Trim(),
                    snapshotQuantity,
                    snapshotMeasurementType,
                    snapshotMeasurementType == FoodMeasurementType.Grams ? snapshotQuantity : null,
                    input.CarbsPer100gSnapshot,
                    input.CarbsPerUnitSnapshot,
                    snapshotCalculatedCarbs,
                    input.CarbOverride);
            }

            var food = foods[input.FoodItemId];
            var quantity = ResolveQuantity(input);
            ValidateQuantity(food.MeasurementType, quantity);
            var calculatedCarbs = FoodCarbCalculator.Calculate(food.MeasurementType, quantity, food.CarbsPer100g, food.CarbsPerUnit);
            return CreateCalculatedItem(
                food.Id,
                food.Name,
                quantity,
                food.MeasurementType,
                food.MeasurementType == FoodMeasurementType.Grams ? quantity : null,
                food.CarbsPer100g,
                food.CarbsPerUnit,
                calculatedCarbs,
                input.CarbOverride);
        }).ToList();
    }

    public async Task<MealTotalsResult> CalculateTotalsAsync(decimal foodCarbs, decimal carbAdjustment, decimal preMealGlucose, CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var roundedFoodCarbs = Math.Round(foodCarbs, 2);
        var totalCarbs = CalculateFinalCarbs(roundedFoodCarbs, carbAdjustment);
        var mealBolus = BolusCalculator.CalculateFoodBolus(totalCarbs, settings.CarbRatio);
        var correctionBolus = BolusCalculator.CalculateCorrectionBolus(preMealGlucose, settings.TargetGlucose, settings.CorrectionFactor);
        var suggestedBolus = BolusCalculator.CalculateTotalBolus(mealBolus, correctionBolus);
        return new MealTotalsResult(totalCarbs, suggestedBolus);
    }

    private static CalculatedMealItemDto CreateCalculatedItem(
        Guid foodItemId,
        string foodName,
        decimal quantity,
        FoodMeasurementType measurementType,
        decimal? weightGrams,
        decimal? carbsPer100g,
        decimal? carbsPerUnit,
        decimal calculatedCarbs,
        decimal? carbOverride)
    {
        var roundedCalculatedCarbs = Math.Round(calculatedCarbs, 2);
        decimal? roundedOverride = carbOverride.HasValue ? Math.Round(carbOverride.Value, 2) : null;
        var effectiveCarbs = roundedOverride ?? roundedCalculatedCarbs;

        return new CalculatedMealItemDto(
            foodItemId,
            foodName,
            quantity,
            measurementType,
            weightGrams,
            carbsPer100g,
            carbsPerUnit,
            roundedCalculatedCarbs,
            roundedOverride,
            effectiveCarbs);
    }

    private async Task<DiabetesSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        await db.DiabetesSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == DefaultUser.Id, cancellationToken)
        ?? throw new NotFoundException("Diabetes settings", DefaultUser.Id);

    private static void ValidateMealInputs(decimal preMealGlucose, IReadOnlyList<MealItemInputDto> items, decimal? directCarbs, decimal carbAdjustment)
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

            if (directCarbs.Value + carbAdjustment < 0)
            {
                throw new ValidationException("Final meal carbs cannot be negative.");
            }

            return;
        }

        if (items.Count == 0)
        {
            throw new ValidationException("Add at least one food item.");
        }

        if (items.Any(i => ResolveQuantity(i) <= 0))
        {
            throw new ValidationException("Food quantities must be greater than zero.");
        }
    }

    private static decimal CalculateFinalCarbs(decimal foodCarbs, decimal carbAdjustment)
    {
        var totalCarbs = Math.Round(foodCarbs + carbAdjustment, 2);
        if (totalCarbs < 0)
        {
            throw new ValidationException("Final meal carbs cannot be negative.");
        }

        return totalCarbs;
    }

    private static decimal ResolveQuantity(MealItemInputDto item) =>
        item.Quantity ?? item.WeightGrams ?? 0;

    private static void ValidateCarbOverride(decimal? carbOverride)
    {
        if (carbOverride is < 0)
        {
            throw new ValidationException("Carb override must be zero or greater.");
        }
    }

    private static void ValidateQuantity(FoodMeasurementType measurementType, decimal quantity)
    {
        if (measurementType == FoodMeasurementType.Piece && quantity != decimal.Truncate(quantity))
        {
            throw new ValidationException("Piece quantity must be a whole number.");
        }
    }

    private static void ValidateSnapshotInput(MealItemInputDto input, FoodMeasurementType measurementType, decimal quantity)
    {
        ValidateQuantity(measurementType, quantity);

        switch (measurementType)
        {
            case FoodMeasurementType.Grams:
                if (input.CarbsPer100gSnapshot is null or < 0)
                {
                    throw new ValidationException("Saved carbs per 100 g must be zero or greater.");
                }

                break;
            case FoodMeasurementType.Portion:
            case FoodMeasurementType.Piece:
                if (input.CarbsPerUnitSnapshot is null or < 0)
                {
                    throw new ValidationException("Saved carbs per unit must be zero or greater.");
                }

                break;
            default:
                throw new ValidationException("Measurement type is not supported.");
        }
    }
}

public sealed record MealTotalsResult(decimal TotalCarbs, decimal SuggestedBolus);

