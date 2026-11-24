using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Application.Dtos;

public record MealAssistantRequest(Guid? ConversationId, string Message, Guid? RequestId = null, AssistantFoodSelection? Selection = null, long? ExpectedDraftVersion = null, AssistantDraftEdit? Edit = null, AssistantResolution? Resolution = null);
public record AssistantResolution(Guid UnresolvedId, string Action, Guid? SourceId = null, string? SourceKind = null,
    decimal? Quantity = null, FoodMeasurementType? Unit = null, decimal? Carbs = null, string? SearchName = null);
public enum AssistantResolutionReason { NoMatches, Ambiguous, QuantityRequired, UnitMismatch, MissingCarbs, OrderUnavailable }
public enum AssistantCandidateKind { Food, DeliveryMealItem, DeliveryMeal }
public record AssistantCandidate(Guid Id, AssistantCandidateKind Kind, string Name, int Score, string? Context = null);
public record AssistantUnresolvedItem(Guid Id, string OriginalText, AssistantFoodQuery Query, AssistantResolutionReason Reason,
    string Detail, IReadOnlyList<AssistantCandidate> Candidates);
public record AssistantDraftEdit(int ItemIndex, string Action, decimal? Quantity = null);
public record RenameAssistantConversation(string Title);
public record AssistantFoodSelection(Guid FoodId, decimal? Quantity = null, FoodMeasurementType? Unit = null, string Kind = "Food");
public record AssistantFoodQuery(string Name, decimal? Quantity, FoodMeasurementType? Unit, string? Restaurant = null, Guid? FoodItemId = null,
    Guid? DeliveryMealItemId = null, Guid? DeliveryMealId = null, string? OriginalText = null, bool UnitInferred = false);
public record AssistantSearch(AssistantFoodQuery[] Foods, string Intent = "history", string? Operation = null, string? Target = null);
public record AssistantMealItem(string Name, decimal Quantity, FoodMeasurementType Unit, decimal Carbs, decimal? RequestedQuantity, FoodMeasurementType? RequestedUnit, decimal? EstimatedCarbs, CarbProvenanceDto? Provenance = null);
public record AssistantMeal(Guid Id, DateTimeOffset MealTime, MealType MealType, decimal TotalCarbs,
    decimal SuggestedBolus, decimal? ConfirmedBolus, decimal PreMealGlucose, string Match, IReadOnlyList<AssistantMealItem> Items,
    IReadOnlyList<AssistantGlucoseReading> GlucoseReadings, bool CanUseInCalculator, DateTimeOffset? CarbsVerifiedAt = null);
public record AssistantGlucoseReading(decimal Value, DateTimeOffset ReadingTime, ReadingType ReadingType);
public record MealAssistantResponse(Guid ConversationId, string Message, IReadOnlyList<AssistantMeal> Meals, AssistantDraft? Draft = null, AssistantKnowledge? Knowledge = null, long DraftVersion = 0, Guid? RequestId = null, bool Replayed = false, string? ResultStatus = null);
public record AssistantDraft(IReadOnlyList<CalculatedMealItemDto> Items, IReadOnlyList<string> Issues, decimal TotalCarbs, IReadOnlyList<AssistantDraftSource>? Sources = null, AssistantFoodQuery? Pending = null, string? EditFeedback = null, AssistantSearch? PendingEdit = null, IReadOnlyList<AssistantUnresolvedItem>? Unresolved = null);
public record AssistantConversationSummary(Guid Id, string Title, DateTimeOffset UpdatedAt);
public record AssistantTurn(string Question, MealAssistantResponse Answer);
public record AssistantDraftState(long DraftVersion, AssistantDraft? Draft);
public record AssistantContext(string Role, string Content);

public record AssistantSavedFood(Guid Id, string Name, FoodMeasurementType Unit, decimal? CarbsPer100g, decimal? CarbsPerUnit, CarbProvenanceDto? Provenance = null);
public record AssistantOrder(Guid Id, string PlaceName, string DishName, string PortionDescription, decimal Carbs, DateTimeOffset? LastUsedAt, CarbProvenanceDto? Provenance = null);
public record AssistantKnowledge(IReadOnlyList<AssistantSavedFood> Foods, IReadOnlyList<AssistantOrder> Orders, IReadOnlyList<AssistantOrderItem>? Items = null);
public record AssistantOrderItem(Guid Id, Guid OrderId, string PlaceName, string OrderName, string Name, decimal Quantity,
    FoodMeasurementType Unit, decimal? CarbsPer100g, decimal? CarbsPerUnit, decimal EffectiveCarbs, CarbProvenanceDto Provenance);
public record AssistantDraftSource(Guid RecordId, string Kind, string Name, bool UnitInferred = false);
