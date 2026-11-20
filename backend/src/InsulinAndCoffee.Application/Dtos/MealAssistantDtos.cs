using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Application.Dtos;

public record MealAssistantRequest(Guid? ConversationId, string Message, Guid? RequestId = null, AssistantFoodSelection? Selection = null);
public record AssistantFoodSelection(Guid FoodId, decimal? Quantity = null, FoodMeasurementType? Unit = null);
public record AssistantFoodQuery(string Name, decimal? Quantity, FoodMeasurementType? Unit, string? Restaurant = null, Guid? FoodItemId = null);
public record AssistantSearch(AssistantFoodQuery[] Foods, string Intent = "history", string? Operation = null, string? Target = null);
public record AssistantMealItem(string Name, decimal Quantity, FoodMeasurementType Unit, decimal Carbs, decimal? RequestedQuantity, FoodMeasurementType? RequestedUnit, decimal? EstimatedCarbs);
public record AssistantMeal(Guid Id, DateTimeOffset MealTime, MealType MealType, decimal TotalCarbs,
    decimal SuggestedBolus, decimal? ConfirmedBolus, decimal PreMealGlucose, string Match, IReadOnlyList<AssistantMealItem> Items,
    IReadOnlyList<AssistantGlucoseReading> GlucoseReadings, bool CanUseInCalculator);
public record AssistantGlucoseReading(decimal Value, DateTimeOffset ReadingTime, ReadingType ReadingType);
public record MealAssistantResponse(Guid ConversationId, string Message, IReadOnlyList<AssistantMeal> Meals, AssistantDraft? Draft = null, AssistantKnowledge? Knowledge = null);
public record AssistantDraft(IReadOnlyList<CalculatedMealItemDto> Items, IReadOnlyList<string> Issues, decimal TotalCarbs, IReadOnlyList<AssistantDraftSource>? Sources = null, AssistantFoodQuery? Pending = null, string? EditFeedback = null, AssistantSearch? PendingEdit = null);
public record AssistantConversationSummary(Guid Id, string Title, DateTimeOffset UpdatedAt);
public record AssistantTurn(string Question, MealAssistantResponse Answer);
public record AssistantContext(string Role, string Content);

public record AssistantSavedFood(Guid Id, string Name, FoodMeasurementType Unit, decimal? CarbsPer100g, decimal? CarbsPerUnit);
public record AssistantOrder(Guid Id, string PlaceName, string DishName, string PortionDescription, decimal Carbs, DateTimeOffset? LastUsedAt);
public record AssistantKnowledge(IReadOnlyList<AssistantSavedFood> Foods, IReadOnlyList<AssistantOrder> Orders);
public record AssistantDraftSource(Guid RecordId, string Kind, string Name);
