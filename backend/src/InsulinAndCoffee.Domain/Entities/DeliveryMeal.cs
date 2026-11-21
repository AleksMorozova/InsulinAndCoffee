using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Domain.Entities;

public class DeliveryMeal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PlaceName { get; set; } = string.Empty;
    public string DishName { get; set; } = string.Empty;
    public string PortionDescription { get; set; } = string.Empty;
    public decimal Carbs { get; set; }
    public decimal? UsualInsulinUnits { get; set; }
    public decimal? LastPreMealGlucose { get; set; }
    public ResultRating ResultRating { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsFavorite { get; set; }
    public int UsageCount { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public CarbReferenceSource CarbReferenceSource { get; set; }
    public CarbTransformation CarbTransformation { get; set; }
    public DateTimeOffset? SourceMealVerifiedAt { get; set; }
    public long Revision { get; set; }
    public ICollection<DeliveryMealItem> Items { get; set; } = [];

    public User? User { get; set; }
}
