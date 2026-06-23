using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(MealService mealService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken) =>
        Ok(await mealService.GetDashboardAsync(cancellationToken));
}
