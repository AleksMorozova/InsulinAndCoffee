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
}
