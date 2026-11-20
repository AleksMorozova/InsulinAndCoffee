using InsulinAndCoffee.Application.Dtos;

namespace InsulinAndCoffee.Application.Abstractions;

public interface ILlmClient
{
    Task<AssistantSearch> InterpretAsync(IReadOnlyList<AssistantContext> context, CancellationToken cancellationToken);
}

public class MealAssistantUnavailableException(string message, string code = "assistant_unavailable") : Exception(message)
{
    public string Code { get; } = code;
}
