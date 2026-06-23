using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsulinAndCoffee.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController(SettingsService settingsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DiabetesSettingsDto>> Get(CancellationToken cancellationToken) =>
        Ok(await settingsService.GetSettingsAsync(cancellationToken));

    [HttpPut]
    public async Task<ActionResult<DiabetesSettingsDto>> Update(UpdateDiabetesSettingsRequest request, CancellationToken cancellationToken) =>
        Ok(await settingsService.UpdateSettingsAsync(request, cancellationToken));
}
