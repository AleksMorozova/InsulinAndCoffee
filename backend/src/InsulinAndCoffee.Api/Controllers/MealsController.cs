using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MealsController(MealService mealService) : ControllerBase
{
    [HttpPost("calculate")]
    public async Task<ActionResult<MealCalculationDto>> Calculate(CalculateMealRequest request, CancellationToken cancellationToken) =>
        Ok(await mealService.CalculateMealAsync(request, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<MealDetailDto>> Create(CreateMealRequest request, CancellationToken cancellationToken)
    {
        var meal = await mealService.CreateMealAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = meal.Id }, meal);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MealSummaryDto>>> Get([FromQuery] string? search, [FromQuery] MealType? mealType, CancellationToken cancellationToken) =>
        Ok(await mealService.GetMealsAsync(search, mealType, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MealDetailDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await mealService.GetMealAsync(id, cancellationToken));

    [HttpPatch("{id:guid}/confirmed-bolus")]
    public async Task<ActionResult<MealDetailDto>> ConfirmBolus(Guid id, ConfirmMealBolusRequest request, CancellationToken cancellationToken) =>
        Ok(await mealService.ConfirmMealBolusAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}/confirmed-bolus")]
    public async Task<ActionResult<MealDetailDto>> ClearConfirmedBolus(Guid id, CancellationToken cancellationToken) =>
        Ok(await mealService.ClearConfirmedBolusAsync(id, cancellationToken));

    [HttpPatch("{id:guid}/items")]
    public async Task<ActionResult<MealDetailDto>> AddItems(Guid id, AddMealItemsRequest request, CancellationToken cancellationToken) =>
        Ok(await mealService.AddMealItemsAsync(id, request, cancellationToken));

    [HttpPut("{mealId:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<MealDetailDto>> UpdateItem(Guid mealId, Guid itemId, UpdateMealItemRequest request, CancellationToken cancellationToken) =>
        Ok(await mealService.UpdateMealItemAsync(mealId, itemId, request, cancellationToken));

    [HttpDelete("{mealId:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<MealDetailDto>> RemoveItem(Guid mealId, Guid itemId, CancellationToken cancellationToken) =>
        Ok(await mealService.RemoveMealItemAsync(mealId, itemId, cancellationToken));
}
