using System.Data;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Infrastructure.Persistence.Seeding;

public static class LocalTestSettingsRepair
{
    public static async Task RunAsync(AppDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var connection = LocalDevelopmentDatabase.Verify(db);
        var userId = MealAssistantDevelopmentSeed.UserId;
        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            if (!await db.Users.AnyAsync(user => user.Id == userId, cancellationToken))
                throw new InvalidOperationException($"Local test user {userId} was not found.");

            var exists = await db.DiabetesSettings.AnyAsync(settings => settings.UserId == userId, cancellationToken);
            if (!exists)
            {
                db.DiabetesSettings.Add(DiabetesSettings.CreateDefaults(Guid.NewGuid(), userId, clock.GetUtcNow()));
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            Console.WriteLine($"{connection.Host}:{connection.Port}/{connection.Database}: {(exists ? "Preserved" : "Created")} settings for {userId}.");
        }

        var currentUser = new TestUser(userId);
        var settings = await new SettingsService(db, clock, currentUser).GetSettingsAsync(cancellationToken);
        Console.WriteLine($"Settings {settings.Id}: target {settings.TargetGlucose}, ratio {settings.CarbRatio}, correction {settings.CorrectionFactor}, duration {settings.InsulinDurationHours}.");
        var food = await db.FoodItems.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (food is null) return;

        var quantity = food.MeasurementType == FoodMeasurementType.Grams ? 100m : 1m;
        var calculation = new MealCalculationService(db, currentUser);
        var items = await new AssistantDraftBuilder(db, currentUser, calculation).PrepareAsync(
            await calculation.CalculateItemsAsync([new MealItemInputDto(food.Id, quantity)], null, null, cancellationToken),
            cancellationToken);
        var draft = items.Select(item => new MealItemInputDto(item.FoodItemId, item.Quantity,
            MeasurementType: item.MeasurementType, FoodNameSnapshot: item.FoodName,
            CarbsPer100gSnapshot: item.CarbsPer100g, CarbsPerUnitSnapshot: item.CarbsPerUnit,
            CarbOverride: item.CarbOverride)).ToArray();
        await calculation.CalculateMealAsync(new CalculateMealRequest(MealType.Lunch, settings.TargetGlucose, draft), cancellationToken);
        Console.WriteLine("Calculator calculation from a saved-food draft succeeded with synthetic glucose.");
    }

    private sealed record TestUser(Guid UserId) : ICurrentUser;
}
