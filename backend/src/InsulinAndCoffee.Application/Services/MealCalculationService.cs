using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Calculations;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Calculations;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class MealCalculationService(IAppDbContext db, ICurrentUser? currentUser = null, TimeProvider? timeProvider = null)
{
    private Guid UserId => currentUser?.UserId ?? DefaultUser.Id;
    public async Task<MealCarbPreview> PreviewCarbsAsync(MealCarbPreviewRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count is < 1 or > 100 || request.Items.Any(item => ResolveQuantity(item) <= 0))
            throw new ValidationException("Enter a positive quantity for each selected food.");
        var items = await CalculateItemsAsync(request.Items, null, null, cancellationToken);
        var foodCarbs = Math.Round(items.Sum(item => item.EffectiveCarbs), 2);
        return new MealCarbPreview(foodCarbs, CalculateFinalCarbs(foodCarbs, request.CarbAdjustment), items);
    }

    public async Task<MealCalculationDto> CalculateMealAsync(CalculateMealRequest request, CancellationToken cancellationToken)
    {
        ValidateMealInputs(request.PreMealGlucose, request.Items, request.DirectCarbs, request.CarbAdjustment);

        var settings = await GetSettingsAsync(cancellationToken);
        var calculatedItems = await CalculateItemsAsync(request.Items, request.DirectCarbs, request.DirectFoodName, cancellationToken, request.SourceDeliveryMealId);
        var foodCarbs = Math.Round(calculatedItems.Sum(i => i.EffectiveCarbs), 2);
        var totalCarbs = CalculateFinalCarbs(foodCarbs, request.CarbAdjustment);
        var mealBolus = BolusCalculator.CalculateFoodBolus(totalCarbs, settings.CarbRatio);
        var correctionBolus = BolusCalculator.CalculateCorrectionBolus(request.PreMealGlucose, settings.TargetGlucose, settings.CorrectionFactor);
        var suggestedBolus = BolusCalculator.CalculateTotalBolus(mealBolus, correctionBolus);

        return new MealCalculationDto(foodCarbs, Math.Round(request.CarbAdjustment, 2), totalCarbs, mealBolus, correctionBolus, suggestedBolus, calculatedItems);
    }

    public async Task<IReadOnlyList<CalculatedMealItemDto>> CalculateItemsAsync(IReadOnlyList<MealItemInputDto> inputItems, decimal? directCarbs, string? directFoodName, CancellationToken cancellationToken, Guid? sourceDeliveryMealId = null)
    {
        if (directCarbs.HasValue)
        {
            var provenance = new CarbProvenanceDto(CarbReferenceSource.Manual, CarbTransformation.None);
            if (sourceDeliveryMealId is Guid orderId)
            {
                var order = await db.DeliveryMeals.AsNoTracking().SingleOrDefaultAsync(order => order.Id == orderId && order.UserId == UserId, cancellationToken)
                    ?? throw new ValidationException("The saved order reference is unavailable.");
                if (directCarbs.Value == order.Carbs) provenance = CarbProvenance.Of(order) with { Transformation = order.CarbTransformation | CarbTransformation.HistoricalReuse };
            }
            return
            [
                CreateCalculatedItem(
                    Guid.Empty,
                    string.IsNullOrWhiteSpace(directFoodName) ? "Delivery meal" : directFoodName.Trim(),
                    1,
                    FoodMeasurementType.Portion,
                    null,
                    null,
                    Math.Round(directCarbs.Value, 2),
                    Math.Round(directCarbs.Value, 2),
                    null) with { Provenance = provenance, SourceDeliveryMealId = sourceDeliveryMealId }
            ];
        }

        if (inputItems.Any(input => new[] { input.SourceMealItemId, input.SourceDeliveryMealId, input.SourceDeliveryMealItemId }.Count(id => id.HasValue) > 1))
            throw new ValidationException("Choose a single saved source for each food value.");
        var deliveryItemIds = inputItems.Where(input => input.SourceDeliveryMealItemId.HasValue).Select(input => input.SourceDeliveryMealItemId.GetValueOrDefault()).Distinct().ToArray();
        var deliveryItems = await db.DeliveryMealItems.AsNoTracking().Include(item => item.DeliveryMeal)
            .Where(item => deliveryItemIds.Contains(item.Id) && item.DeliveryMeal!.UserId == UserId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var historicalIds = inputItems.Where(input => input.SourceMealItemId.HasValue).Select(input => input.SourceMealItemId.GetValueOrDefault()).Distinct().ToArray();
        var historical = await db.MealItems.AsNoTracking().Include(item => item.Meal)
            .Where(item => historicalIds.Contains(item.Id) && db.Meals.Any(meal => meal.Id == item.MealId && meal.UserId == UserId))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var orderIds = inputItems.Where(input => input.SourceDeliveryMealId.HasValue).Select(input => input.SourceDeliveryMealId.GetValueOrDefault()).Distinct().ToArray();
        var orders = await db.DeliveryMeals.AsNoTracking().Where(order => orderIds.Contains(order.Id) && order.UserId == UserId)
            .ToDictionaryAsync(order => order.Id, cancellationToken);
        if (historical.Count != historicalIds.Length || orders.Count != orderIds.Length || deliveryItems.Count != deliveryItemIds.Length)
            throw new ValidationException("One or more saved source records are unavailable.");
        if (inputItems.Any(input => input.MeasurementType.HasValue &&
                (input.SourceMealItemId is Guid id && input.MeasurementType != historical[id].MeasurementType
                 || input.SourceDeliveryMealId.HasValue && input.MeasurementType != FoodMeasurementType.Portion
                 || input.SourceDeliveryMealItemId is Guid deliveryId && input.MeasurementType != deliveryItems[deliveryId].MeasurementType)))
            throw new ValidationException("The requested unit does not match the saved source.");
        var resolved = inputItems.Select(input => input.SourceMealItemId is Guid itemId
            ? input with { FoodItemId = historical[itemId].FoodItemId, FoodNameSnapshot = historical[itemId].FoodNameSnapshot,
                MeasurementType = historical[itemId].MeasurementType, CarbsPer100gSnapshot = historical[itemId].CarbsPer100gSnapshot,
                CarbsPerUnitSnapshot = historical[itemId].CarbsPerUnitSnapshot,
                CarbOverride = input.ClearCarbOverride ? null : input.CarbOverride ?? historical[itemId].CarbOverride }
            : input.SourceDeliveryMealItemId is Guid deliveryId ? input with {
                FoodItemId = deliveryItems[deliveryId].FoodItemId ?? Guid.Empty, MeasurementType = deliveryItems[deliveryId].MeasurementType,
                FoodNameSnapshot = deliveryItems[deliveryId].Name,
                CarbsPer100gSnapshot = deliveryItems[deliveryId].CarbsPer100gSnapshot, CarbsPerUnitSnapshot = deliveryItems[deliveryId].CarbsPerUnitSnapshot,
                CarbOverride = input.ClearCarbOverride ? null : input.CarbOverride ?? deliveryItems[deliveryId].CarbOverride }
            : input.SourceDeliveryMealId is Guid id ? input with { FoodItemId = Guid.Empty, MeasurementType = FoodMeasurementType.Portion,
                FoodNameSnapshot = $"{orders[id].PlaceName} · {orders[id].DishName}", CarbsPer100gSnapshot = null, CarbsPerUnitSnapshot = orders[id].Carbs }
            : input).ToArray();
        var currentFoodInputs = resolved.Where(input => input.MeasurementType is null).ToList();
        var foodIds = currentFoodInputs.Select(i => i.FoodItemId).Distinct().ToList();
        var foods = await db.FoodItems
            .AsNoTracking()
            .Where(f => f.UserId == UserId && foodIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        if (foods.Count != foodIds.Count)
        {
            throw new ValidationException("One or more selected foods were not found.");
        }

        return resolved.Select(input =>
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

                if (input.SourceDeliveryMealId.HasValue && snapshotQuantity != 1m)
                    throw new ValidationException("Reuse a complete saved order as one portion.");
                var source = input.SourceMealItemId is Guid sourceId ? CarbProvenance.Of(historical[sourceId]) with
                    { Transformation = historical[sourceId].CarbTransformation | CarbTransformation.Calculated | CarbTransformation.HistoricalReuse,
                      SourceMealVerifiedAt = historical[sourceId].Meal?.CarbsVerifiedAt ?? historical[sourceId].SourceMealVerifiedAt }
                    : input.SourceDeliveryMealId is Guid savedOrderId ? CarbProvenance.Of(orders[savedOrderId]) with
                    { Transformation = orders[savedOrderId].CarbTransformation | CarbTransformation.Calculated | CarbTransformation.HistoricalReuse }
                    : input.SourceDeliveryMealItemId is Guid savedItemId ? new CarbProvenanceDto(deliveryItems[savedItemId].CarbReferenceSource,
                        deliveryItems[savedItemId].CarbTransformation | CarbTransformation.Calculated | CarbTransformation.HistoricalReuse, deliveryItems[savedItemId].SourceMealVerifiedAt)
                    : new CarbProvenanceDto(CarbReferenceSource.Unknown, CarbTransformation.Calculated);
                return CreateCalculatedItem(
                    input.FoodItemId,
                    string.IsNullOrWhiteSpace(input.FoodNameSnapshot) ? "Saved food" : input.FoodNameSnapshot.Trim(),
                    snapshotQuantity,
                    snapshotMeasurementType,
                    snapshotMeasurementType == FoodMeasurementType.Grams ? snapshotQuantity : null,
                    input.CarbsPer100gSnapshot,
                    input.CarbsPerUnitSnapshot,
                    snapshotCalculatedCarbs,
                    input.CarbOverride) with { Provenance = source, SourceMealItemId = input.SourceMealItemId, SourceDeliveryMealId = input.SourceDeliveryMealId, SourceDeliveryMealItemId = input.SourceDeliveryMealItemId };
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
                input.CarbOverride) with { Provenance = new(food.CarbReferenceSource, CarbTransformation.Calculated) };
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

    private Task<DiabetesSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        SettingsService.GetOrCreateUserSettingsAsync(db, UserId, timeProvider ?? TimeProvider.System, cancellationToken);

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

