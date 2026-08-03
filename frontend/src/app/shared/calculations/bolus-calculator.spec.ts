import {
  calculateCorrectionBolus,
  calculateFoodBolus,
  calculateTotalBolus,
  roundDose
} from './bolus-calculator';

describe('BolusCalculator TypeScript migration', () => {
  describe('calculateFoodBolus', () => {
    it('calculateFoodBolus_WhenCarbohydratesAreZero_ReturnsZero', () => {
      expect(calculateFoodBolus(0, 10)).toBe(0);
    });

    it('calculateFoodBolus_WhenDivisionHasIntegerResult_ReturnsRoundedDose', () => {
      expect(calculateFoodBolus(50, 10)).toBe(5);
    });

    it('calculateFoodBolus_WhenDivisionHasFractionalResult_ReturnsDoseRoundedToTwoDecimals', () => {
      expect(calculateFoodBolus(45, 12)).toBe(3.75);
    });

    it('calculateFoodBolus_WhenInputsArePrecisionSensitive_UsesNumberArithmeticWithRoundedResult', () => {
      expect(calculateFoodBolus(0.3, 0.1)).toBe(3);
    });

    it('calculateFoodBolus_WhenCarbohydrateRatioIsZero_ThrowsRangeError', () => {
      expect(() => calculateFoodBolus(45, 0)).toThrowError(RangeError);
    });

    it('calculateFoodBolus_WhenCarbohydratesAreNegative_ReturnsMathematicalResult', () => {
      expect(calculateFoodBolus(-45, 10)).toBe(-4.5);
    });

    it('calculateFoodBolus_WhenCarbohydrateRatioIsNegative_ReturnsMathematicalResult', () => {
      expect(calculateFoodBolus(45, -10)).toBe(-4.5);
    });

    it('calculateFoodBolus_WhenInputsAreVeryLarge_ReturnsFiniteRoundedNumberResult', () => {
      expect(calculateFoodBolus(7.922816251426434e27, 10)).toBe(7.922816251426434e26);
    });

    it('calculateFoodBolus_WhenDivisionResultIsNotFinite_ThrowsRangeError', () => {
      expect(() => calculateFoodBolus(Number.MAX_VALUE, Number.MIN_VALUE)).toThrowError(RangeError);
    });
  });

  describe('calculateCorrectionBolus', () => {
    it('calculateCorrectionBolus_WhenCurrentGlucoseIsAboveTarget_ReturnsRoundedCorrection', () => {
      expect(calculateCorrectionBolus(9.5, 6.5, 3)).toBe(1);
    });

    it('calculateCorrectionBolus_WhenCurrentGlucoseEqualsTarget_ReturnsZero', () => {
      expect(calculateCorrectionBolus(6.5, 6.5, 3)).toBe(0);
    });

    it('calculateCorrectionBolus_WhenCurrentGlucoseIsBelowTarget_ReturnsZero', () => {
      expect(calculateCorrectionBolus(4.5, 6.5, 3)).toBe(0);
    });

    it('calculateCorrectionBolus_WhenCorrectionFactorIsZeroAndCurrentGlucoseIsAboveTarget_ThrowsRangeError', () => {
      expect(() => calculateCorrectionBolus(9.5, 6.5, 0)).toThrowError(RangeError);
    });

    it('calculateCorrectionBolus_WhenCorrectionFactorIsZeroAndCurrentGlucoseEqualsTarget_ReturnsZero', () => {
      expect(calculateCorrectionBolus(6.5, 6.5, 0)).toBe(0);
    });

    it('calculateCorrectionBolus_WhenCorrectionFactorIsZeroAndCurrentGlucoseIsBelowTarget_ReturnsZero', () => {
      expect(calculateCorrectionBolus(4.5, 6.5, 0)).toBe(0);
    });

    it('calculateCorrectionBolus_WhenCorrectionFactorIsNegative_ReturnsMathematicalResult', () => {
      expect(calculateCorrectionBolus(9.5, 6.5, -3)).toBe(-1);
    });

    it('calculateCorrectionBolus_WhenCorrectionHasFractionalResult_ReturnsDoseRoundedToTwoDecimals', () => {
      expect(calculateCorrectionBolus(8.2, 6.5, 3)).toBe(0.57);
    });

    it('calculateCorrectionBolus_WhenInputsArePrecisionSensitive_UsesNumberArithmeticWithRoundedResult', () => {
      expect(calculateCorrectionBolus(0.3, 0.1, 0.1)).toBe(2);
    });
  });

  describe('calculateTotalBolus', () => {
    it('calculateTotalBolus_WhenOnlyFoodBolusIsPresent_ReturnsFoodBolus', () => {
      expect(calculateTotalBolus(4.25, 0)).toBe(4.25);
    });

    it('calculateTotalBolus_WhenOnlyCorrectionBolusIsPresent_ReturnsCorrectionBolus', () => {
      expect(calculateTotalBolus(0, 1.25)).toBe(1.25);
    });

    it('calculateTotalBolus_WhenBothComponentsArePresent_ReturnsRoundedSum', () => {
      expect(calculateTotalBolus(4.25, 1.13)).toBe(5.38);
    });

    it('calculateTotalBolus_WhenFoodBolusIsNegative_ReturnsMathematicalRoundedSum', () => {
      expect(calculateTotalBolus(-2, 1)).toBe(-1);
    });

    it('calculateTotalBolus_WhenCorrectionBolusIsNegative_ReturnsMathematicalRoundedSum', () => {
      expect(calculateTotalBolus(2, -3)).toBe(-1);
    });

    it('calculateTotalBolus_WhenSumIsNotFinite_ThrowsRangeError', () => {
      expect(() => calculateTotalBolus(Number.MAX_VALUE, Number.MAX_VALUE)).toThrowError(RangeError);
    });
  });

  describe('roundDose', () => {
    it('roundDose_WhenValueIsBelowRoundingBoundary_RoundsDown', () => {
      expect(roundDose(1.234)).toBe(1.23);
    });

    it('roundDose_WhenValueIsExactMidpoint_UsesMidpointRoundingToEven', () => {
      expect(roundDose(1.225)).toBe(1.22);
    });

    it('roundDose_WhenMidpointWouldRoundUpToEven_UsesMidpointRoundingToEven', () => {
      expect(roundDose(1.235)).toBe(1.24);
    });

    it('roundDose_WhenValueIsAboveRoundingBoundary_RoundsUp', () => {
      expect(roundDose(1.236)).toBe(1.24);
    });

    it('roundDose_WhenValueIsNegative_UsesMidpointRoundingToEven', () => {
      expect(roundDose(-1.225)).toBe(-1.22);
    });

    it('roundDose_WhenNegativeMidpointWouldRoundDownToEven_UsesMidpointRoundingToEven', () => {
      expect(roundDose(-1.235)).toBe(-1.24);
    });

    it('roundDose_WhenValueIsImmediatelyBelowMidpoint_RoundsTowardNearest', () => {
      expect(roundDose(1.2249)).toBe(1.22);
    });

    it('roundDose_WhenValueIsImmediatelyAboveMidpoint_RoundsTowardNearest', () => {
      expect(roundDose(1.2251)).toBe(1.23);
    });

    it('roundDose_WhenValueIsNaN_ThrowsTypeError', () => {
      expect(() => roundDose(Number.NaN)).toThrowError(TypeError);
    });

    it('roundDose_WhenValueIsInfinity_ThrowsRangeError', () => {
      expect(() => roundDose(Number.POSITIVE_INFINITY)).toThrowError(RangeError);
    });

    it('roundDose_WhenValueIsNull_ThrowsTypeError', () => {
      expect(() => roundDose(null as unknown as number)).toThrowError(TypeError);
    });

    it('roundDose_WhenValueIsUndefined_ThrowsTypeError', () => {
      expect(() => roundDose(undefined as unknown as number)).toThrowError(TypeError);
    });
  });
});
