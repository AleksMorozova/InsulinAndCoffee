using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Domain.Entities;

public class GlucoseReading
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? MealId { get; set; }
    public decimal Value { get; set; }
    public DateTimeOffset ReadingTime { get; set; }
    public ReadingType ReadingType { get; set; }
    public string? Notes { get; set; }

    public User? User { get; set; }
    public Meal? Meal { get; set; }
}
