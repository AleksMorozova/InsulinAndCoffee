using System.Net;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsulinAndCoffee.Application.Tests;

public class OpenAiLlmClientTests
{
    [Fact]
    public async Task Interpret_ReadsSingleAllowedCall()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Reply(
            """{"choices":[{"message":{"tool_calls":[{"function":{"name":"search_meal_history","arguments":"{\"foods\":[{\"name\":\"Porridge\",\"quantity\":180,\"unit\":\"Grams\"}]}"}}]}}]}"""))));
        var result = await Client(http).InterpretAsync([new("user", "Find porridge")], default);
        Assert.Equal(180m, Assert.Single(result.Foods).Quantity);
    }

    [Theory]
    [InlineData("invalid JSON")]
    [InlineData("""{"choices":[{"message":{"tool_calls":[{"function":{"name":"get_user","arguments":"{}"}}]}}]}""")]
    [InlineData("""{"choices":[{"message":{"tool_calls":[{},{}]}}]}""")]
    [InlineData("""{"choices":[{"message":{"tool_calls":[{"function":{"name":"search_meal_history","arguments":"{\"foods\":[],\"userId\":\"other\"}"}}]}}]}""")]
    public async Task Interpret_RejectsMalformedUnknownOrMultipleCalls(string body)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Reply(body))));
        await Assert.ThrowsAsync<MealAssistantUnavailableException>(() => Client(http).InterpretAsync([], default));
    }

    [Fact]
    public async Task Interpret_HandlesProviderFailure()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests))));
        await Assert.ThrowsAsync<MealAssistantUnavailableException>(() => Client(http).InterpretAsync([], default));
    }

    [Fact]
    public async Task Interpret_HandlesTimeout()
    {
        using var http = new HttpClient(new Handler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Reply("");
        }));
        await Assert.ThrowsAsync<MealAssistantUnavailableException>(() => Client(http).InterpretAsync([], default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Interpret_DevelopmentConfiguration_UsesSecretOverrideOrReportsMissingKey(bool hasKey)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json")
            .AddInMemoryCollection(hasKey
                ? new Dictionary<string, string?> { ["MealAssistant:ApiKey"] = "test-secret" }
                : new Dictionary<string, string?> { ["MealAssistant:ApiKey"] = "" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        var requestCount = 0;
        services.AddHttpClient<ILlmClient, OpenAiLlmClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new Handler(async (request, token) =>
            {
                requestCount++;
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.True(request.Headers.Authorization?.Parameter == "test-secret", "The synthetic secret override was not used.");
                using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                Assert.Equal("gpt-4.1-mini", payload.RootElement.GetProperty("model").GetString());
                Assert.Equal(500, payload.RootElement.GetProperty("max_completion_tokens").GetInt32());
                Assert.False(payload.RootElement.GetProperty("parallel_tool_calls").GetBoolean());
                return Reply("""{"choices":[{"message":{"tool_calls":[{"function":{"name":"search_meal_history","arguments":"{\"foods\":[]}"}}]}}]}""");
            }));
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILlmClient>();
        Assert.Equal("OpenAI", configuration["MealAssistant:Provider"]);
        Assert.Equal("15", configuration["MealAssistant:TimeoutSeconds"]);

        if (hasKey)
        {
            var result = await client.InterpretAsync([], CancellationToken.None);
            Assert.Empty(result.Foods);
            Assert.Equal(1, requestCount);
        }
        else
        {
            var error = await Assert.ThrowsAsync<MealAssistantUnavailableException>(
                () => client.InterpretAsync([], CancellationToken.None));
            Assert.Contains("MealAssistant:ApiKey", error.Message);
            Assert.Equal(0, requestCount);
        }
    }

    private static OpenAiLlmClient Client(HttpClient http) => new(http, new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MealAssistant:ApiKey"] = "test-key",
            ["MealAssistant:Model"] = "test-model",
            ["MealAssistant:TimeoutSeconds"] = "1"
        }).Build(), NullLogger<OpenAiLlmClient>.Instance);

    private static HttpResponseMessage Reply(string content) => new(HttpStatusCode.OK) { Content = new StringContent(content) };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
