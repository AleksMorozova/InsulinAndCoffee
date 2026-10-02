namespace InsulinAndCoffee.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? GoogleSubject { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public DiabetesSettings? DiabetesSettings { get; set; }
    public ICollection<FoodItem> FoodItems { get; set; } = [];
    public ICollection<Meal> Meals { get; set; } = [];
    public ICollection<GlucoseReading> GlucoseReadings { get; set; } = [];
    public ICollection<DeliveryMeal> DeliveryMeals { get; set; } = [];
    public ICollection<SupplyItem> SupplyItems { get; set; } = [];
}
