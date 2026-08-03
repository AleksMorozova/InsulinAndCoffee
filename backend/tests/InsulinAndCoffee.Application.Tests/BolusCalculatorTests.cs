using InsulinAndCoffee.Application.Calculations;

namespace InsulinAndCoffee.Application.Tests;

public class BolusCalculatorTests
{
    [Fact]
    public void CalculateFoodBolus_WhenCarbohydratesAreZero_ReturnsZero()
    {
        var result = BolusCalculator.CalculateFoodBolus(totalCarbs: 0m, carbRatio: 10m);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateFoodBolus_WhenDivisionHasIntegerResult_ReturnsRoundedDose()
    {
        var result = BolusCalculator.CalculateFoodBolus(totalCarbs: 50m, carbRatio: 10m);

        Assert.Equal(5m, result);
    }

    [Fact]
    public void CalculateFoodBolus_WhenDivisionHasFractionalResult_ReturnsDoseRoundedToTwoDecimals()
    {
        var result = BolusCalculator.CalculateFoodBolus(totalCarbs: 45m, carbRatio: 12m);

        Assert.Equal(3.75m, result);
    }

    [Fact]
    public void CalculateFoodBolus_WhenCarbohydrateRatioIsZero_ThrowsDivideByZero()
    {
        Assert.Throws<DivideByZeroException>(() =>
            BolusCalculator.CalculateFoodBolus(totalCarbs: 45m, carbRatio: 0m));
    }

    [Fact]
    public void CalculateFoodBolus_WhenCarbohydratesAreNegative_ReturnsMathematicalResult()
    {
        var result = BolusCalculator.CalculateFoodBolus(totalCarbs: -45m, carbRatio: 10m);

        Assert.Equal(-4.5m, result);
    }

    [Fact]
    public void CalculateFoodBolus_WhenCarbohydrateRatioIsNegative_ReturnsMathematicalResult()
    {
        var result = BolusCalculator.CalculateFoodBolus(totalCarbs: 45m, carbRatio: -10m);

        Assert.Equal(-4.5m, result);
    }

    [Fact]
    public void CalculateCorrectionBolus_WhenCurrentGlucoseIsAboveTarget_ReturnsRoundedCorrection()
    {
        var result = BolusCalculator.CalculateCorrectionBolus(currentGlucose: 9.5m, targetGlucose: 6.5m, correctionFactor: 3m);

        Assert.Equal(1m, result);
    }

    [Fact]
    public void CalculateCorrectionBolus_WhenCurrentGlucoseEqualsTarget_ReturnsZero()
    {
        var result = BolusCalculator.CalculateCorrectionBolus(currentGlucose: 6.5m, targetGlucose: 6.5m, correctionFactor: 3m);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateCorrectionBolus_WhenCurrentGlucoseIsBelowTarget_ReturnsZero()
    {
        var result = BolusCalculator.CalculateCorrectionBolus(currentGlucose: 4.5m, targetGlucose: 6.5m, correctionFactor: 3m);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateCorrectionBolus_WhenCorrectionFactorIsZeroAndCurrentGlucoseIsAboveTarget_ThrowsDivideByZero()
    {
        Assert.Throws<DivideByZeroException>(() =>
            BolusCalculator.CalculateCorrectionBolus(currentGlucose: 9.5m, targetGlucose: 6.5m, correctionFactor: 0m));
    }

    [Fact]
    public void CalculateCorrectionBolus_WhenCorrectionFactorIsNegative_ReturnsMathematicalResult()
    {
        var result = BolusCalculator.CalculateCorrectionBolus(currentGlucose: 9.5m, targetGlucose: 6.5m, correctionFactor: -3m);

        Assert.Equal(-1m, result);
    }

    [Fact]
    public void CalculateCorrectionBolus_WhenCorrectionHasFractionalResult_ReturnsDoseRoundedToTwoDecimals()
    {
        var result = BolusCalculator.CalculateCorrectionBolus(currentGlucose: 8.2m, targetGlucose: 6.5m, correctionFactor: 3m);

        Assert.Equal(0.57m, result);
    }

    [Fact]
    public void CalculateTotalBolus_WhenOnlyFoodBolusIsPresent_ReturnsFoodBolus()
    {
        var result = BolusCalculator.CalculateTotalBolus(foodBolus: 4.25m, correctionBolus: 0m);

        Assert.Equal(4.25m, result);
    }

    [Fact]
    public void CalculateTotalBolus_WhenOnlyCorrectionBolusIsPresent_ReturnsCorrectionBolus()
    {
        var result = BolusCalculator.CalculateTotalBolus(foodBolus: 0m, correctionBolus: 1.25m);

        Assert.Equal(1.25m, result);
    }

    [Fact]
    public void CalculateTotalBolus_WhenBothComponentsArePresent_ReturnsRoundedSum()
    {
        var result = BolusCalculator.CalculateTotalBolus(foodBolus: 4.25m, correctionBolus: 1.13m);

        Assert.Equal(5.38m, result);
    }

    [Fact]
    public void CalculateTotalBolus_WhenGlucoseIsBelowTarget_UsesZeroCorrection()
    {
        var foodBolus = BolusCalculator.CalculateFoodBolus(totalCarbs: 45m, carbRatio: 10m);
        var correctionBolus = BolusCalculator.CalculateCorrectionBolus(currentGlucose: 4.5m, targetGlucose: 6.5m, correctionFactor: 3m);

        var result = BolusCalculator.CalculateTotalBolus(foodBolus, correctionBolus);

        Assert.Equal(0m, correctionBolus);
        Assert.Equal(4.5m, result);
    }

    [Fact]
    public void CalculateTotalBolus_WhenBothCalculatedComponentsAreNonNegative_ReturnsNonNegativeDose()
    {
        var foodBolus = BolusCalculator.CalculateFoodBolus(totalCarbs: 0m, carbRatio: 10m);
        var correctionBolus = BolusCalculator.CalculateCorrectionBolus(currentGlucose: 4.5m, targetGlucose: 6.5m, correctionFactor: 3m);

        var result = BolusCalculator.CalculateTotalBolus(foodBolus, correctionBolus);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void RoundDose_WhenValueIsBelowRoundingBoundary_RoundsDown()
    {
        var result = BolusCalculator.RoundDose(1.234m);

        Assert.Equal(1.23m, result);
    }

    [Fact]
    public void RoundDose_WhenValueIsExactMidpoint_UsesMidpointRoundingToEven()
    {
        var result = BolusCalculator.RoundDose(1.225m);

        Assert.Equal(1.22m, result);
    }

    [Fact]
    public void RoundDose_WhenValueIsAboveRoundingBoundary_RoundsUp()
    {
        var result = BolusCalculator.RoundDose(1.236m);

        Assert.Equal(1.24m, result);
    }

    [Fact]
    public void RoundDose_WhenValueIsNegative_UsesMidpointRoundingToEven()
    {
        var result = BolusCalculator.RoundDose(-1.225m);

        Assert.Equal(-1.22m, result);
    }
}
