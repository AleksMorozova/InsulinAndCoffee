const doseDecimalPlaces = 2;

/**
 * Migrated equivalent of C# CalculateFoodBolus(decimal totalCarbs, decimal carbRatio).
 *
 * C# uses decimal arithmetic; this TypeScript version uses JavaScript number
 * and therefore cannot guarantee exact decimal precision or C# decimal range
 * equivalence for every possible input.
 */
export function calculateFoodBolus(totalCarbs: number, carbRatio: number): number {
  assertValidNumber(totalCarbs, 'totalCarbs');
  assertValidNumber(carbRatio, 'carbRatio');

  return roundDose(divide(totalCarbs, carbRatio, 'carbRatio'));
}

/**
 * Migrated equivalent of C# CalculateCorrectionBolus(decimal currentGlucose,
 * decimal targetGlucose, decimal correctionFactor).
 *
 * Preserves the C# branch behavior: the correction factor is only used when
 * currentGlucose is above targetGlucose.
 */
export function calculateCorrectionBolus(
  currentGlucose: number,
  targetGlucose: number,
  correctionFactor: number
): number {
  assertValidNumber(currentGlucose, 'currentGlucose');
  assertValidNumber(targetGlucose, 'targetGlucose');
  assertValidNumber(correctionFactor, 'correctionFactor');

  if (currentGlucose <= targetGlucose) {
    return 0;
  }

  return roundDose(divide(currentGlucose - targetGlucose, correctionFactor, 'correctionFactor'));
}

/**
 * Migrated equivalent of C# CalculateTotalBolus(decimal foodBolus,
 * decimal correctionBolus).
 */
export function calculateTotalBolus(foodBolus: number, correctionBolus: number): number {
  assertValidNumber(foodBolus, 'foodBolus');
  assertValidNumber(correctionBolus, 'correctionBolus');

  const totalBolus = foodBolus + correctionBolus;
  assertFiniteResult(totalBolus);

  return roundDose(totalBolus);
}

/**
 * Migrated equivalent of C# Math.Round(dose, 2), whose default midpoint
 * behavior is MidpointRounding.ToEven.
 *
 * This function emulates the characterized C# behavior for JavaScript number
 * inputs. It is not an arbitrary-precision decimal implementation.
 */
export function roundDose(dose: number): number {
  assertValidNumber(dose, 'dose');

  return roundHalfToEven(dose, doseDecimalPlaces);
}

function assertValidNumber(value: number, parameterName: string): void {
  if (typeof value !== 'number') {
    throw new TypeError(`${parameterName} must be a number.`);
  }

  if (Number.isNaN(value)) {
    throw new TypeError(`${parameterName} must not be NaN.`);
  }

  if (!Number.isFinite(value)) {
    throw new RangeError(`${parameterName} must be finite.`);
  }
}

function divide(dividend: number, divisor: number, divisorName: string): number {
  if (divisor === 0) {
    throw new RangeError(`${divisorName} cannot be zero.`);
  }

  const result = dividend / divisor;
  assertFiniteResult(result);

  return result;
}

function assertFiniteResult(value: number): void {
  if (!Number.isFinite(value)) {
    throw new RangeError('Calculation result must be finite.');
  }
}

function roundHalfToEven(value: number, decimalPlaces: number): number {
  const isNegative = value < 0;
  const absoluteDecimal = toPlainDecimalString(Math.abs(value));
  const roundedAbsolute = roundPositiveDecimalStringHalfToEven(absoluteDecimal, decimalPlaces);
  const rounded = Number(roundedAbsolute);

  assertFiniteResult(rounded);

  if (rounded === 0) {
    return 0;
  }

  return isNegative ? -rounded : rounded;
}

function roundPositiveDecimalStringHalfToEven(value: string, decimalPlaces: number): string {
  const [rawWholePart, rawFractionPart = ''] = value.split('.');
  const wholePart = rawWholePart.length === 0 ? '0' : rawWholePart;
  const fractionPart = rawFractionPart.padEnd(decimalPlaces + 1, '0');
  const keptFraction = fractionPart.slice(0, decimalPlaces).padEnd(decimalPlaces, '0');
  const nextDigit = Number(fractionPart[decimalPlaces] ?? '0');
  const remainingDigits = fractionPart.slice(decimalPlaces + 1);
  const lastKeptDigit = decimalPlaces === 0
    ? Number(wholePart[wholePart.length - 1] ?? '0')
    : Number(keptFraction[keptFraction.length - 1] ?? '0');

  const shouldIncrement =
    nextDigit > 5 ||
    (nextDigit === 5 && (hasNonZeroDigit(remainingDigits) || lastKeptDigit % 2 !== 0));

  const scaledValue = BigInt(`${wholePart}${keptFraction}`) + (shouldIncrement ? 1n : 0n);
  const scaledText = scaledValue.toString().padStart(decimalPlaces + 1, '0');
  const integerText = scaledText.slice(0, -decimalPlaces) || '0';
  const fractionText = scaledText.slice(-decimalPlaces).padStart(decimalPlaces, '0');

  return `${integerText}.${fractionText}`;
}

function hasNonZeroDigit(value: string): boolean {
  return [...value].some((digit) => digit !== '0');
}

function toPlainDecimalString(value: number): string {
  const text = value.toString();

  if (!text.includes('e')) {
    return text;
  }

  const [coefficient, exponentText] = text.split('e');
  const exponent = Number(exponentText);
  const [wholePart, fractionPart = ''] = coefficient.split('.');
  const digits = `${wholePart}${fractionPart}`;
  const decimalIndex = wholePart.length + exponent;

  if (decimalIndex <= 0) {
    return `0.${'0'.repeat(-decimalIndex)}${digits}`;
  }

  if (decimalIndex >= digits.length) {
    return `${digits}${'0'.repeat(decimalIndex - digits.length)}`;
  }

  return `${digits.slice(0, decimalIndex)}.${digits.slice(decimalIndex)}`;
}
