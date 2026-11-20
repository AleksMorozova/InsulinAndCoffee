namespace InsulinAndCoffee.Application.Abstractions;

public sealed class AssistantConflictException(string code, string message, long? currentDraftVersion = null) : Exception(message)
{
    public string Code { get; } = code;
    public long? CurrentDraftVersion { get; } = currentDraftVersion;
}
