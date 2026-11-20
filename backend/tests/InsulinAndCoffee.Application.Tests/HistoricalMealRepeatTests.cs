using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Calculations;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Domain.Exceptions;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Tests;

public class HistoricalMealRepeatTests
{
    [Theory]
    [InlineData(null, 0, false)]
    [InlineData(18, 0, false)]
    [InlineData(null, 5, false)]
    [InlineData(null, -5, false)]
    [InlineData(18, 5, true)]
    [InlineData(0, 0, true)]
    public async Task CreateMealAsync_RepeatAfterFoodEditOrDeletion_PreservesSnapshotsAndAdjustment(
        int? overrideCarbs, int adjustment, bool deleteFood)
    {
        await using var db = CreateDb();
        var owner = new CurrentUser(Guid.NewGuid());
        db.DiabetesSettings.Add(DiabetesSettings.CreateDefaults(Guid.NewGuid(), owner.UserId,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        var food = new FoodItem { Id = Guid.NewGuid(), UserId = owner.UserId, Name = "Original soup",
            MeasurementType = FoodMeasurementType.Grams, CarbsPer100g = 10m };
        db.FoodItems.Add(food);
        await db.SaveChangesAsync();
        var calculation = new MealCalculationService(db, owner);
        var service = new MealService(db, TimeProvider.System, calculation, owner);
        var original = await service.CreateMealAsync(new CreateMealRequest(MealType.Lunch, null, 9m, 4m,
            "Original", [new MealItemInputDto(food.Id, 150m, CarbOverride: overrideCarbs)],
            CarbAdjustment: adjustment), CancellationToken.None);
        if (deleteFood) db.FoodItems.Remove(food);
        else
        {
            food.Name = "Edited soup";
            food.MeasurementType = FoodMeasurementType.Portion;
            food.CarbsPer100g = null;
            food.CarbsPerUnit = 99m;
        }
        await db.SaveChangesAsync();

        var source = await service.GetMealAsync(original.Id, CancellationToken.None);
        var item = Assert.Single(source.Items);
        var input = new MealItemInputDto(item.FoodItemId, item.Quantity, item.WeightGrams,
            item.MeasurementType, item.FoodNameSnapshot, item.CarbsPer100gSnapshot,
            item.CarbsPerUnitSnapshot, item.CarbOverride);
        var repeated = await service.CreateMealAsync(new CreateMealRequest(source.MealType, null, 6.8m, null,
            null, [input], CarbAdjustment: source.CarbAdjustment), CancellationToken.None);

        var repeatedItem = Assert.Single(repeated.Items);
        Assert.Equal(source.TotalCarbs, repeated.TotalCarbs);
        Assert.Equal(source.CarbAdjustment, repeated.CarbAdjustment);
        Assert.Equal(item with { Id = repeatedItem.Id }, repeatedItem);
        Assert.Equal(6.8m, repeated.PreMealGlucose);
        Assert.Null(repeated.ConfirmedBolus);
        var modified = await service.UpdateMealItemAsync(repeated.Id, repeatedItem.Id,
            new UpdateMealItemRequest(200m, CarbOverride: repeatedItem.CarbOverride), CancellationToken.None);
        Assert.Equal((overrideCarbs ?? 20m) + adjustment, modified.TotalCarbs);
        Assert.Equal((decimal)adjustment, modified.CarbAdjustment);
        var unchanged = await service.GetMealAsync(original.Id, CancellationToken.None);
        Assert.Equal(original.TotalCarbs, unchanged.TotalCarbs);
        Assert.Equal(original.CarbAdjustment, unchanged.CarbAdjustment);
        Assert.Equal(original.ConfirmedBolus, unchanged.ConfirmedBolus);
        Assert.Equal(original.PreMealGlucose, unchanged.PreMealGlucose);
        Assert.Equal(original.Items.ToArray(), unchanged.Items.ToArray());
    }

    [Theory]
    [InlineData(FoodMeasurementType.Grams, null, 20)]
    [InlineData(FoodMeasurementType.Grams, -1, 20)]
    [InlineData(FoodMeasurementType.Portion, 20, null)]
    [InlineData(FoodMeasurementType.Portion, 20, -1)]
    [InlineData(FoodMeasurementType.Piece, 20, null)]
    [InlineData(FoodMeasurementType.Piece, 20, -1)]
    [InlineData((FoodMeasurementType)99, 20, 20)]
    public async Task PreviewCarbsAsync_InvalidLegacyFood_RejectsMissingNegativeOrUnsupportedBasis(
        FoodMeasurementType unit, int? per100g, int? perUnit)
    {
        await using var db = CreateDb();
        var owner = new CurrentUser(Guid.NewGuid());
        var food = new FoodItem { Id = Guid.NewGuid(), UserId = owner.UserId, Name = "Legacy food",
            MeasurementType = unit, CarbsPer100g = per100g, CarbsPerUnit = perUnit };
        db.FoodItems.Add(food);
        await db.SaveChangesAsync();
        var service = new MealCalculationService(db, owner);
        await Assert.ThrowsAsync<CarbCalculationException>(() => service.PreviewCarbsAsync(
            new MealCarbPreviewRequest([new MealItemInputDto(food.Id, 1m)], 0m), CancellationToken.None));
    }

    [Theory]
    [InlineData(FoodMeasurementType.Grams)]
    [InlineData(FoodMeasurementType.Portion)]
    [InlineData(FoodMeasurementType.Piece)]
    public async Task PreviewCarbsAsync_ExplicitZeroBasis_ReturnsZero(FoodMeasurementType unit)
    {
        await using var db = CreateDb();
        var service = new MealCalculationService(db);
        var food = new FoodItem { Id = Guid.NewGuid(), UserId = DefaultUser.Id, Name = "Zero carb",
            MeasurementType = unit, CarbsPer100g = unit == FoodMeasurementType.Grams ? 0m : null,
            CarbsPerUnit = unit == FoodMeasurementType.Grams ? null : 0m };
        db.FoodItems.Add(food);
        await db.SaveChangesAsync();
        var catalogPreview = await service.PreviewCarbsAsync(
            new([new MealItemInputDto(food.Id, 2m)], 0m), CancellationToken.None);
        Assert.Equal(0m, catalogPreview.TotalCarbs);
        var input = new MealItemInputDto(Guid.NewGuid(), 2m, MeasurementType: unit,
            FoodNameSnapshot: "Zero carb", CarbsPer100gSnapshot: 0m, CarbsPerUnitSnapshot: 0m);
        var preview = await service.PreviewCarbsAsync(new([input], 0m), CancellationToken.None);
        Assert.Equal(0m, preview.TotalCarbs);
    }

    [Fact]
    public void Calculate_PrecisionSensitiveBases_PreservesMidpointToEvenRounding()
    {
        Assert.Equal(1.00m, FoodCarbCalculator.Calculate(FoodMeasurementType.Grams, 100m, 1.005m, null));
        Assert.Equal(1.02m, FoodCarbCalculator.Calculate(FoodMeasurementType.Portion, 1m, null, 1.015m));
    }

    [Theory]
    [InlineData(FoodMeasurementType.Grams)]
    [InlineData(FoodMeasurementType.Portion)]
    [InlineData(FoodMeasurementType.Piece)]
    public async Task PreviewCarbsAsync_SnapshotHasOnlyWrongBasis_ThrowsValidation(FoodMeasurementType unit)
    {
        await using var db = CreateDb();
        var service = new MealCalculationService(db);
        var input = new MealItemInputDto(Guid.NewGuid(), 2m, MeasurementType: unit,
            FoodNameSnapshot: "Invalid snapshot", CarbsPer100gSnapshot: unit == FoodMeasurementType.Grams ? null : 20m,
            CarbsPerUnitSnapshot: unit == FoodMeasurementType.Grams ? 20m : null);
        await Assert.ThrowsAsync<ValidationException>(() => service.PreviewCarbsAsync(new([input], 0m), CancellationToken.None));
    }

    [Fact]
    public async Task GetMealAsync_AnotherUsersMeal_ThrowsNotFound()
    {
        await using var db = CreateDb();
        var meal = new Meal { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        var owner = new CurrentUser(Guid.NewGuid());
        var service = new MealService(db, TimeProvider.System, new MealCalculationService(db, owner), owner);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetMealAsync(meal.Id, CancellationToken.None));
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed record CurrentUser(Guid UserId) : ICurrentUser;
}
