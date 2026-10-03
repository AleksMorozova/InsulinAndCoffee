using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthUserDto>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Me), user);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthTokenDto>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, GetAttemptMetadata(), cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("google")]
    public async Task<ActionResult<AuthTokenDto>> Google(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginWithGoogleAsync(request, GetAttemptMetadata(), cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthUserDto>> Me(CancellationToken cancellationToken) =>
        Ok(await authService.GetCurrentAsync(cancellationToken));

    private LoginAttemptMetadata GetAttemptMetadata() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString());
}
