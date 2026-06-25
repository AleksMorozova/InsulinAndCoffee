using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.Controllers;

[ApiController]
[Route("api/meals/{mealId:guid}/save-as-delivery-meal")]
public class MealDeliveryMealsController(DeliveryMealService deliveryMealService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DeliveryMealDto>> SaveAsDeliveryMeal(Guid mealId, CreateDeliveryMealFromMealRequest request, CancellationToken cancellationToken)
    {
        var deliveryMeal = await deliveryMealService.CreateFromMealAsync(mealId, request, cancellationToken);
        return CreatedAtAction("GetById", "DeliveryMeals", new { id = deliveryMeal.Id }, deliveryMeal);
    }
}
