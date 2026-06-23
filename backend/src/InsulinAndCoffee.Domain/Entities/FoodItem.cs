namespace InsulinAndCoffee.Domain.Entities;

public class FoodItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal CarbsPer100g { get; set; }
    public decimal ProteinPer100g { get; set; }
    public decimal FatPer100g { get; set; }
    public decimal CaloriesPer100g { get; set; }
    public bool IsFavorite { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
    public ICollection<MealItem> MealItems { get; set; } = [];
}
