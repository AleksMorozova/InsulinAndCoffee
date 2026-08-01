namespace InsulinAndCoffee.Application.Services;

public sealed class AppValidationException(
    string message,
    IDictionary<string, string[]> errors) : Exception(message)
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}