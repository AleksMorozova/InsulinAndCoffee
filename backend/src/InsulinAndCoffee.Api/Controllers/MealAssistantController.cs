using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InsulinAndCoffee.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/meal-assistant")]
[EnableRateLimiting("meal-assistant")]
public class MealAssistantController(MealAssistantService assistant) : ControllerBase
{
    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<AssistantConversationSummary>>> List(CancellationToken cancellationToken) =>
        Ok(await assistant.ListAsync(cancellationToken));

    [HttpGet("conversations/{id:guid}")]
    public async Task<ActionResult<IReadOnlyList<AssistantTurn>>> Read(Guid id, CancellationToken cancellationToken) =>
        Ok(await assistant.ReadAsync(id, cancellationToken));

    [HttpGet("conversations/{id:guid}/calculator-draft")]
    public async Task<ActionResult<IReadOnlyList<CalculatedMealItemDto>>> PrepareDraft(Guid id, CancellationToken cancellationToken) =>
        Ok(await assistant.PrepareDraftAsync(id, cancellationToken));

    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await assistant.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("messages")]
    [RequestSizeLimit(8192)]
    public async Task<ActionResult<MealAssistantResponse>> Send(MealAssistantRequest request, CancellationToken cancellationToken) =>
        Ok(await assistant.SendAsync(request, cancellationToken));
}
