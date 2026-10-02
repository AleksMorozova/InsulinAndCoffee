using InsulinAndCoffee.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace InsulinAndCoffee.Api.Authentication;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> hasher = new();
    private static readonly object Subject = new();

    public string HashPassword(string password) => hasher.HashPassword(Subject, password);

    public bool VerifyPassword(string passwordHash, string password) =>
        hasher.VerifyHashedPassword(Subject, passwordHash, password) is not PasswordVerificationResult.Failed;
}
