using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Domain.Entities;

public class KnownMeal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PlaceName { get; set; } = string.Empty;
    public string DishName { get; set; } = string.Empty;
    public string PortionDescription { get; set; } = string.Empty;
    public decimal Carbs { get; set; }
    public decimal UsualInsulinUnits { get; set; }
    public decimal? LastPreMealGlucose { get; set; }
    public ResultRating ResultRating { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsFavorite { get; set; }
    public int UsageCount { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
}
