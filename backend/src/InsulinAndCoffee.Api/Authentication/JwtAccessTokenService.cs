using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InsulinAndCoffee.Api.Authentication;

public sealed class JwtAccessTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenService
{
    public (string Token, DateTimeOffset ExpiresAt) Create(User user)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.ExpirationMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            ],
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
