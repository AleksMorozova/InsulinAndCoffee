using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.Controllers;

[ApiController]
[Route("api/known-meals")]
public class KnownMealsController(KnownMealService knownMealService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<KnownMealSectionsDto>> Get([FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await knownMealService.GetSectionsAsync(search, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KnownMealDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await knownMealService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<KnownMealDto>> Create(UpsertKnownMealRequest request, CancellationToken cancellationToken)
    {
        var knownMeal = await knownMealService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = knownMeal.Id }, knownMeal);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KnownMealDto>> Update(Guid id, UpsertKnownMealRequest request, CancellationToken cancellationToken) =>
        Ok(await knownMealService.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/favorite")]
    public async Task<ActionResult<KnownMealDto>> ToggleFavorite(Guid id, CancellationToken cancellationToken) =>
        Ok(await knownMealService.ToggleFavoriteAsync(id, cancellationToken));

    [HttpPost("{id:guid}/use-again")]
    public async Task<ActionResult<UseKnownMealDto>> UseAgain(Guid id, CancellationToken cancellationToken) =>
        Ok(await knownMealService.UseAgainAsync(id, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await knownMealService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
