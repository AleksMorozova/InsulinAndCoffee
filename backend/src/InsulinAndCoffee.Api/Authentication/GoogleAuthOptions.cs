namespace InsulinAndCoffee.Api.Authentication;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";
    public string ClientId { get; init; } = string.Empty;
}
