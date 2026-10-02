using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsulinAndCoffee.Infrastructure;
using InsulinAndCoffee.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InsulinAndCoffee.Application.Tests;

public sealed class AuthIntegrationTests : IClassFixture<AuthIntegrationTests.ApiFactory>
{
    private readonly ApiFactory factory;
    public AuthIntegrationTests(ApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwt()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "aleks", password = "aleks" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("accessToken").GetString()));
        Assert.Equal("aleks", json.RootElement.GetProperty("user").GetProperty("username").GetString());
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "aleks", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_ValidCredential_CreatesEmptyUserAndReturnsJwt()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/google", new { credential = "valid-google-token" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = json.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var foods = JsonDocument.Parse(await client.GetStringAsync("/api/foods?pageSize=100"));
        Assert.Equal(0, foods.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GoogleLogin_InvalidCredential_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/google", new { credential = "invalid" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsBadRequest()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { username = "ALEKS", password = "valid-password" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/auth/me")]
    [InlineData("/api/dashboard/today")]
    [InlineData("/api/meals")]
    [InlineData("/api/delivery-meals")]
    [InlineData("/api/foods")]
    [InlineData("/api/supplies")]
    [InlineData("/api/settings")]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegisterAndLogin_NewUserStartsEmptyAndCannotAccessAleksFood()
    {
        var username = $"new-{Guid.NewGuid():N}"[..20];
        var client = factory.CreateClient();
        var registration = await client.PostAsJsonAsync("/api/auth/register", new { username, password = "test-password" });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        var token = await LoginAsync(client, username, "test-password");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var foods = JsonDocument.Parse(await client.GetStringAsync("/api/foods?pageSize=100"));
        Assert.Equal(0, foods.RootElement.GetProperty("totalCount").GetInt32());

        var response = await client.PutAsJsonAsync(
            "/api/foods/33333333-3333-3333-3333-333333333301",
            new { name = "Not mine", measurementType = "Grams", carbsPer100g = 1m, proteinPer100g = 0m, fatPer100g = 0m, caloriesPer100g = 0m, isFavorite = false });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FoodCreatedByTest_IsNotVisibleToAleks()
    {
        var testClient = factory.CreateClient();
        testClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(testClient, "test", "test"));
        var created = await testClient.PostAsJsonAsync("/api/foods", new
        {
            name = $"Test food {Guid.NewGuid():N}", measurementType = "Grams", carbsPer100g = 10m,
            proteinPer100g = 0m, fatPer100g = 0m, caloriesPer100g = 0m, isFavorite = false
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdJson.RootElement.GetProperty("id").GetGuid();

        var aleksClient = factory.CreateClient();
        aleksClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(aleksClient, "aleks", "aleks"));
        var delete = await aleksClient.DeleteAsync($"/api/foods/{id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private const string ConnectionVariable = "ConnectionStrings__DefaultConnection";
        private const string JwtVariable = "Jwt__SigningKey";
        private readonly string? originalConnection = Environment.GetEnvironmentVariable(ConnectionVariable);
        private readonly string? originalJwt = Environment.GetEnvironmentVariable(JwtVariable);

        public ApiFactory()
        {
            Environment.SetEnvironmentVariable(ConnectionVariable, "Host=localhost;Database=auth_tests;Username=test;Password=test");
            Environment.SetEnvironmentVariable(JwtVariable, "authentication-integration-test-key-32-chars");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("auth-integration-tests"));
                services.RemoveAll<IGoogleIdentityValidator>();
                services.AddScoped<IGoogleIdentityValidator, TestGoogleIdentityValidator>();
            });
        }

        private sealed class TestGoogleIdentityValidator : IGoogleIdentityValidator
        {
            public Task<GoogleIdentity?> ValidateAsync(string credential, CancellationToken cancellationToken) =>
                Task.FromResult<GoogleIdentity?>(credential == "valid-google-token"
                    ? new GoogleIdentity("google-subject-123456", "google.user@example.com", "Google User")
                    : null);
        }

        protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
            return host;
        }

        protected override void Dispose(bool disposing)
        {
            Environment.SetEnvironmentVariable(ConnectionVariable, originalConnection);
            Environment.SetEnvironmentVariable(JwtVariable, originalJwt);
            base.Dispose(disposing);
        }
    }
}
