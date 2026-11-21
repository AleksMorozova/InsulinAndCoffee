using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class SettingsService(IAppDbContext db, TimeProvider timeProvider, ICurrentUser? currentUser = null)
{
    private Guid UserId => currentUser?.UserId ?? DefaultUser.Id;
    public async Task<DiabetesSettingsDto> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateUserSettingsAsync(db, UserId, timeProvider, cancellationToken);
        return new(settings.Id, settings.TargetGlucose, settings.CarbRatio, settings.CorrectionFactor, settings.InsulinDurationHours, settings.UpdatedAt);
    }

    public async Task<DiabetesSettingsDto> UpdateSettingsAsync(UpdateDiabetesSettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.TargetGlucose <= 0 || request.CarbRatio <= 0 || request.CorrectionFactor <= 0 || request.InsulinDurationHours <= 0)
        {
            throw new ValidationException("All settings must be greater than zero.");
        }

        var now = timeProvider.GetUtcNow();
        var settings = await GetOrCreateUserSettingsAsync(db, UserId, timeProvider, cancellationToken, track: true);
        settings.TargetGlucose = request.TargetGlucose;
        settings.CarbRatio = request.CarbRatio;
        settings.CorrectionFactor = request.CorrectionFactor;
        settings.InsulinDurationHours = request.InsulinDurationHours;
        settings.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return new(settings.Id, settings.TargetGlucose, settings.CarbRatio, settings.CorrectionFactor, settings.InsulinDurationHours, settings.UpdatedAt);
    }

    internal static async Task<DiabetesSettings> GetOrCreateUserSettingsAsync(
        IAppDbContext db, Guid userId, TimeProvider timeProvider, CancellationToken cancellationToken, bool track = false)
    {
        var query = track ? db.DiabetesSettings.AsQueryable() : db.DiabetesSettings.AsNoTracking();
        var existing = await query.SingleOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (existing is not null) return existing;
        if (!await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
            throw new NotFoundException("User", userId);

        var settings = DiabetesSettings.CreateDefaults(Guid.NewGuid(), userId, timeProvider.GetUtcNow());
        db.DiabetesSettings.Add(settings);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return settings;
        }
        catch (SettingsAlreadyExistException)
        {
            // Another request won the unique UserId insert. Never replace its values.
            // Removing an Added entity detaches only that failed insert, preserving other tracked work.
            db.DiabetesSettings.Remove(settings);
            return await query.SingleAsync(s => s.UserId == userId, cancellationToken);
        }
    }
}
