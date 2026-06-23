using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.Controllers;

[ApiController]
[Route("api/meals/{mealId:guid}/save-to-known-meals")]
public class MealKnownMealsController(KnownMealService knownMealService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<KnownMealDto>> SaveToKnownMeals(Guid mealId, CreateKnownMealFromMealRequest request, CancellationToken cancellationToken)
    {
        var knownMeal = await knownMealService.CreateFromMealAsync(mealId, request, cancellationToken);
        return CreatedAtAction("GetById", "KnownMeals", new { id = knownMeal.Id }, knownMeal);
    }
}
