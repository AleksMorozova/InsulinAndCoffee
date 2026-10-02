using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public sealed class AuthService(
    IAppDbContext db,
    IPasswordService passwords,
    IAccessTokenService tokens,
    IGoogleIdentityValidator googleIdentity,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<AuthUserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var username = ValidateCredentials(request.Username, request.Password);
        var normalized = Normalize(username);
        if (await db.Users.AnyAsync(user => user.NormalizedUsername == normalized, cancellationToken))
        {
            throw new AppValidationException("Registration failed.", new Dictionary<string, string[]>
            {
                [nameof(request.Username)] = ["Username is already in use."]
            });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            NormalizedUsername = normalized,
            CreatedAt = timeProvider.GetUtcNow()
        };
        user.PasswordHash = passwords.HashPassword(request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public async Task<AuthTokenDto?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return null;
        }

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.NormalizedUsername == Normalize(request.Username), cancellationToken);
        if (user?.PasswordHash is null || !passwords.VerifyPassword(user.PasswordHash, request.Password))
        {
            return null;
        }

        var (token, expiresAt) = tokens.Create(user);
        return new(token, expiresAt, ToDto(user));
    }

    public async Task<AuthTokenDto?> LoginWithGoogleAsync(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Credential)) return null;
        var identity = await googleIdentity.ValidateAsync(request.Credential, cancellationToken);
        if (identity is null) return null;

        var user = await db.Users.FirstOrDefaultAsync(user => user.GoogleSubject == identity.Subject, cancellationToken);
        if (user is null)
        {
            var username = await CreateGoogleUsernameAsync(identity, cancellationToken);
            user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                NormalizedUsername = Normalize(username),
                GoogleSubject = identity.Subject,
                Email = identity.Email,
                Name = identity.Name,
                CreatedAt = timeProvider.GetUtcNow()
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }

        var (token, expiresAt) = tokens.Create(user);
        return new(token, expiresAt, ToDto(user));
    }

    public async Task<AuthUserDto> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .FirstAsync(user => user.Id == currentUser.UserId, cancellationToken);
        return ToDto(user);
    }

    private static string ValidateCredentials(string usernameValue, string password)
    {
        var errors = new Dictionary<string, string[]>();
        var username = usernameValue?.Trim() ?? string.Empty;
        if (username.Length is < 3 or > 50)
            errors[nameof(RegisterRequest.Username)] = ["Username must be between 3 and 50 characters."];
        if (password.Length is < 8 or > 128)
            errors[nameof(RegisterRequest.Password)] = ["Password must be between 8 and 128 characters."];
        if (errors.Count > 0)
            throw new AppValidationException("Registration failed.", errors);
        return username;
    }

    public static string Normalize(string username) => username.Trim().ToUpperInvariant();

    private async Task<string> CreateGoogleUsernameAsync(GoogleIdentity identity, CancellationToken cancellationToken)
    {
        var emailName = identity.Email.Split('@', 2)[0];
        var cleaned = new string(emailName.Where(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_').ToArray());
        if (cleaned.Length < 3) cleaned = $"user-{cleaned}";
        var baseName = cleaned[..Math.Min(cleaned.Length, 43)];
        var candidate = baseName;
        if (await db.Users.AnyAsync(user => user.NormalizedUsername == Normalize(candidate), cancellationToken))
        {
            var suffixLength = Math.Min(6, identity.Subject.Length);
            candidate = $"{baseName}-{identity.Subject[^suffixLength..]}";
        }
        return candidate;
    }
    private static AuthUserDto ToDto(User user) => new(user.Username, user.CreatedAt);
}
