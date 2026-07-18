using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Tests;

public class FoodServiceTests
{
    [Fact]
    public async Task GetFoodsAsync_FiltersOrdersAndPaginatesResults()
    {
        await using var db = CreateDbContext();
        db.FoodItems.AddRange(
            Food("Delta", isFavorite: false),
            Food("Charlie", isFavorite: true),
            Food("Bravo", isFavorite: false),
            Food("Alpha", isFavorite: true),
            Food("Other user's food", isFavorite: true, userId: Guid.NewGuid()));
        await db.SaveChangesAsync();
        var service = new FoodService(db, TimeProvider.System);

        var result = await service.GetFoodsAsync(search: null, page: 2, pageSize: 2, CancellationToken.None);

        Assert.Equal(["Bravo", "Delta"], result.Items.Select(item => item.Name));
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task GetFoodsAsync_AppliesSearchBeforePagination()
    {
        await using var db = CreateDbContext();
        db.FoodItems.AddRange(
            Food("Apple", isFavorite: true),
            Food("Pineapple", isFavorite: false),
            Food("Banana", isFavorite: true));
        await db.SaveChangesAsync();
        var service = new FoodService(db, TimeProvider.System);

        var result = await service.GetFoodsAsync(" APP ", page: 1, pageSize: 1, CancellationToken.None);

        Assert.Equal("Apple", Assert.Single(result.Items).Name);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetFoodsAsync_WhenPaginationIsInvalid_ThrowsValidation(int page, int pageSize)
    {
        await using var db = CreateDbContext();
        var service = new FoodService(db, TimeProvider.System);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.GetFoodsAsync(search: null, page, pageSize, CancellationToken.None));
    }

    private static FoodItem Food(string name, bool isFavorite, Guid? userId = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId ?? DefaultUser.Id,
        Name = name,
        IsFavorite = isFavorite,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
