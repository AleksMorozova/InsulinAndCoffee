using InsulinAndCoffee.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace InsulinAndCoffee.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<AdminLoginAttemptService>();
        services.AddScoped<AiMonitoringService>();
        services.AddScoped<InsulinAndCoffee.Application.Abstractions.AiRequestContext>();
        services.AddScoped<FoodService>();
        services.AddScoped<DeliveryMealService>();
        services.AddScoped<MealCalculationService>();
        services.AddScoped<MealService>();
        services.AddScoped<MealHistorySearchService>();
        services.AddScoped<MealAssistantService>();
        services.AddScoped<AssistantDraftBuilder>();
        services.AddScoped<FoodKnowledgeSearchService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<SupplyService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
