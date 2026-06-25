using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<DiabetesSettings> DiabetesSettings => Set<DiabetesSettings>();
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<MealItem> MealItems => Set<MealItem>();
    public DbSet<GlucoseReading> GlucoseReadings => Set<GlucoseReading>();
    public DbSet<DeliveryMeal> DeliveryMeals => Set<DeliveryMeal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<MealType>();
        modelBuilder.HasPostgresEnum<ReadingType>();
        modelBuilder.HasPostgresEnum<ResultRating>();

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Name).HasMaxLength(120).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<DiabetesSettings>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.UserId).IsUnique();
            entity.Property(s => s.TargetGlucose).HasPrecision(6, 2);
            entity.Property(s => s.CarbRatio).HasPrecision(6, 2);
            entity.Property(s => s.CorrectionFactor).HasPrecision(6, 2);
            entity.Property(s => s.InsulinDurationHours).HasPrecision(4, 1);
        });

        modelBuilder.Entity<FoodItem>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Ignore(f => f.MealItems);
            entity.HasIndex(f => new { f.UserId, f.Name });
            entity.Property(f => f.Name).HasMaxLength(160).IsRequired();
            entity.Property(f => f.CarbsPer100g).HasPrecision(7, 2);
            entity.Property(f => f.ProteinPer100g).HasPrecision(7, 2);
            entity.Property(f => f.FatPer100g).HasPrecision(7, 2);
            entity.Property(f => f.CaloriesPer100g).HasPrecision(8, 2);
        });

        modelBuilder.Entity<Meal>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.UserId, m.MealTime });
            entity.Property(m => m.PreMealGlucose).HasPrecision(6, 2);
            entity.Property(m => m.TotalCarbs).HasPrecision(8, 2);
            entity.Property(m => m.SuggestedBolus).HasPrecision(6, 2);
            entity.Property(m => m.ConfirmedBolus).HasPrecision(6, 2);
            entity.Property(m => m.Notes).HasMaxLength(1000);
        });

        modelBuilder.Entity<MealItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Ignore(i => i.FoodItem);
            entity.Property(i => i.FoodNameSnapshot).HasMaxLength(160).IsRequired();
            entity.Property(i => i.WeightGrams).HasPrecision(8, 2);
            entity.Property(i => i.CarbsPer100gSnapshot).HasPrecision(7, 2);
            entity.Property(i => i.CalculatedCarbs).HasPrecision(8, 2);
            entity.HasOne(i => i.Meal).WithMany(m => m.Items).HasForeignKey(i => i.MealId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GlucoseReading>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.UserId, r.ReadingTime });
            entity.Property(r => r.Value).HasPrecision(6, 2);
            entity.Property(r => r.Notes).HasMaxLength(1000);
            entity.HasOne(r => r.Meal).WithMany(m => m.GlucoseReadings).HasForeignKey(r => r.MealId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DeliveryMeal>(entity =>
        {
            entity.HasKey(k => k.Id);
            entity.HasIndex(k => new { k.UserId, k.PlaceName, k.DishName });
            entity.HasIndex(k => new { k.UserId, k.IsFavorite });
            entity.HasIndex(k => new { k.UserId, k.UsageCount });
            entity.Property(k => k.PlaceName).HasMaxLength(180).IsRequired();
            entity.Property(k => k.DishName).HasMaxLength(180).IsRequired();
            entity.Property(k => k.PortionDescription).HasMaxLength(220).IsRequired();
            entity.Property(k => k.Carbs).HasPrecision(8, 2);
            entity.Property(k => k.UsualInsulinUnits).HasPrecision(6, 2);
            entity.Property(k => k.LastPreMealGlucose).HasPrecision(6, 2);
            entity.Property(k => k.Tags).HasMaxLength(400);
            entity.Property(k => k.Notes).HasMaxLength(1000);
        });

        Seed(modelBuilder);
    }

    private static void Seed(ModelBuilder modelBuilder)
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
