namespace InsulinAndCoffee.Domain.Entities;

public class DiabetesSettings
{
    public static DiabetesSettings CreateDefaults(Guid id, Guid userId, DateTimeOffset updatedAt) =>
        new()
        {
            Id = id,
            UserId = userId,
            TargetGlucose = 6.5m,
            CarbRatio = 10m,
            CorrectionFactor = 3m,
            InsulinDurationHours = 4m,
            UpdatedAt = updatedAt
        };

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal TargetGlucose { get; set; }
    public decimal CarbRatio { get; set; }
    public decimal CorrectionFactor { get; set; }
    public decimal InsulinDurationHours { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User? User { get; set; }
}
