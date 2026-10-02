using Google.Apis.Auth;
using InsulinAndCoffee.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace InsulinAndCoffee.Api.Authentication;

public sealed class GoogleIdentityValidator(IOptions<GoogleAuthOptions> options) : IGoogleIdentityValidator
{
    public async Task<GoogleIdentity?> ValidateAsync(string credential, CancellationToken cancellationToken)
    {
        var clientId = options.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId)) return null;

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(credential, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId]
            });
            cancellationToken.ThrowIfCancellationRequested();
            if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
                return null;
            return new GoogleIdentity(payload.Subject, payload.Email, payload.Name);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
