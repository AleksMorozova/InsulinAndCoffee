using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Application.Services;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InsulinAndCoffee.Application.Tests;

public class SettingsServiceTests
{
    [Fact]
    public async Task GetSettingsAsync_MissingSettings_InitializesOnceAndPreservesCustomizationAndIsolation()
    {
        await using var db = Db();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        db.Users.AddRange(User(owner), User(other));
        await db.SaveChangesAsync(CancellationToken.None);
        var service = new SettingsService(db, TimeProvider.System, new CurrentUser(owner));
        var first = await service.GetSettingsAsync(CancellationToken.None);
        var repeated = await service.GetSettingsAsync(CancellationToken.None);
        Assert.Equal(first.Id, repeated.Id);
        Assert.NotEqual(owner, first.Id);
        Assert.Equal(6.5m, first.TargetGlucose);
        Assert.Equal(10m, first.CarbRatio);
        Assert.Equal(3m, first.CorrectionFactor);
        Assert.Equal(4m, first.InsulinDurationHours);
        var custom = new UpdateDiabetesSettingsRequest(7m, 12m, 4m, 5m);
        await service.UpdateSettingsAsync(custom, CancellationToken.None);
        var kept = await service.GetSettingsAsync(CancellationToken.None);
        Assert.Equal(7m, kept.TargetGlucose);
        Assert.Equal(12m, kept.CarbRatio);
        Assert.Equal(4m, kept.CorrectionFactor);
        Assert.Equal(5m, kept.InsulinDurationHours);
        var isolated = await new SettingsService(db, TimeProvider.System, new CurrentUser(other)).GetSettingsAsync(CancellationToken.None);
        Assert.NotEqual(kept.Id, isolated.Id);
        Assert.Equal(6.5m, isolated.TargetGlucose);
        Assert.Equal(2, await db.DiabetesSettings.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CalculateMealAsync_LegacyUserWithoutSettings_RepairsBeforeCalculating()
    {
        await using var db = Db();
        var owner = Guid.NewGuid();
        db.Users.Add(User(owner));
        await db.SaveChangesAsync(CancellationToken.None);
        var result = await new MealCalculationService(db, new CurrentUser(owner)).CalculateMealAsync(
            new(Domain.Enums.MealType.Lunch, 5.5m, [], DirectCarbs: 20m), CancellationToken.None);
        Assert.Equal(20m, result.TotalCarbs);
        Assert.Equal(2m, result.MealBolus);
        Assert.Equal(owner, (await db.DiabetesSettings.SingleAsync(CancellationToken.None)).UserId);
    }

    [Fact]
    public async Task GetSettingsAsync_UnexpectedPersistenceFailure_PropagatesWithoutFallbackDefaults()
    {
        var failure = new FailedSave();
        await using var db = Db(failure);
        var owner = Guid.NewGuid();
        db.Users.Add(User(owner));
        await db.SaveChangesAsync(CancellationToken.None);
        failure.Fail = true;
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            new SettingsService(db, TimeProvider.System, new CurrentUser(owner)).GetSettingsAsync(CancellationToken.None));
        Assert.Empty(await db.DiabetesSettings.AsNoTracking().ToListAsync(CancellationToken.None));
    }

    private static AppDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(interceptors).Options);
    private static User User(Guid id) => new() { Id = id, Username = id.ToString(), NormalizedUsername = id.ToString(), PasswordHash = "test" };
    private sealed class CurrentUser(Guid id) : ICurrentUser { public Guid UserId => id; }
    private sealed class FailedSave : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Fail ? throw new DbUpdateException("Simulated database failure") : ValueTask.FromResult(result);
    }
}
