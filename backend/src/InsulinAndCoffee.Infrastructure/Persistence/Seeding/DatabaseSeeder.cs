using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Infrastructure.Persistence.Seeding;

public static class DatabaseSeeder
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var settingsId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        modelBuilder.Entity<User>().HasData(new User
        {
            Id = DefaultUser.Id,
            Name = "Aleksandra",
            Email = "aleksandra@example.com",
            CreatedAt = createdAt
        });

        modelBuilder.Entity<DiabetesSettings>().HasData(new DiabetesSettings
        {
            Id = settingsId,
            UserId = DefaultUser.Id,
            TargetGlucose = 6.5m,
            CarbRatio = 10m,
            CorrectionFactor = 3m,
            InsulinDurationHours = 4m,
            UpdatedAt = createdAt
        });

        modelBuilder.Entity<FoodItem>().HasData(
            Food("33333333-3333-3333-3333-333333333301", "Philadelphia Roll", 25m, true, createdAt),
            Food("33333333-3333-3333-3333-333333333302", "Sushi Rice", 28m, false, createdAt),
            Food("33333333-3333-3333-3333-333333333303", "Bread", 45m, true, createdAt),
            Food("33333333-3333-3333-3333-333333333304", "Butter", 1m, false, createdAt),
            Food("33333333-3333-3333-3333-333333333305", "Cottage Cheese Casserole", 27m, true, createdAt),
            Food("33333333-3333-3333-3333-333333333306", "Borscht", 6m, true, createdAt),
            Food("33333333-3333-3333-3333-333333333307", "Chicken Cutlet", 8m, false, createdAt),
            Food("33333333-3333-3333-3333-333333333308", "Latte", 5m, true, createdAt),
            Food("33333333-3333-3333-3333-333333333309", "Chocolate", 55m, true, createdAt));

        modelBuilder.Entity<DeliveryMeal>().HasData(
            DeliveryMeal("44444444-4444-4444-4444-444444444401", "Sushi Master", "Philadelphia Set", "standard set", 95m, 7m, ResultRating.Good, "sushi, delivery, dinner", "Reliable repeat order.", true, createdAt),
            DeliveryMeal("44444444-4444-4444-4444-444444444402", "Local Cafe", "Cottage Cheese Casserole", "one slice", 67m, 6m, ResultRating.Good, "cafe, dessert, breakfast", "Good with coffee.", true, createdAt),
            DeliveryMeal("44444444-4444-4444-4444-444444444403", "Delivery", "Shawarma", "standard", 80m, 8m, ResultRating.Unknown, "delivery, lunch", "Check glucose response next time.", false, createdAt));

    }

    private static FoodItem Food(string id, string name, decimal carbs, bool favorite, DateTimeOffset createdAt) => new()
    {
        Id = Guid.Parse(id),
        UserId = DefaultUser.Id,
        Name = name,
        CarbsPer100g = carbs,
        ProteinPer100g = 0,
        FatPer100g = 0,
        CaloriesPer100g = 0,
        IsFavorite = favorite,
        CreatedAt = createdAt
    };

    private static DeliveryMeal DeliveryMeal(string id, string placeName, string dishName, string portionDescription, decimal carbs, decimal insulin, ResultRating rating, string tags, string notes, bool favorite, DateTimeOffset createdAt) => new()
    {
        Id = Guid.Parse(id),
        UserId = DefaultUser.Id,
        PlaceName = placeName,
        DishName = dishName,
        PortionDescription = portionDescription,
        Carbs = carbs,
        UsualInsulinUnits = insulin,
        LastPreMealGlucose = null,
        ResultRating = rating,
        Tags = tags,
        Notes = notes,
        IsFavorite = favorite,
        UsageCount = 0,
        LastUsedAt = null,
        CreatedAt = createdAt
    };
}