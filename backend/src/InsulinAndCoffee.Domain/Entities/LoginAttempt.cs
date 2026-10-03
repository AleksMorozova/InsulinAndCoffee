namespace InsulinAndCoffee.Domain.Entities;

public class LoginAttempt
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public bool IsSuccessful { get; set; }
    public DateTimeOffset AttemptedAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public User? User { get; set; }
}
