using InsulinAndCoffee.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace InsulinAndCoffee.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<FoodService>();
        services.AddScoped<KnownMealService>();
        services.AddScoped<MealService>();
        services.AddScoped<SettingsService>();
        return services;
    }
}
