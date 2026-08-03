using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.Controllers;

[ApiController]
[Route("api/delivery-meals")]
public class DeliveryMealsController(DeliveryMealStranglerRouter deliveryMealRouter) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DeliveryMealSectionsDto>> Get([FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await deliveryMealRouter.GetSectionsAsync(search, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeliveryMealDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await deliveryMealRouter.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<DeliveryMealDto>> Create(UpsertDeliveryMealRequest request, CancellationToken cancellationToken)
    {
        var deliveryMeal = await deliveryMealRouter.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = deliveryMeal.Id }, deliveryMeal);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DeliveryMealDto>> Update(Guid id, UpsertDeliveryMealRequest request, CancellationToken cancellationToken) =>
        Ok(await deliveryMealRouter.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/favorite")]
    public async Task<ActionResult<DeliveryMealDto>> ToggleFavorite(Guid id, CancellationToken cancellationToken) =>
        Ok(await deliveryMealRouter.ToggleFavoriteAsync(id, cancellationToken));

    [HttpPost("{id:guid}/meal-draft")]
    public async Task<ActionResult<UseDeliveryMealDto>> CreateMealDraft(Guid id, CancellationToken cancellationToken) =>
        Ok(await deliveryMealRouter.CreateMealDraftFromDeliveryMealAsync(id, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await deliveryMealRouter.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
