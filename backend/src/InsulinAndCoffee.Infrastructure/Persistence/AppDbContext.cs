using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Infrastructure.Persistence.Seeding;
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
    public DbSet<SupplyItem> SupplyItems => Set<SupplyItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresEnum<MealType>();
        modelBuilder.HasPostgresEnum<ReadingType>();
        modelBuilder.HasPostgresEnum<ResultRating>();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        DatabaseSeeder.Seed(modelBuilder);
    }
}