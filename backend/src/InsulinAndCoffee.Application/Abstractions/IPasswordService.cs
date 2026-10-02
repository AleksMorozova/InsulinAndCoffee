namespace InsulinAndCoffee.Application.Abstractions;

public interface IPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string passwordHash, string password);
}
