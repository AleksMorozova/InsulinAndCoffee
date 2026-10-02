namespace InsulinAndCoffee.Application.Abstractions;

public sealed record GoogleIdentity(string Subject, string Email, string? Name);

public interface IGoogleIdentityValidator
{
    Task<GoogleIdentity?> ValidateAsync(string credential, CancellationToken cancellationToken);
}
