using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Domain.Exceptions;

namespace InsulinAndCoffee.Domain.Calculations;

public static class FoodCarbCalculator
{
    public static decimal ScaleTotal(decimal total, decimal originalQuantity, decimal quantity)
    {
        if (total < 0 || originalQuantity <= 0 || quantity <= 0)
            throw new CarbCalculationException("Enter a non-negative total and positive quantities.");
        return Math.Round(total * quantity / originalQuantity, 2);
    }
    // A reference derived from an explicitly entered total, never a generic nutrition estimate.
    // The explicit override remains authoritative, including any reference rounding difference.
    public static decimal ReferenceFromTotal(FoodMeasurementType unit, decimal quantity, decimal total)
    {
        if (!Enum.IsDefined(unit) || quantity <= 0 || total < 0) throw new CarbCalculationException("Enter a valid quantity, unit and total carbohydrates.");
        return Math.Round(total * (unit == FoodMeasurementType.Grams ? 100m : 1m) / quantity, 2);
    }
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
