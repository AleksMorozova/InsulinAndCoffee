namespace InsulinAndCoffee.Domain.Exceptions;

public sealed class CarbCalculationException(string message) : Exception(message);
