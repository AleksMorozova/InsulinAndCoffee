using System.Security.Claims;
using InsulinAndCoffee.Application.Abstractions;

namespace InsulinAndCoffee.Api.Authentication;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id)
                ? id
                : throw new UnauthorizedAccessException("An authenticated user is required.");
        }
    }
}
