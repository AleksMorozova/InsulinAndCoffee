using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Services;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await base.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException
            { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_DiabetesSettings_UserId" })
        {
            throw new SettingsAlreadyExistException(exception);
        }
    }

    public DbSet<AssistantConversation> AssistantConversations => Set<AssistantConversation>();
    public DbSet<AssistantMessage> AssistantMessages => Set<AssistantMessage>();
    public DbSet<AssistantRequestReceipt> AssistantRequestReceipts => Set<AssistantRequestReceipt>();
    public void DiscardChanges() => ChangeTracker.Clear();
    public DbSet<User> Users => Set<User>();
    public DbSet<DiabetesSettings> DiabetesSettings => Set<DiabetesSettings>();
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<MealItem> MealItems => Set<MealItem>();
    public DbSet<GlucoseReading> GlucoseReadings => Set<GlucoseReading>();
    public DbSet<DeliveryMeal> DeliveryMeals => Set<DeliveryMeal>();
    public DbSet<DeliveryMealItem> DeliveryMealItems => Set<DeliveryMealItem>();
    public DbSet<SupplyItem> SupplyItems => Set<SupplyItem>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDbFunction(typeof(AssistantFoodMatching).GetMethod(nameof(AssistantFoodMatching.Normalize), [typeof(string)])
            ?? throw new InvalidOperationException("Food name normalization is unavailable."))
            .HasTranslation(arguments =>
            {
                SqlExpression Text(string value) => new SqlConstantExpression(value, arguments[0].TypeMapping);
                SqlExpression Call(string name, params SqlExpression[] values) =>
                    new SqlFunctionExpression(name, values, nullable: true,
                        argumentsPropagateNullability: values.Select(_ => true), typeof(string), arguments[0].TypeMapping);
                var lower = Call("lower", Call("normalize", arguments[0], new SqlFragmentExpression("NFKC")));
                var fixture = Call("replace", lower, Text("[meal assistant local fixture v1] "), Text(""));
                var apostrophe = Call("replace", fixture, Text("’"), Text("'"));
                return Call("btrim", Call("regexp_replace", apostrophe, Text(AssistantFoodMatching.Separators), Text(" "), Text("g")));
            });

        modelBuilder.HasPostgresEnum<MealType>();
        modelBuilder.HasPostgresEnum<ReadingType>();
        modelBuilder.HasPostgresEnum<ResultRating>();
        modelBuilder.HasPostgresEnum<FoodMeasurementType>();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        DatabaseSeeder.Seed(modelBuilder);
    }
}
