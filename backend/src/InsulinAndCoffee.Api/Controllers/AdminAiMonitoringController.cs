using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.Controllers;

[ApiController]
[Route("api/admin/ai-monitoring")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminAiMonitoringController(AiMonitoringService service) : ControllerBase
{
    [HttpGet("requests")]
    public async Task<ActionResult<PaginatedResult<AiUsageRecord>>> History([FromQuery] AiUsageFilter filter,
        int page = 1, int pageSize = 25, string sort = "time", bool descending = true, CancellationToken cancellationToken = default) =>
        Ok(await service.HistoryAsync(filter, page, pageSize, sort, descending, cancellationToken));

    [HttpGet("requests/{id:guid}")]
    public async Task<ActionResult<AiUsageRecord>> Details(Guid id, CancellationToken cancellationToken) => Ok(await service.DetailsAsync(id, cancellationToken));

    [HttpGet("overview")]
    public async Task<ActionResult<AiMonitoringOverview>> Overview([FromQuery] AiUsageFilter filter, CancellationToken cancellationToken) =>
        Ok(await service.OverviewAsync(filter, cancellationToken));

    [HttpGet("pricing")]
    public async Task<ActionResult<IReadOnlyList<AiModelPricing>>> Prices(CancellationToken cancellationToken) => Ok(await service.PricesAsync(cancellationToken));

    [HttpPost("pricing")]
    public async Task<ActionResult<AiModelPricing>> AddPrice(CreateAiPricingRequest request, CancellationToken cancellationToken) =>
        Ok(await service.AddPriceAsync(request, cancellationToken));
}
