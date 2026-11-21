namespace InsulinAndCoffee.Application.Abstractions;

public sealed class CarbVerificationConflictException() : Exception("The meal changed. Reload it and review the current carbs before verifying or editing again.");
