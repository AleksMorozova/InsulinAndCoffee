namespace InsulinAndCoffee.Application.Dtos;

public sealed record RegisterRequest(string Username, string Password);
public sealed record LoginRequest(string Username, string Password);
public sealed record GoogleLoginRequest(string Credential);
public sealed record AuthUserDto(string Username, DateTimeOffset CreatedAt);
public sealed record AuthTokenDto(string AccessToken, DateTimeOffset ExpiresAt, AuthUserDto User);
