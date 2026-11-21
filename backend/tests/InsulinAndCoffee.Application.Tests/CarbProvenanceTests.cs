using System.Text.Json;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Domain.Exceptions;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Tests;

public class CarbProvenanceTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedClock();

    [Fact]
    public async Task Food_ManualEntryAndEdit_SetSourceWithoutRewritingHistoricalSnapshots()
    {
        await using var db = Db();
        var foods = new FoodService(db, Clock, new User(Owner));
        var food = await foods.CreateFoodAsync(FoodRequest(20m), CancellationToken.None);
        Assert.Equal(CarbReferenceSource.Manual, food.Provenance?.ReferenceSource);
        var meal = await Meals(db).CreateMealAsync(Request(new(food.Id, 100m)), CancellationToken.None);
        await foods.UpdateFoodAsync(food.Id, FoodRequest(50m), CancellationToken.None);
        var historical = await Meals(db).GetMealAsync(meal.Id, CancellationToken.None);
        Assert.Equal(20m, Assert.Single(historical.Items).CalculatedCarbs);
        Assert.Equal(20m, historical.Items[0].CarbsPer100gSnapshot);
        Assert.Equal(CarbReferenceSource.Manual, historical.Items[0].Provenance?.ReferenceSource);
        Assert.Null(historical.CarbsVerifiedAt);
    }

    [Theory]
    [InlineData(CarbReferenceSource.Manual)]
    [InlineData(CarbReferenceSource.AiEstimate)]
    [InlineData(CarbReferenceSource.Unknown)]
    public async Task Calculation_KeepsReferenceSourceSeparateFromDeterministicTransformation(CarbReferenceSource source)
    {
        await using var db = Db();
        var food = Food(source);
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var item = Assert.Single(await Calculation(db).CalculateItemsAsync([new(food.Id, 150m)], null, null, CancellationToken.None));
        Assert.Equal(30m, item.CalculatedCarbs);
        Assert.Equal(source, item.Provenance?.ReferenceSource);
        Assert.Equal(CarbTransformation.Calculated, item.Provenance?.Transformation);
        Assert.Null(item.Provenance?.SourceMealVerifiedAt);
    }

    [Fact]
    public async Task SavingAndConfirmingInsulin_DoNotVerifyCarbohydrates()
    {
        await using var db = Db();
        var food = Food();
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var service = Meals(db);
        var meal = await service.CreateMealAsync(Request(new(food.Id, 100m)), CancellationToken.None);
        Assert.Null(meal.CarbsVerifiedAt);
        meal = await service.ConfirmMealBolusAsync(meal.Id, new(0m), CancellationToken.None);
        Assert.Null(meal.CarbsVerifiedAt);
        Assert.Equal(0m, meal.ConfirmedBolus);
        meal = await service.ClearConfirmedBolusAsync(meal.Id, CancellationToken.None);
        meal = await service.VerifyCarbsAsync(meal.Id, new(meal.CarbRevision), CancellationToken.None);
        Assert.Equal(Now, meal.CarbsVerifiedAt);
        Assert.Null(meal.ConfirmedBolus);
        Assert.Equal(1, meal.CarbRevision);
        meal = await service.ConfirmMealBolusAsync(meal.Id, new(2m), CancellationToken.None);
        Assert.Equal(Now, meal.CarbsVerifiedAt);
        meal = await service.ClearConfirmedBolusAsync(meal.Id, CancellationToken.None);
        Assert.Equal(Now, meal.CarbsVerifiedAt);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("override")]
    [InlineData("add")]
    [InlineData("remove")]
    public async Task RelevantSnapshotChange_InvalidatesVerificationWithoutChangingReferenceOrigin(string operation)
    {
        await using var db = Db();
        var food = Food();
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var service = Meals(db);
        var meal = await service.CreateMealAsync(Request(new(food.Id, 100m)) with { Items = [new(food.Id, 100m), new(food.Id, 50m)] }, CancellationToken.None);
        meal = await service.VerifyCarbsAsync(meal.Id, new(0), CancellationToken.None);
        var item = meal.Items[0];
        meal = operation switch
        {
            "quantity" => await service.UpdateMealItemAsync(meal.Id, item.Id, new(150m), CancellationToken.None),
            "override" => await service.UpdateMealItemAsync(meal.Id, item.Id, new(item.Quantity, CarbOverride: 0m), CancellationToken.None),
            "add" => await service.AddMealItemsAsync(meal.Id, new([new(food.Id, 100m)]), CancellationToken.None),
            _ => await service.RemoveMealItemAsync(meal.Id, item.Id, CancellationToken.None)
        };
        Assert.Null(meal.CarbsVerifiedAt);
        Assert.Equal(2, meal.CarbRevision);
        Assert.All(meal.Items, row => Assert.Equal(CarbReferenceSource.Manual, row.Provenance?.ReferenceSource));
    }

    [Fact]
    public async Task OverrideAndAdjustment_KeepOriginalCalculatedValueAndDoNotVerify()
    {
        await using var db = Db();
        var food = Food(CarbReferenceSource.AiEstimate);
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var meal = await Meals(db).CreateMealAsync(Request(new(food.Id, 100m, CarbOverride: 7m)) with { CarbAdjustment = -2m }, CancellationToken.None);
        var item = Assert.Single(meal.Items);
        Assert.Equal(20m, item.CalculatedCarbs);
        Assert.Equal(7m, item.CarbOverride);
        Assert.Equal(7m, item.EffectiveCarbs);
        Assert.Equal(-2m, meal.CarbAdjustment);
        Assert.Equal(5m, meal.TotalCarbs);
        Assert.Equal(CarbReferenceSource.AiEstimate, item.Provenance?.ReferenceSource);
        Assert.Equal(CarbReferenceSource.Manual, meal.Provenance?.ReferenceSource);
        Assert.Null(meal.CarbsVerifiedAt);
    }

    [Fact]
    public async Task HistoricalReuse_LoadsOwnedSnapshotAndRetainsOriginButDoesNotVerifyNewMeal()
    {
        await using var db = Db();
        var food = Food(CarbReferenceSource.AiEstimate);
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var service = Meals(db);
        var original = await service.CreateMealAsync(Request(new(food.Id, 100m, CarbOverride: 7m)) with { CarbAdjustment = -2m }, CancellationToken.None);
        original = await service.VerifyCarbsAsync(original.Id, new(0), CancellationToken.None);
        await new FoodService(db, Clock, new User(Owner)).UpdateFoodAsync(food.Id, FoodRequest(50m), CancellationToken.None);
        var input = new MealItemInputDto(Guid.NewGuid(), 150m, MeasurementType: FoodMeasurementType.Grams,
            FoodNameSnapshot: "Forged display", CarbsPer100gSnapshot: 999m, SourceMealItemId: original.Items[0].Id);
        var repeated = await service.CreateMealAsync(Request(input) with { CarbAdjustment = original.CarbAdjustment }, CancellationToken.None);
        var item = Assert.Single(repeated.Items);
        Assert.Equal("Test food", item.FoodNameSnapshot);
        Assert.Equal(20m, item.CarbsPer100gSnapshot);
        Assert.Equal(30m, item.CalculatedCarbs);
        Assert.Equal(7m, item.CarbOverride);
        Assert.Equal(5m, repeated.TotalCarbs);
        Assert.Equal(CarbReferenceSource.AiEstimate, item.Provenance?.ReferenceSource);
        Assert.True(item.Provenance?.Transformation.HasFlag(CarbTransformation.HistoricalReuse));
        Assert.Equal(Now, item.Provenance?.SourceMealVerifiedAt);
        Assert.Null(repeated.CarbsVerifiedAt);
        Assert.Equal(20m, (await service.GetMealAsync(original.Id, CancellationToken.None)).Items[0].CalculatedCarbs);
        var cleared = Assert.Single(await Calculation(db).CalculateItemsAsync([input with { ClearCarbOverride = true }], null, null, CancellationToken.None));
        Assert.Null(cleared.CarbOverride);
        Assert.Equal(30m, cleared.EffectiveCarbs);
    }

    [Fact]
    public async Task Verification_IsOwnedAndRejectsStaleSnapshotRevision()
    {
        await using var db = Db();
        var food = Food();
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var service = Meals(db);
        var meal = await service.CreateMealAsync(Request(new(food.Id, 100m)), CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => Meals(db, Other).VerifyCarbsAsync(meal.Id, new(0), CancellationToken.None));
        await service.UpdateMealItemAsync(meal.Id, meal.Items[0].Id, new(150m), CancellationToken.None);
        await Assert.ThrowsAsync<CarbVerificationConflictException>(() => service.VerifyCarbsAsync(meal.Id, new(0), CancellationToken.None));
        Assert.Null((await service.GetMealAsync(meal.Id, CancellationToken.None)).CarbsVerifiedAt);
        await Assert.ThrowsAsync<ValidationException>(() => Calculation(db, Other).CalculateItemsAsync(
            [new(food.Id, 100m, SourceMealItemId: meal.Items[0].Id)], null, null, CancellationToken.None));
    }

    [Fact]
    public async Task LegacyAndUntrustedSnapshots_RemainUnknownAndUnverified()
    {
        await using var db = Db();
        var item = Assert.Single(await Calculation(db).CalculateItemsAsync(
            [new(Guid.NewGuid(), 100m, MeasurementType: FoodMeasurementType.Grams, FoodNameSnapshot: "Legacy food", CarbsPer100gSnapshot: 20m)], null, null, CancellationToken.None));
        Assert.Equal(CarbReferenceSource.Unknown, item.Provenance?.ReferenceSource);
        var food = Food(CarbReferenceSource.Unknown);
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var legacy = await Meals(db).CreateMealAsync(Request(new(food.Id, 100m)), CancellationToken.None);
        Assert.Equal(CarbReferenceSource.Unknown, legacy.Provenance?.ReferenceSource);
        Assert.Null(legacy.CarbsVerifiedAt);
        await new FoodService(db, Clock, new User(Owner)).UpdateFoodAsync(food.Id, FoodRequest(20m) with { IsFavorite = true }, CancellationToken.None);
        Assert.Equal(CarbReferenceSource.Unknown, (await db.FoodItems.SingleAsync(CancellationToken.None)).CarbReferenceSource);
    }

    [Fact]
    public async Task OrderReuse_PreservesWholeOrderSourceAndNeverInfersVerificationFromInsulin()
    {
        await using var db = Db();
        var food = Food();
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var meal = await Meals(db).CreateMealAsync(Request(new(food.Id, 100m)) with { ConfirmedBolus = 2m }, CancellationToken.None);
        var orders = new DeliveryMealService(db, Clock, new User(Owner));
        var order = await orders.CreateFromMealAsync(meal.Id, new("Cafe", "Saved dish", "One order", ResultRating.Unknown, "", false), CancellationToken.None);
        Assert.Null(order.Provenance?.SourceMealVerifiedAt);
        var repeated = await Meals(db).CreateMealAsync(Request(new(food.Id, 100m)) with
        { Items = [], DirectCarbs = order.Carbs, SourceDeliveryMealId = order.Id }, CancellationToken.None);
        Assert.Equal(CarbReferenceSource.Manual, repeated.Items[0].Provenance?.ReferenceSource);
        Assert.True(repeated.Items[0].Provenance?.Transformation.HasFlag(CarbTransformation.HistoricalReuse));
        Assert.Null(repeated.CarbsVerifiedAt);
        await Assert.ThrowsAsync<ValidationException>(() => Calculation(db).CalculateItemsAsync(
            [new(Guid.Empty, 2m, SourceDeliveryMealId: order.Id)], null, null, CancellationToken.None));
    }

    [Fact]
    public async Task AssistantDraft_MetadataSurvivesSerializationAndPreparationRecomputesTrustedOrigin()
    {
        await using var db = Db();
        var food = Food();
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var builder = new AssistantDraftBuilder(db, new User(Owner), Calculation(db));
        var draft = Assert.IsType<AssistantDraft>(await builder.BuildAsync([new("Test food", 100m, FoodMeasurementType.Grams)], CancellationToken.None));
        var restored = Assert.IsType<AssistantDraft>(JsonSerializer.Deserialize<AssistantDraft>(JsonSerializer.Serialize(draft)));
        Assert.Equal(CarbReferenceSource.Manual, restored.Items[0].Provenance?.ReferenceSource);
        var forged = restored.Items[0] with { Provenance = new(CarbReferenceSource.AiEstimate, CarbTransformation.HistoricalReuse, Now.AddYears(1)) };
        var prepared = Assert.Single(await builder.PrepareAsync([forged], CancellationToken.None));
        Assert.Equal(CarbReferenceSource.Manual, prepared.Provenance?.ReferenceSource);
        Assert.Null(prepared.Provenance?.SourceMealVerifiedAt);
        Assert.Equal(20m, prepared.CalculatedCarbs);
        var changed = Assert.IsType<AssistantDraft>(await builder.UpdateAsync(new AssistantSearch([new("Test food", 150m, FoodMeasurementType.Grams)], "plan", Operation: "set", Target: "Test food"), restored, CancellationToken.None));
        Assert.Equal(CarbReferenceSource.Manual, changed.Items[0].Provenance?.ReferenceSource);
        Assert.Equal(30m, changed.Items[0].CalculatedCarbs);
    }

    [Theory]
    [InlineData(FoodMeasurementType.Grams, 150, 30)]
    [InlineData(FoodMeasurementType.Portion, 0.5, 10)]
    [InlineData(FoodMeasurementType.Piece, 2, 40)]
    public async Task Provenance_DoesNotChangeGramPortionPieceMath(FoodMeasurementType unit, decimal quantity, decimal expected)
    {
        await using var db = Db();
        var food = Food();
        food.MeasurementType = unit;
        food.CarbsPerUnit = 20m;
        db.FoodItems.Add(food);
        await db.SaveChangesAsync(CancellationToken.None);
        var item = Assert.Single(await Calculation(db).CalculateItemsAsync([new(food.Id, quantity)], null, null, CancellationToken.None));
        Assert.Equal(expected, item.EffectiveCarbs);
        Assert.Equal(CarbTransformation.Calculated, item.Provenance?.Transformation);
    }

    private static AppDbContext Db()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.DiabetesSettings.Add(DiabetesSettings.CreateDefaults(Guid.NewGuid(), Owner, Now));
        db.SaveChanges();
        return db;
    }
    private static FoodItem Food(CarbReferenceSource source = CarbReferenceSource.Manual) => new()
    { Id = Guid.NewGuid(), UserId = Owner, Name = "Test food", MeasurementType = FoodMeasurementType.Grams, CarbsPer100g = 20m, CarbReferenceSource = source };
    private static UpsertFoodItemRequest FoodRequest(decimal carbs) => new("Test food", FoodMeasurementType.Grams, carbs, null, 0m, 0m, 0m, false);
    private static CreateMealRequest Request(MealItemInputDto item) => new(MealType.Lunch, null, 6.5m, null, null, [item]);
    private static MealCalculationService Calculation(AppDbContext db, Guid? user = null) => new(db, new User(user ?? Owner));
    private static MealService Meals(AppDbContext db, Guid? user = null) => new(db, Clock, Calculation(db, user), new User(user ?? Owner));
    private sealed class User(Guid id) : ICurrentUser { public Guid UserId => id; }
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
