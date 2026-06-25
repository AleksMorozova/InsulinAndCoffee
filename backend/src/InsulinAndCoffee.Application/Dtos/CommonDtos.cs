using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Application.Dtos;

public record DiabetesSettingsDto(Guid Id, decimal TargetGlucose, decimal CarbRatio, decimal CorrectionFactor, decimal InsulinDurationHours, DateTimeOffset UpdatedAt);

public record UpdateDiabetesSettingsRequest(decimal TargetGlucose, decimal CarbRatio, decimal CorrectionFactor, decimal InsulinDurationHours);

public record FoodItemDto(Guid Id, string Name, decimal CarbsPer100g, decimal ProteinPer100g, decimal FatPer100g, decimal CaloriesPer100g, bool IsFavorite, DateTimeOffset CreatedAt);

public record UpsertFoodItemRequest(string Name, decimal CarbsPer100g, decimal ProteinPer100g, decimal FatPer100g, decimal CaloriesPer100g, bool IsFavorite);

public record MealItemInputDto(Guid FoodItemId, decimal WeightGrams);

public record MealItemDto(Guid Id, Guid FoodItemId, string FoodNameSnapshot, decimal WeightGrams, decimal CarbsPer100gSnapshot, decimal CalculatedCarbs);

public record MealCalculationDto(decimal TotalCarbs, decimal MealBolus, decimal CorrectionBolus, decimal SuggestedBolus, IReadOnlyList<CalculatedMealItemDto> Items);

public record CalculatedMealItemDto(Guid FoodItemId, string FoodName, decimal WeightGrams, decimal CarbsPer100g, decimal CalculatedCarbs);

public record CalculateMealRequest(MealType MealType, decimal PreMealGlucose, IReadOnlyList<MealItemInputDto> Items, decimal? DirectCarbs = null, string? DirectFoodName = null);

public record CreateMealRequest(MealType MealType, DateTimeOffset? MealTime, decimal PreMealGlucose, decimal ConfirmedBolus, string? Notes, IReadOnlyList<MealItemInputDto> Items, decimal? DirectCarbs = null, string? DirectFoodName = null);

public record MealSummaryDto(Guid Id, MealType MealType, DateTimeOffset MealTime, decimal PreMealGlucose, decimal TotalCarbs, decimal SuggestedBolus, decimal ConfirmedBolus, string? Notes, IReadOnlyList<string> FoodNames);

public record MealDetailDto(Guid Id, MealType MealType, DateTimeOffset MealTime, decimal PreMealGlucose, decimal TotalCarbs, decimal SuggestedBolus, decimal ConfirmedBolus, string? Notes, DateTimeOffset CreatedAt, IReadOnlyList<MealItemDto> Items);

public record DashboardDto(decimal TodaysTotalCarbs, decimal TodaysConfirmedInsulinUnits, MealSummaryDto? LastMeal);

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
