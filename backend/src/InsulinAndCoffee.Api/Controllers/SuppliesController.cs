using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.Controllers;

[ApiController]
[Route("api/supplies")]
public class SuppliesController(SupplyService supplyService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplyItemDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await supplyService.GetAllAsync(cancellationToken));

    [HttpGet("check")]
    public async Task<ActionResult<IReadOnlyList<SupplyCheckResultDto>>> GetSupplyCheck(CancellationToken cancellationToken) =>
        Ok(await supplyService.GetSupplyCheckAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplyItemDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await supplyService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<SupplyItemDto>> Create(CreateSupplyItemRequest request, CancellationToken cancellationToken)
    {
        var item = await supplyService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SupplyItemDto>> Update(
        Guid id,
        UpdateSupplyItemRequest request,
        CancellationToken cancellationToken) =>
        Ok(await supplyService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await supplyService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
