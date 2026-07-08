using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoodsController(FoodService foodService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<FoodItemDto>>> Get(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await foodService.GetFoodsAsync(search, page, pageSize, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<FoodItemDto>> Create(UpsertFoodItemRequest request, CancellationToken cancellationToken)
    {
        var food = await foodService.CreateFoodAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = food.Id }, food);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FoodItemDto>> Update(Guid id, UpsertFoodItemRequest request, CancellationToken cancellationToken) =>
        Ok(await foodService.UpdateFoodAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await foodService.DeleteFoodAsync(id, cancellationToken);
        return NoContent();
    }
}
