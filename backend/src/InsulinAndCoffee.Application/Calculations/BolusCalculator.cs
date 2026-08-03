namespace InsulinAndCoffee.Application.Calculations;

public static class BolusCalculator
{
    public static decimal CalculateFoodBolus(decimal totalCarbs, decimal carbRatio) =>
        RoundDose(totalCarbs / carbRatio);

    public static decimal CalculateCorrectionBolus(decimal currentGlucose, decimal targetGlucose, decimal correctionFactor) =>
        currentGlucose > targetGlucose
            ? RoundDose((currentGlucose - targetGlucose) / correctionFactor)
            : 0;

    public static decimal CalculateTotalBolus(decimal foodBolus, decimal correctionBolus) =>
        RoundDose(foodBolus + correctionBolus);

    public static decimal RoundDose(decimal dose) =>
        Math.Round(dose, 2);
}
