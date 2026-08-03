namespace InsulinAndCoffee.Application.Services;

public enum DeliveryMealRoutingMode
{
    Legacy,
    Shadow,
    Migrated
}

public sealed class DeliveryMealStranglerOptions
{
    public DeliveryMealRoutingMode RoutingMode { get; init; } = DeliveryMealRoutingMode.Legacy;
}
