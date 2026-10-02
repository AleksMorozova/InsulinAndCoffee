using InsulinAndCoffee.Domain.Entities;

namespace InsulinAndCoffee.Application.Abstractions;

public interface IAccessTokenService
{
    (string Token, DateTimeOffset ExpiresAt) Create(User user);
}
