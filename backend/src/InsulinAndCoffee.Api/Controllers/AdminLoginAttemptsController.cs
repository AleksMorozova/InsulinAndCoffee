using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.Controllers;

[ApiController]
[Route("api/admin/login-attempts")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminLoginAttemptsController(AdminLoginAttemptService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<LoginAttemptDto>>> Get(
        int page = 1,
        int pageSize = 25,
        string? username = null,
        bool? success = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetAsync(page, pageSize, username, success, from, to, cancellationToken));

    [HttpGet("stats")]
    public async Task<ActionResult<LoginAttemptStatsDto>> GetStats(
        string? username = null,
        bool? success = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetStatsAsync(username, success, from, to, cancellationToken));
}
