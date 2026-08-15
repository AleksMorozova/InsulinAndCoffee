using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Application.Dtos;

public record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record DiabetesSettingsDto(Guid Id, decimal TargetGlucose, decimal CarbRatio, decimal CorrectionFactor, decimal InsulinDurationHours, DateTimeOffset UpdatedAt);

public record UpdateDiabetesSettingsRequest(decimal TargetGlucose, decimal CarbRatio, decimal CorrectionFactor, decimal InsulinDurationHours);

public record FoodItemDto(Guid Id, string Name, FoodMeasurementType MeasurementType, decimal? CarbsPer100g, decimal? CarbsPerUnit, decimal ProteinPer100g, decimal FatPer100g, decimal CaloriesPer100g, bool IsFavorite, DateTimeOffset CreatedAt);

public record UpsertFoodItemRequest(string Name, FoodMeasurementType MeasurementType, decimal? CarbsPer100g, decimal? CarbsPerUnit, decimal ProteinPer100g, decimal FatPer100g, decimal CaloriesPer100g, bool IsFavorite);

public record MealItemInputDto(
    Guid FoodItemId,
    decimal? Quantity = null,
    decimal? WeightGrams = null,
    FoodMeasurementType? MeasurementType = null,
    string? FoodNameSnapshot = null,
    decimal? CarbsPer100gSnapshot = null,
    decimal? CarbsPerUnitSnapshot = null,
    decimal? CarbOverride = null);

public record MealItemDto(Guid Id, Guid FoodItemId, string FoodNameSnapshot, decimal Quantity, FoodMeasurementType MeasurementType, decimal? WeightGrams, decimal? CarbsPer100gSnapshot, decimal? CarbsPerUnitSnapshot, decimal CalculatedCarbs, decimal? CarbOverride, decimal EffectiveCarbs);

public record MealCalculationDto(decimal FoodCarbs, decimal CarbAdjustment, decimal TotalCarbs, decimal MealBolus, decimal CorrectionBolus, decimal SuggestedBolus, IReadOnlyList<CalculatedMealItemDto> Items);

public record CalculatedMealItemDto(Guid FoodItemId, string FoodName, decimal Quantity, FoodMeasurementType MeasurementType, decimal? WeightGrams, decimal? CarbsPer100g, decimal? CarbsPerUnit, decimal CalculatedCarbs, decimal? CarbOverride, decimal EffectiveCarbs);

public record CalculateMealRequest(MealType MealType, decimal PreMealGlucose, IReadOnlyList<MealItemInputDto> Items, decimal? DirectCarbs = null, string? DirectFoodName = null, decimal CarbAdjustment = 0);

public record CreateMealRequest(MealType MealType, DateTimeOffset? MealTime, decimal PreMealGlucose, decimal? ConfirmedBolus, string? Notes, IReadOnlyList<MealItemInputDto> Items, decimal? DirectCarbs = null, string? DirectFoodName = null, decimal CarbAdjustment = 0);

public record ConfirmMealBolusRequest(decimal ConfirmedBolus);

public record AddMealItemsRequest(IReadOnlyList<MealItemInputDto> Items);

public record UpdateMealItemRequest(decimal? Quantity = null, decimal? WeightGrams = null, decimal? CarbOverride = null);

public record MealSummaryDto(Guid Id, MealType MealType, DateTimeOffset MealTime, decimal PreMealGlucose, decimal TotalCarbs, decimal CarbAdjustment, decimal SuggestedBolus, decimal? ConfirmedBolus, string? Notes, IReadOnlyList<string> FoodNames);

public record MealDetailDto(Guid Id, MealType MealType, DateTimeOffset MealTime, decimal PreMealGlucose, decimal TotalCarbs, decimal CarbAdjustment, decimal SuggestedBolus, decimal? ConfirmedBolus, string? Notes, DateTimeOffset CreatedAt, IReadOnlyList<MealItemDto> Items);

public record DashboardDto(
    DateOnly Date,
    decimal TotalCarbs,
    decimal ConfirmedInsulin,
    int MealCount,
    IReadOnlyList<DashboardMealDto> Meals);

public record DashboardMealDto(
    Guid Id,
    MealType MealType,
    DateTimeOffset MealTime,
    DateTimeOffset CreatedAt,
    decimal TotalCarbs,
    decimal? ConfirmedInsulin,
    bool RequiresInsulinConfirmation);

public record DeliveryMealDto(
    Guid Id,
    string PlaceName,
    string DishName,
    string PortionDescription,
    decimal Carbs,
    decimal UsualInsulinUnits,
    decimal? LastPreMealGlucose,
    ResultRating ResultRating,
    string Tags,
    string? Notes,
    bool IsFavorite,
    int UsageCount,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt);

public record UpsertDeliveryMealRequest(
    string PlaceName,
    string DishName,
    string PortionDescription,
    decimal Carbs,
    decimal UsualInsulinUnits,
    decimal? LastPreMealGlucose,
    ResultRating ResultRating,
    string Tags,
    string? Notes,
    bool IsFavorite);

public record CreateDeliveryMealFromMealRequest(
    string PlaceName,
    string DishName,
    string PortionDescription,
    ResultRating ResultRating,
    string Tags,
    bool IsFavorite);

public record DeliveryMealSectionsDto(
    IReadOnlyList<DeliveryMealDto> Favorites,
    IReadOnlyList<DeliveryMealDto> MostUsed,
    IReadOnlyList<DeliveryMealDto> RecentlyUsed,
    IReadOnlyList<DeliveryMealDto> SearchResults);

public record UseDeliveryMealDto(Guid Id, decimal Carbs, decimal UsualInsulinUnits, string Notes);

public record SupplyItemDto(
    Guid Id,
    string Name,
    decimal CurrentQuantity,
    string Unit,
    decimal DailyUsage,
    int LowStockThresholdDays,
    DateTimeOffset LastUpdatedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record CreateSupplyItemRequest(
    string Name,
    decimal CurrentQuantity,
    string Unit,
    decimal DailyUsage,
    int LowStockThresholdDays);

public record UpdateSupplyItemRequest(
    string Name,
    decimal CurrentQuantity,
    string Unit,
    decimal DailyUsage,
    int LowStockThresholdDays);

public record SupplyCheckResultDto(
    Guid Id,
    string Name,
    decimal CurrentQuantity,
    string Unit,
    decimal DailyUsage,
    int LowStockThresholdDays,
    decimal? DaysLeft,
    DateOnly? EstimatedRunOutDate,
    string Status);

