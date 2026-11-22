using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<AiUsageRecord> AiUsageRecords { get; }
    DbSet<AiModelPricing> AiModelPrices { get; }
    DbSet<AssistantConversation> AssistantConversations { get; }
    DbSet<AssistantMessage> AssistantMessages { get; }
    DbSet<AssistantRequestReceipt> AssistantRequestReceipts { get; }
    DbSet<User> Users { get; }
    DbSet<DiabetesSettings> DiabetesSettings { get; }
    DbSet<FoodItem> FoodItems { get; }
    DbSet<Meal> Meals { get; }
    DbSet<MealItem> MealItems { get; }
    DbSet<GlucoseReading> GlucoseReadings { get; }
    DbSet<DeliveryMeal> DeliveryMeals { get; }
    DbSet<DeliveryMealItem> DeliveryMealItems { get; }
    DbSet<SupplyItem> SupplyItems { get; }
    DbSet<LoginAttempt> LoginAttempts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    void DiscardChanges();
}
