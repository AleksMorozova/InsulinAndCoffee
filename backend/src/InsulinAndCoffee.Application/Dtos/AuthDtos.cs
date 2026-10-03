namespace InsulinAndCoffee.Application.Dtos;

using InsulinAndCoffee.Domain.Enums;

public sealed record RegisterRequest(string Username, string Password);
public sealed record LoginRequest(string Username, string Password);
public sealed record GoogleLoginRequest(string Credential);
public sealed record AuthUserDto(Guid Id, string Username, UserRole Role, DateTimeOffset CreatedAt);
public sealed record AuthTokenDto(string AccessToken, DateTimeOffset ExpiresAt, AuthUserDto User);
public sealed record LoginAttemptMetadata(string? IpAddress, string? UserAgent);
