namespace InsulinAndCoffee.Application.Dtos;

public sealed record LoginAttemptDto(
    Guid Id,
    string Username,
    Guid? UserId,
    bool Success,
    DateTimeOffset AttemptedAtUtc,
    string? FailureReason,
    string? IpAddress,
    string? UserAgent);

public sealed record LoginAttemptStatsDto(
    int TotalAttempts,
    int SuccessfulAttempts,
    int FailedAttempts,
    int UniqueUsernames);
