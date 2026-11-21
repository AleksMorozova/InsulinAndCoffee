namespace InsulinAndCoffee.Application.Abstractions;

public sealed class DeliveryMealConflictException() : Exception("This saved order changed. Reload it before editing or reusing its items.");
