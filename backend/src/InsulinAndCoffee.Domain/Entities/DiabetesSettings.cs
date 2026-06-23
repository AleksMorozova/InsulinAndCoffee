namespace InsulinAndCoffee.Domain.Entities;

public class DiabetesSettings
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal TargetGlucose { get; set; }
    public decimal CarbRatio { get; set; }
    public decimal CorrectionFactor { get; set; }
    public decimal InsulinDurationHours { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User? User { get; set; }
}
