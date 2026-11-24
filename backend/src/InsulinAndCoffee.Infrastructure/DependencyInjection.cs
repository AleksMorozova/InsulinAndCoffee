using InsulinAndCoffee.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InsulinAndCoffee.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found or is empty.");
        }

        services.AddSingleton<AuthenticationDatabaseTimingInterceptor>();
        services.AddSingleton<AuthenticationConnectionTimingInterceptor>();
        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseNpgsql(connectionString).AddInterceptors(
                provider.GetRequiredService<AuthenticationDatabaseTimingInterceptor>(),
                provider.GetRequiredService<AuthenticationConnectionTimingInterceptor>()));

        services.AddScoped<IAppDbContext>(provider =>
            provider.GetRequiredService<AppDbContext>());
        services.AddSingleton<IAiUsageWriter, AiUsageWriter>();

        return services;
    }
}
