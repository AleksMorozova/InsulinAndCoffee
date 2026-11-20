using System.Data;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Infrastructure.Persistence.Seeding;

public static class AskPastMeDevelopmentSeed
{
    public static async Task RunAsync(AppDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        LocalDevelopmentDatabase.Verify(db);
        var userId = MealAssistantDevelopmentSeed.UserId;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await db.Users.AnyAsync(user => user.Id == userId, cancellationToken))
            throw new InvalidOperationException($"Local test user {userId} was not found.");

        const string marker = "[Ask Past Me local fixture v1]";
        var timestamp = clock.GetUtcNow();
        var orders = new[]
        {
            Order("5f91e464-e7d3-43fb-9e2d-000000000201", "Le Grill", "California roll and additional sushi",
                "California sushi roll: 1 portion, 60 g carbs. Additional sushi: 5 pieces × 15 g carbs = 75 g carbs.", 135m),
            Order("5f91e464-e7d3-43fb-9e2d-000000000202", "Huli Huli", "Shawarma",
                "Shawarma: 1 piece, 95 g carbs.", 95m)
        };

        var created = 0;
        foreach (var order in orders)
        {
            var existing = await db.DeliveryMeals.AsNoTracking().SingleOrDefaultAsync(item => item.Id == order.Id, cancellationToken);
            if (existing is null)
            {
                db.DeliveryMeals.Add(order);
                created++;
            }
            else if (existing.UserId != userId || existing.Notes != order.Notes)
            {
                throw new InvalidOperationException($"Fixture ID {order.Id} conflicts with an existing delivery.");
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await transaction.DisposeAsync();

        // Exercise the existing reuse workflow and roll back its usage bookkeeping.
        await using var verification = await db.Database.BeginTransactionAsync(cancellationToken);
        var service = new DeliveryMealService(db, clock, new FixtureUser(userId));
        foreach (var order in orders)
        {
            var sections = await service.GetSectionsAsync(order.PlaceName, cancellationToken);
            var visible = sections.SearchResults.Single(item => item.Id == order.Id);
            var draft = await service.CreateMealDraftFromDeliveryMealAsync(order.Id, cancellationToken);
            if (visible.Carbs != order.Carbs || visible.PortionDescription != order.PortionDescription ||
                draft.Carbs != order.Carbs || draft.UsualInsulinUnits is not null ||
                !draft.Notes.Contains(order.PortionDescription, StringComparison.Ordinal))
                throw new InvalidOperationException($"Delivery verification failed for {order.Id}.");
            Console.WriteLine($"{visible.Id} | {visible.PlaceName} | {visible.PortionDescription} | {draft.Carbs} g draft");
        }
        await verification.RollbackAsync(cancellationToken);
        Console.WriteLine($"Created {created} orders for {userId}. Retrieval and calculator draft verified.");

        DeliveryMeal Order(string id, string place, string dish, string portions, decimal carbs) => new()
        {
            Id = Guid.Parse(id),
            UserId = userId,
            PlaceName = place,
            DishName = dish,
            PortionDescription = portions,
            Carbs = carbs,
            ResultRating = ResultRating.Unknown,
            Notes = $"{marker} CreatedAt is the fixture insertion timestamp. Order date unknown.",
            Tags = "local test fixture",
            CreatedAt = timestamp
        };
    }

    private sealed record FixtureUser(Guid UserId) : ICurrentUser;
}
