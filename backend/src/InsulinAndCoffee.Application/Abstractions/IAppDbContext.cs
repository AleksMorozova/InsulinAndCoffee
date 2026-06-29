using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<DiabetesSettings> DiabetesSettings { get; }
    DbSet<FoodItem> FoodItems { get; }
    DbSet<Meal> Meals { get; }
    DbSet<MealItem> MealItems { get; }
    DbSet<GlucoseReading> GlucoseReadings { get; }
    DbSet<DeliveryMeal> DeliveryMeals { get; }
    DbSet<SupplyItem> SupplyItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
