namespace InsulinAndCoffee.Domain.Entities;

using InsulinAndCoffee.Domain.Enums;

public class MealItem
{
    public Guid Id { get; set; }
    public Guid MealId { get; set; }
    public Guid FoodItemId { get; set; }
    public string FoodNameSnapshot { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public FoodMeasurementType MeasurementType { get; set; } = FoodMeasurementType.Grams;
    public decimal? WeightGrams { get; set; }
    public decimal? CarbsPer100gSnapshot { get; set; }
    public decimal? CarbsPerUnitSnapshot { get; set; }
    public decimal CalculatedCarbs { get; set; }

    public Meal? Meal { get; set; }
    public FoodItem? FoodItem { get; set; }
}
