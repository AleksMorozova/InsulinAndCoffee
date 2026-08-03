using InsulinAndCoffee.Application.Dtos;

namespace InsulinAndCoffee.Application.Services;

public class MockTypeScriptDeliveryMealService
{
    public Task ShadowAsync(string operationName, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<DeliveryMealSectionsDto> GetSectionsAsync(string? search, CancellationToken cancellationToken) =>
        NotMigrated<DeliveryMealSectionsDto>();

    public Task<DeliveryMealDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        NotMigrated<DeliveryMealDto>();

    public Task<DeliveryMealDto> CreateAsync(UpsertDeliveryMealRequest request, CancellationToken cancellationToken) =>
        NotMigrated<DeliveryMealDto>();

    public Task<DeliveryMealDto> CreateFromMealAsync(Guid mealId, CreateDeliveryMealFromMealRequest request, CancellationToken cancellationToken) =>
        NotMigrated<DeliveryMealDto>();

    public Task<DeliveryMealDto> UpdateAsync(Guid id, UpsertDeliveryMealRequest request, CancellationToken cancellationToken) =>
        NotMigrated<DeliveryMealDto>();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        NotMigrated();

    public Task<DeliveryMealDto> ToggleFavoriteAsync(Guid id, CancellationToken cancellationToken) =>
        NotMigrated<DeliveryMealDto>();

    public Task<UseDeliveryMealDto> CreateMealDraftFromDeliveryMealAsync(Guid id, CancellationToken cancellationToken) =>
        NotMigrated<UseDeliveryMealDto>();

    private static Task NotMigrated() =>
        Task.FromException(new NotImplementedException("The mock future TypeScript delivery-meal implementation is not implemented. Switch routing mode back to Legacy for real behavior."));

    private static Task<T> NotMigrated<T>() =>
        Task.FromException<T>(new NotImplementedException("The mock future TypeScript delivery-meal implementation is not implemented. Switch routing mode back to Legacy for real behavior."));
}
