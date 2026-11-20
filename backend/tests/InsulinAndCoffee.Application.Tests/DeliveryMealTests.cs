using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Tests;

public class DeliveryMealTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task CreateAndReuse_PreservesUnrecordedAndExplicitZeroDose(int? dose)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new DeliveryMealService(db, TimeProvider.System, new CurrentUser(Guid.NewGuid()));
        var order = await service.CreateAsync(new UpsertDeliveryMealRequest("Restaurant", "Combination",
            "1 portion and 5 pieces", 135m, dose, null, ResultRating.Unknown, "", null, false), CancellationToken.None);

        var visible = Assert.Single((await service.GetSectionsAsync("Restaurant", CancellationToken.None)).SearchResults);
        var draft = await service.CreateMealDraftFromDeliveryMealAsync(order.Id, CancellationToken.None);

        Assert.Equal((decimal?)dose, visible.UsualInsulinUnits);
        Assert.Equal((decimal?)dose, draft.UsualInsulinUnits);
        Assert.Equal(135m, draft.Carbs);
        Assert.Contains("1 portion and 5 pieces", draft.Notes);
        Assert.Empty(db.Meals);
    }

    private sealed record CurrentUser(Guid UserId) : ICurrentUser;
}
