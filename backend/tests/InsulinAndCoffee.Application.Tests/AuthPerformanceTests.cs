using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsulinAndCoffee.Api.Authentication;
using InsulinAndCoffee.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace InsulinAndCoffee.Application.Tests;

public partial class AssistantPostgreSqlSafetyTests
{
    [AssistantPostgresFact]
    public async Task Login_LocalPostgres_RecordsSafeStageTimingsAndRepairsMissingSettingsAfterNavigation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var owner = Guid.NewGuid();
        var username = "timing-" + owner.ToString("N");
        const string password = "synthetic-timing-password";
        await using (var setup = database.Open())
        {
            // Legacy user without settings; normal registration is covered in AuthIntegrationTests.
            setup.Users.Add(new User { Id = owner, Username = username, NormalizedUsername = username.ToUpperInvariant(),
                PasswordHash = new PasswordService().HashPassword(password), CreatedAt = TimeProvider.System.GetUtcNow() });
            await setup.SaveChangesAsync(CancellationToken.None);
        }
        using var logs = new AuthTimingLogProvider();
        using var original = new StartupFactory(database.ConnectionString);
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateClient();
        using (var pool = new NpgsqlConnection(database.ConnectionString)) NpgsqlConnection.ClearPool(pool);

        string? token = null;
        for (var sample = 0; sample < 6; sample++)
        {
            logs.Events.Clear();
            var elapsed = Stopwatch.StartNew();
            var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
            var httpMs = elapsed.Elapsed.TotalMilliseconds;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            token = json.RootElement.GetProperty("accessToken").GetString();
            var entries = logs.Events.ToArray();
            var timing = Assert.Single(entries, entry => entry.ContainsKey("UserLookupMs"));
            Assert.Equal("Success", timing["Outcome"]);
            Assert.NotEmpty(Assert.IsType<string>(timing["TraceId"]));
            Assert.All(new[] { "TotalMs", "UserLookupMs", "PasswordVerificationMs", "AuditWriteMs", "JwtGenerationMs" },
                key => Assert.True(Convert.ToDouble(timing[key]) >= 0));
            // One indexed user query and one audit INSERT; no settings/profile query in login.
            Assert.Equal(2, entries.Count(entry => entry.ContainsKey("Operation")));
            var httpTiming = Assert.Single(entries, entry => entry.ContainsKey("StatusCode"));
            Assert.Equal(200, httpTiming["StatusCode"]);
            Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), httpTiming["CorrelationId"]);
            Assert.Contains(entries, entry => entry.ContainsKey("Failed") && !entry.ContainsKey("Operation"));
            var serialized = JsonSerializer.Serialize(entries);
            Assert.DoesNotContain(username, serialized);
            Assert.DoesNotContain(password, serialized);
            Assert.DoesNotContain(token!, serialized);
            Console.WriteLine($"AUTH_SAMPLE {sample} LocalHttpMs={httpMs:F3} {serialized}");
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        logs.Events.Clear();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings")).StatusCode);
        Console.WriteLine("AUTH_POST_LOGIN " + JsonSerializer.Serialize(logs.Events));
        await using (var check = database.Open())
        {
            var settings = Assert.Single(await check.DiabetesSettings.Where(settings => settings.UserId == owner).ToListAsync());
            Assert.Equal(DiabetesSettings.CreateDefaults(Guid.NewGuid(), owner, settings.UpdatedAt).TargetGlucose, settings.TargetGlucose);
            Assert.Equal(6, await check.LoginAttempts.CountAsync(attempt => attempt.UserId == owner));
        }
        logs.Events.Clear();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { username, password = "invalid" })).StatusCode);
        Assert.Equal("InvalidCredentials", Assert.Single(logs.Events, entry => entry.ContainsKey("Outcome"))["Outcome"]);
        Assert.Contains(logs.Events, entry => Equals(entry.GetValueOrDefault("StatusCode"), 401));
    }

    private sealed class AuthTimingLogProvider : ILoggerProvider
    {
        public ConcurrentQueue<Dictionary<string, object?>> Events { get; } = new();
        public ILogger CreateLogger(string categoryName) => new TimingLogger(this,
            categoryName.EndsWith("AuthService", StringComparison.Ordinal) || categoryName.Contains("Timing", StringComparison.Ordinal));
        public void Dispose() { }
        private sealed class TimingLogger(AuthTimingLogProvider owner, bool enabled) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => enabled && logLevel == LogLevel.Information;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel) && state is IEnumerable<KeyValuePair<string, object?>> fields)
                    owner.Events.Enqueue(fields.Where(field => field.Key != "{OriginalFormat}").ToDictionary());
            }
        }
    }
}
