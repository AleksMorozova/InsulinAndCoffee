using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsulinAndCoffee.Infrastructure;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;

namespace InsulinAndCoffee.Application.Tests;

public sealed class AuthIntegrationTests : IClassFixture<AuthIntegrationTests.ApiFactory>
{
    private readonly ApiFactory factory;
    public AuthIntegrationTests(ApiFactory factory) => this.factory = factory;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CountedMeal_OptionalPortionNote_PreservesStructuredTotalsOverrideAndSelectedItem(string? portionDescription)
    {
        using var client = factory.CreateClient();
        var username = "optional-" + Guid.NewGuid().ToString("N")[..12];
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", new { username, password = "optional-note-test" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, username, "optional-note-test"));
        var created = await client.PostAsJsonAsync("/api/delivery-meals", new { placeName = "Le Gril", dishName = "Суши", portionDescription,
            carbs = 135m, resultRating = "Unknown", tags = "", isFavorite = false, items = new[] {
                new { name = "Калифорния", quantity = 1m, measurementType = "Portion", carbsPerUnit = 60m, carbOverride = (decimal?)null },
                new { name = "Суши", quantity = 5m, measurementType = "Portion", carbsPerUnit = 15m, carbOverride = (decimal?)75m }
            } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("", json.RootElement.GetProperty("portionDescription").GetString());
        Assert.Equal(135m, json.RootElement.GetProperty("carbs").GetDecimal());
        var orderId = json.RootElement.GetProperty("id").GetGuid();
        var sushi = json.RootElement.GetProperty("items")[1];
        var sushiId = sushi.GetProperty("id").GetGuid();
        Assert.Equal(75m, sushi.GetProperty("carbOverride").GetDecimal());
        var updated = await client.PutAsJsonAsync($"/api/delivery-meals/{orderId}", new { placeName = "Le Gril", dishName = "Суши",
            portionDescription, carbs = 135m, resultRating = "Unknown", tags = "", isFavorite = false, expectedRevision = 0 });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var selection = await client.PostAsJsonAsync($"/api/delivery-meals/{orderId}/items-draft", new { itemIds = new[] { sushiId }, expectedRevision = 1 });
        Assert.Equal(HttpStatusCode.OK, selection.StatusCode);
        using var selected = JsonDocument.Parse(await selection.Content.ReadAsStringAsync());
        Assert.Equal(1, selected.RootElement.GetArrayLength());
        Assert.Equal("Суши", selected.RootElement[0].GetProperty("foodName").GetString());
        Assert.Equal(5m, selected.RootElement[0].GetProperty("quantity").GetDecimal());
        Assert.Equal(75m, selected.RootElement[0].GetProperty("effectiveCarbs").GetDecimal());
        Assert.Equal(75m, selected.RootElement[0].GetProperty("carbOverride").GetDecimal());
        // Omitted note is accepted as well, including the legacy whole-order workflow.
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/delivery-meals", new {
            placeName = "Le Gril", dishName = "Legacy", carbs = 135m, resultRating = "Unknown", tags = "", isFavorite = false })).StatusCode);
    }

    [Fact]
    public async Task StructuredOrders_HttpContracts_PreviewReuseAndOwnershipAreValidated()
    {
        using var client = factory.CreateClient();
        var username = "items-" + Guid.NewGuid().ToString("N")[..12];
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", new { username, password = "structured-test-password" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, username, "structured-test-password"));
        var items = new[] { new { name = "Sushi", quantity = 5m, measurementType = "Piece", carbsPerUnit = 15m } };
        var preview = await client.PostAsJsonAsync("/api/delivery-meals/preview-items", new { items, savedTotal = 135m });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var previewJson = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        Assert.Equal(75m, previewJson.RootElement.GetProperty("totalCarbs").GetDecimal());
        Assert.Equal(-60m, previewJson.RootElement.GetProperty("difference").GetDecimal());
        var created = await client.PostAsJsonAsync("/api/delivery-meals", new { placeName = "Le Grill", dishName = "Test order", portionDescription = "Five pieces",
            carbs = 75m, resultRating = "Unknown", tags = "", isFavorite = false, items });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var orderId = json.RootElement.GetProperty("id").GetGuid();
        var itemId = json.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();
        var draft = await client.PostAsJsonAsync($"/api/delivery-meals/{orderId}/items-draft", new { itemIds = new[] { itemId }, expectedRevision = 0 });
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        using var draftJson = JsonDocument.Parse(await draft.Content.ReadAsStringAsync());
        Assert.Equal(itemId, draftJson.RootElement[0].GetProperty("sourceDeliveryMealItemId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/delivery-meals/{orderId}/items-draft", new { itemIds = new[] { itemId }, expectedRevision = 10 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/delivery-meals/preview-items", new { items = new[] {
            new { name = "Forged", quantity = 1m, measurementType = "Piece", carbsPerUnit = 15m, provenance = new { sourceMealVerifiedAt = "2099-01-01T00:00:00Z" } } } })).StatusCode);
        using var other = factory.CreateClient();
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(other, "admin", "admin"));
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/delivery-meals/{orderId}/items-draft", new { itemIds = new[] { itemId }, expectedRevision = 0 })).StatusCode);
    }

    [Fact]
    public async Task CarbVerification_UsesServerState_RejectsSpoofingAndStaleRevision()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, "aleks", "aleks"));
        var created = await client.PostAsJsonAsync("/api/meals", new { mealType = "Lunch", preMealGlucose = 6m, directCarbs = 20m, items = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("carbsVerifiedAt").ValueKind);
        var route = $"/api/meals/{id}/carb-verification";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new { expectedCarbRevision = 0, carbsVerifiedAt = "2099-01-01T00:00:00Z" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(route, new { expectedCarbRevision = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { expectedCarbRevision = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/foods", new { name = "Forged", measurementType = "Grams", carbsPer100g = 20m, provenance = new { referenceSource = "Manual", sourceMealVerifiedAt = "2099-01-01T00:00:00Z" } })).StatusCode);
        using var other = factory.CreateClient();
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(other, "admin", "admin"));
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(route, new { expectedCarbRevision = 1 })).StatusCode);
    }

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
    public async Task Login_SuccessAndFailure_PersistSafeAuditRecords()
    {
        var username = $"audit-{Guid.NewGuid():N}"[..20];
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", new { username, password = "safe-test-password" })).StatusCode);

        client.DefaultRequestHeaders.UserAgent.ParseAdd("audit-test-agent");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { username, password = "wrong-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { username, password = "safe-test-password" })).StatusCode);

        using var scope = factory.Services.CreateScope();
        var attempts = await scope.ServiceProvider.GetRequiredService<AppDbContext>().LoginAttempts
            .Where(attempt => attempt.Username == username)
            .OrderBy(attempt => attempt.AttemptedAtUtc)
            .ToListAsync();
        Assert.Equal(2, attempts.Count);
        Assert.False(attempts[0].IsSuccessful);
        Assert.Equal("InvalidCredentials", attempts[0].FailureReason);
        Assert.NotNull(attempts[0].UserId);
        Assert.True(attempts[1].IsSuccessful);
        Assert.Null(attempts[1].FailureReason);
        Assert.All(attempts, attempt => Assert.Equal("audit-test-agent", attempt.UserAgent));
        Assert.DoesNotContain(attempts, attempt =>
            string.Equals(attempt.FailureReason, "safe-test-password", StringComparison.Ordinal) ||
            string.Equals(attempt.UserAgent, "safe-test-password", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_UnknownUsername_PersistsAttemptWithoutUserId()
    {
        var username = $"unknown-{Guid.NewGuid():N}"[..20];
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username, password = "not-the-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var attempt = await scope.ServiceProvider.GetRequiredService<AppDbContext>().LoginAttempts
            .SingleAsync(item => item.Username == username);
        Assert.Null(attempt.UserId);
        Assert.False(attempt.IsSuccessful);
    }

    [Fact]
    public async Task AdminLoginAttempts_RegularUserIsForbiddenAndAdminIsAllowed()
    {
        var regularClient = factory.CreateClient();
        regularClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(regularClient, "aleks", "aleks"));
        Assert.Equal(HttpStatusCode.Forbidden, (await regularClient.GetAsync("/api/admin/login-attempts")).StatusCode);

        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(adminClient, "admin", "admin"));
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/api/admin/login-attempts")).StatusCode);
    }

    [Fact]
    public async Task AdminLoginAttempts_FiltersOrdersAndPagesResults()
    {
        var marker = $"filter-{Guid.NewGuid():N}"[..20];
        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.LoginAttempts.AddRange(
                Attempt(marker, false, now.AddMinutes(-2)),
                Attempt(marker, true, now.AddMinutes(-1)),
                Attempt(marker, false, now));
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, "admin", "admin"));
        using var firstPage = JsonDocument.Parse(await client.GetStringAsync($"/api/admin/login-attempts?username={marker}&success=false&page=1&pageSize=1"));
        Assert.Equal(2, firstPage.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, firstPage.RootElement.GetProperty("totalPages").GetInt32());
        Assert.Equal(now, firstPage.RootElement.GetProperty("items")[0].GetProperty("attemptedAtUtc").GetDateTimeOffset());

        using var secondPage = JsonDocument.Parse(await client.GetStringAsync($"/api/admin/login-attempts?username={marker}&success=false&page=2&pageSize=1"));
        Assert.Equal(now.AddMinutes(-2), secondPage.RootElement.GetProperty("items")[0].GetProperty("attemptedAtUtc").GetDateTimeOffset());
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
        var userId = json.RootElement.GetProperty("user").GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.DiabetesSettings.CountAsync(item => item.UserId == userId));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings")).StatusCode);
        // Simulate a legacy/imported user missing the otherwise atomic registration settings.
        var missing = await db.DiabetesSettings.SingleAsync(item => item.UserId == userId);
        db.DiabetesSettings.Remove(missing);
        await db.SaveChangesAsync(CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(1, await db.DiabetesSettings.CountAsync(item => item.UserId == userId));
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

    [Fact]
    public async Task Health_WithoutToken_ReturnsHealthy()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Empty(response.Headers.WwwAuthenticate);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task Health_ReportsCommitOnlyForRenderPreview(string isPreview, bool expectCommit)
    {
        const string commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        using var previewFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["IS_PULL_REQUEST"] = isPreview, ["RENDER_GIT_COMMIT"] = commit })));
        using var client = previewFactory.CreateClient();
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal(expectCommit, response.Headers.Contains("X-Preview-Commit"));
        if (expectCommit) Assert.Equal(commit, Assert.Single(response.Headers.GetValues("X-Preview-Commit")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/foods")).StatusCode);
    }

    [Theory]
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
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Bearer");
    }

    [Fact]
    public async Task RegisterAndLogin_NewUserStartsEmptyAndCannotAccessAleksFood()
    {
        var username = $"new-{Guid.NewGuid():N}"[..20];
        var client = factory.CreateClient();
        var registration = await client.PostAsJsonAsync("/api/auth/register", new { username, password = "test-password" });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        using var registered = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        var userId = registered.RootElement.GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.DiabetesSettings.AsNoTracking().SingleAsync(item => item.UserId == userId);
        Assert.NotEqual(userId, settings.Id);
        Assert.Equal(6.5m, settings.TargetGlucose);
        Assert.Equal(10m, settings.CarbRatio);
        Assert.Equal(3m, settings.CorrectionFactor);
        Assert.Equal(4m, settings.InsulinDurationHours);

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new { username, password = "another-password" });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(1, await db.DiabetesSettings.CountAsync(item => item.UserId == userId));

        var token = await LoginAsync(client, username, "test-password");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings")).StatusCode);
        using var foods = JsonDocument.Parse(await client.GetStringAsync("/api/foods?pageSize=100"));
        Assert.Equal(0, foods.RootElement.GetProperty("totalCount").GetInt32());

        var response = await client.PutAsJsonAsync(
            "/api/foods/33333333-3333-3333-3333-333333333301",
            new { name = "Not mine", measurementType = "Grams", carbsPer100g = 1m, proteinPer100g = 0m, fatPer100g = 0m, caloriesPer100g = 0m, isFavorite = false });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var calculated = await client.PostAsJsonAsync("/api/meals/calculate", new
        {
            mealType = "Lunch", preMealGlucose = 6.5m, directCarbs = 20m, items = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.OK, calculated.StatusCode);
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

    [Fact]
    public async Task AssistantSelection_UsesAuthorizedFoodAndIgnoresClientCarbohydrateValues()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, "test", "test"));
        using var created = await client.PostAsJsonAsync("/api/foods", new
        {
            name = "Selected salad " + Guid.NewGuid().ToString("N"), measurementType = "Portion", carbsPerUnit = 5m,
            proteinPer100g = 0m, fatPer100g = 0m, caloriesPer100g = 0m, isFavorite = false
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var food = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var foodId = food.RootElement.GetProperty("id").GetGuid();
        using var response = await client.PostAsJsonAsync("/api/meal-assistant/messages", new
        {
            message = "Use this saved food", requestId = Guid.NewGuid(),
            selection = new { foodId, quantity = 2m, unit = "Portion", carbsPerUnit = 999999m }
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(10m, answer.RootElement.GetProperty("draft").GetProperty("totalCarbs").GetDecimal());
        Assert.Equal(foodId, answer.RootElement.GetProperty("draft").GetProperty("items")[0].GetProperty("foodItemId").GetGuid());
    }

    [Fact]
    public async Task AssistantSelection_UnavailableId_ReturnsProblemDetails400()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, "test", "test"));
        using var response = await client.PostAsJsonAsync("/api/meal-assistant/messages", new
        {
            message = "Use this saved food", requestId = Guid.NewGuid(), selection = new { foodId = Guid.NewGuid(), quantity = 1m }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Validation failed", problem.RootElement.GetProperty("title").GetString());
        Assert.Contains("no longer available", problem.RootElement.GetProperty("detail").GetString());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task AssistantMutation_StaleVersion_ReturnsMachineReadableConflictWithoutChangingDraft()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, "test", "test"));
        using var created = await client.PostAsJsonAsync("/api/foods", new
        {
            name = "Versioned food " + Guid.NewGuid().ToString("N"), measurementType = "Portion", carbsPerUnit = 5m,
            proteinPer100g = 0m, fatPer100g = 0m, caloriesPer100g = 0m, isFavorite = false
        });
        using var food = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var foodId = food.RootElement.GetProperty("id").GetGuid();
        using var initial = await client.PostAsJsonAsync("/api/meal-assistant/messages", new
        { message = "Choose this food", requestId = Guid.NewGuid(), selection = new { foodId, quantity = 1m }, expectedDraftVersion = 0 });
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        using var answer = JsonDocument.Parse(await initial.Content.ReadAsStringAsync());
        var conversationId = answer.RootElement.GetProperty("conversationId").GetGuid();
        Assert.Equal(1, answer.RootElement.GetProperty("draftVersion").GetInt64());

        using var stale = await client.PostAsJsonAsync("/api/meal-assistant/messages", new
        { conversationId, message = "Add another food", requestId = Guid.NewGuid(), selection = new { foodId, quantity = 1m }, expectedDraftVersion = 0 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var problem = JsonDocument.Parse(await stale.Content.ReadAsStringAsync());
        Assert.Equal(409, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Assistant request conflict", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("draft_version_conflict", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(1, problem.RootElement.GetProperty("currentDraftVersion").GetInt64());
        Assert.True(problem.RootElement.TryGetProperty("detail", out _));
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        using var state = JsonDocument.Parse(await client.GetStringAsync($"/api/meal-assistant/conversations/{conversationId}/draft-state"));
        Assert.Equal(1, state.RootElement.GetProperty("draftVersion").GetInt64());
        Assert.Equal(5m, state.RootElement.GetProperty("draft").GetProperty("totalCarbs").GetDecimal());
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static LoginAttempt Attempt(string username, bool successful, DateTimeOffset attemptedAt) => new()
    {
        Id = Guid.NewGuid(), Username = username, IsSuccessful = successful, AttemptedAtUtc = attemptedAt,
        FailureReason = successful ? null : "InvalidCredentials"
    };

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
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
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=auth_tests;Username=test;Password=test",
                ["Jwt:SigningKey"] = "authentication-integration-test-key-32-chars"
            }));
            var host = base.CreateHost(builder);
            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
            return host;
        }

    }
}
