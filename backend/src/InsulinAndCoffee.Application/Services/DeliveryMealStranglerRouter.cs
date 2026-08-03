using InsulinAndCoffee.Application.Dtos;

namespace InsulinAndCoffee.Application.Services;

public class DeliveryMealStranglerRouter(
    DeliveryMealService legacyService,
    MockTypeScriptDeliveryMealService mockTypeScriptService,
    DeliveryMealStranglerOptions options)
{
    public Task<DeliveryMealSectionsDto> GetSectionsAsync(string? search, CancellationToken cancellationToken) =>
        RouteAsync(
            operationName: nameof(GetSectionsAsync),
            legacy: () => legacyService.GetSectionsAsync(search, cancellationToken),
            migrated: () => mockTypeScriptService.GetSectionsAsync(search, cancellationToken),
            cancellationToken);

    public Task<DeliveryMealDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        RouteAsync(
            operationName: nameof(GetByIdAsync),
            legacy: () => legacyService.GetByIdAsync(id, cancellationToken),
            migrated: () => mockTypeScriptService.GetByIdAsync(id, cancellationToken),
            cancellationToken);

    public Task<DeliveryMealDto> CreateAsync(UpsertDeliveryMealRequest request, CancellationToken cancellationToken) =>
        RouteAsync(
            operationName: nameof(CreateAsync),
            legacy: () => legacyService.CreateAsync(request, cancellationToken),
            migrated: () => mockTypeScriptService.CreateAsync(request, cancellationToken),
            cancellationToken);

    public Task<DeliveryMealDto> CreateFromMealAsync(Guid mealId, CreateDeliveryMealFromMealRequest request, CancellationToken cancellationToken) =>
        RouteAsync(
            operationName: nameof(CreateFromMealAsync),
            legacy: () => legacyService.CreateFromMealAsync(mealId, request, cancellationToken),
            migrated: () => mockTypeScriptService.CreateFromMealAsync(mealId, request, cancellationToken),
            cancellationToken);

    public Task<DeliveryMealDto> UpdateAsync(Guid id, UpsertDeliveryMealRequest request, CancellationToken cancellationToken) =>
        RouteAsync(
            operationName: nameof(UpdateAsync),
            legacy: () => legacyService.UpdateAsync(id, request, cancellationToken),
            migrated: () => mockTypeScriptService.UpdateAsync(id, request, cancellationToken),
            cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        RouteAsync(
            operationName: nameof(DeleteAsync),
            legacy: () => legacyService.DeleteAsync(id, cancellationToken),
            migrated: () => mockTypeScriptService.DeleteAsync(id, cancellationToken),
            cancellationToken);

    public Task<DeliveryMealDto> ToggleFavoriteAsync(Guid id, CancellationToken cancellationToken) =>
        RouteAsync(
            operationName: nameof(ToggleFavoriteAsync),
            legacy: () => legacyService.ToggleFavoriteAsync(id, cancellationToken),
            migrated: () => mockTypeScriptService.ToggleFavoriteAsync(id, cancellationToken),
            cancellationToken);

    public Task<UseDeliveryMealDto> CreateMealDraftFromDeliveryMealAsync(Guid id, CancellationToken cancellationToken) =>
        RouteAsync(
            operationName: nameof(CreateMealDraftFromDeliveryMealAsync),
            legacy: () => legacyService.CreateMealDraftFromDeliveryMealAsync(id, cancellationToken),
            migrated: () => mockTypeScriptService.CreateMealDraftFromDeliveryMealAsync(id, cancellationToken),
            cancellationToken);

    private async Task<T> RouteAsync<T>(
        string operationName,
        Func<Task<T>> legacy,
        Func<Task<T>> migrated,
        CancellationToken cancellationToken)
    {
        return options.RoutingMode switch
        {
            DeliveryMealRoutingMode.Legacy => await legacy(),
            DeliveryMealRoutingMode.Shadow => await RunLegacyWithShadowAsync(operationName, legacy, cancellationToken),
            DeliveryMealRoutingMode.Migrated => await migrated(),
            _ => await legacy()
        };
    }

    private async Task RouteAsync(
        string operationName,
        Func<Task> legacy,
        Func<Task> migrated,
        CancellationToken cancellationToken)
    {
        switch (options.RoutingMode)
        {
            case DeliveryMealRoutingMode.Legacy:
                await legacy();
                break;
            case DeliveryMealRoutingMode.Shadow:
                await RunLegacyWithShadowAsync(operationName, legacy, cancellationToken);
                break;
            case DeliveryMealRoutingMode.Migrated:
                await migrated();
                break;
            default:
                await legacy();
                break;
        }
    }

    private async Task<T> RunLegacyWithShadowAsync<T>(
        string operationName,
        Func<Task<T>> legacy,
        CancellationToken cancellationToken)
    {
        var result = await legacy();
        await mockTypeScriptService.ShadowAsync(operationName, cancellationToken);
        return result;
    }

    private async Task RunLegacyWithShadowAsync(
        string operationName,
        Func<Task> legacy,
        CancellationToken cancellationToken)
    {
        await legacy();
        await mockTypeScriptService.ShadowAsync(operationName, cancellationToken);
    }
}
