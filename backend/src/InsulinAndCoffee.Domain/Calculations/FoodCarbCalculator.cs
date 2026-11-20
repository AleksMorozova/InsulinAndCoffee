using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Domain.Exceptions;

namespace InsulinAndCoffee.Domain.Calculations;

public static class FoodCarbCalculator
{
    public static decimal Calculate(
        FoodMeasurementType measurementType,
        decimal quantity,
        decimal? carbsPer100g,
        decimal? carbsPerUnit)
    {
        var basis = measurementType switch
        {
            FoodMeasurementType.Grams => carbsPer100g,
            FoodMeasurementType.Portion or FoodMeasurementType.Piece => carbsPerUnit,
            _ => throw new CarbCalculationException("Measurement type is not supported.")
        };

        if (basis is null or < 0)
            throw new CarbCalculationException(measurementType == FoodMeasurementType.Grams
                ? "Carbs per 100 g must be provided and be zero or greater."
                : "Carbs per unit must be provided and be zero or greater.");

        var carbs = measurementType == FoodMeasurementType.Grams
            ? basis.Value * quantity / 100
            : basis.Value * quantity;

        return Math.Round(carbs, 2);
    }
}
