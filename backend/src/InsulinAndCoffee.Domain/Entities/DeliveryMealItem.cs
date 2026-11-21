using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Domain.Entities;

public class DeliveryMealItem
{
    public Guid Id { get; set; }
    public Guid DeliveryMealId { get; set; }
    public DeliveryMeal? DeliveryMeal { get; set; }
    public Guid? FoodItemId { get; set; }
    public FoodItem? FoodItem { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public FoodMeasurementType MeasurementType { get; set; }
    public decimal? CarbsPer100gSnapshot { get; set; }
    public decimal? CarbsPerUnitSnapshot { get; set; }
    public decimal CalculatedCarbs { get; set; }
    public decimal? CarbOverride { get; set; }
    public CarbReferenceSource CarbReferenceSource { get; set; }
    public CarbTransformation CarbTransformation { get; set; }
    public DateTimeOffset? SourceMealVerifiedAt { get; set; }
    public int DisplayOrder { get; set; }
}
