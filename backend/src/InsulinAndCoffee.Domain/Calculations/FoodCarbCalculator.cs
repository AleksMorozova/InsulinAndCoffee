using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Domain.Calculations;

public static class FoodCarbCalculator
{
    public static decimal Calculate(
        FoodMeasurementType measurementType,
        decimal quantity,
        decimal? carbsPer100g,
        decimal? carbsPerUnit)
    {
        var carbs = measurementType switch
        {
            FoodMeasurementType.Grams => carbsPer100g.GetValueOrDefault() * quantity / 100,
            FoodMeasurementType.Portion => carbsPerUnit.GetValueOrDefault() * quantity,
            FoodMeasurementType.Piece => carbsPerUnit.GetValueOrDefault() * quantity,
            _ => throw new ArgumentOutOfRangeException(nameof(measurementType), measurementType, "Unsupported food measurement type.")
        };

        return Math.Round(carbs, 2);
    }
}
