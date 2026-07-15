using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Domain.Entities;

public class Meal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public MealType MealType { get; set; }
    public DateTimeOffset MealTime { get; set; }
    public decimal PreMealGlucose { get; set; }
    public decimal TotalCarbs { get; set; }
    public decimal SuggestedBolus { get; set; }
    public decimal? ConfirmedBolus { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
    public ICollection<MealItem> Items { get; set; } = [];
    public ICollection<GlucoseReading> GlucoseReadings { get; set; } = [];
}
