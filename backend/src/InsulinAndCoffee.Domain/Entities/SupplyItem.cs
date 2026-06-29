namespace InsulinAndCoffee.Domain.Entities;

public class SupplyItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal CurrentQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal DailyUsage { get; set; }
    public int LowStockThresholdDays { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public User? User { get; set; }
}
