using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
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

    [Fact]
    public async Task CreateFoodAsync_ForGramsFood_RequiresCarbsPer100g()
    {
        await using var db = CreateDbContext();
        var service = new FoodService(db, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateFoodAsync(
                new UpsertFoodItemRequest("Buckwheat", FoodMeasurementType.Grams, null, null, 0, 0, 0, false),
                CancellationToken.None));

        Assert.Equal("Carbs per 100 g must be zero or greater.", exception.Message);
    }

    [Fact]
    public async Task CreateFoodAsync_ForPortionFood_RequiresCarbsPerUnit()
    {
        await using var db = CreateDbContext();
        var service = new FoodService(db, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateFoodAsync(
                new UpsertFoodItemRequest("Borscht", FoodMeasurementType.Portion, null, null, 0, 0, 0, false),
                CancellationToken.None));

        Assert.Equal("Carbs per portion must be zero or greater.", exception.Message);
    }

    [Fact]
    public async Task CreateFoodAsync_ForPieceFood_SavesCarbsPerUnit()
    {
        await using var db = CreateDbContext();
        var service = new FoodService(db, TimeProvider.System);

        var result = await service.CreateFoodAsync(
            new UpsertFoodItemRequest("Cheesecake", FoodMeasurementType.Piece, null, 8m, 0, 0, 0, false),
            CancellationToken.None);

        Assert.Equal(FoodMeasurementType.Piece, result.MeasurementType);
        Assert.Null(result.CarbsPer100g);
        Assert.Equal(8m, result.CarbsPerUnit);
    }

    [Fact]
    public async Task CreateFoodAsync_ForPortionFood_PreservesNutritionValuesForSelectedBasis()
    {
        await using var db = CreateDbContext();
        var service = new FoodService(db, TimeProvider.System);

        var result = await service.CreateFoodAsync(
            new UpsertFoodItemRequest("Soup", FoodMeasurementType.Portion, null, 20m, 4m, 2m, 60m, false),
            CancellationToken.None);

        Assert.Equal(FoodMeasurementType.Portion, result.MeasurementType);
        Assert.Equal(20m, result.CarbsPerUnit);
        Assert.Equal(4m, result.ProteinPer100g);
        Assert.Equal(2m, result.FatPer100g);
        Assert.Equal(60m, result.CaloriesPer100g);
    }

    [Fact]
    public async Task UpdateFoodAsync_WhenSwitchingFromGramsToPortion_ClearsStaleCarbsPer100g()
    {
        await using var db = CreateDbContext();
        var food = Food("Soup", isFavorite: false);
        food.CarbsPer100g = 5m;
        db.FoodItems.Add(food);
        await db.SaveChangesAsync();
        var service = new FoodService(db, TimeProvider.System);

        var result = await service.UpdateFoodAsync(
            food.Id,
            new UpsertFoodItemRequest("Soup", FoodMeasurementType.Portion, 5m, 20m, 4m, 2m, 60m, false),
            CancellationToken.None);

        Assert.Equal(FoodMeasurementType.Portion, result.MeasurementType);
        Assert.Null(result.CarbsPer100g);
        Assert.Equal(20m, result.CarbsPerUnit);
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
