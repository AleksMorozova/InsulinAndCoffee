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
        var settings = await db.DiabetesSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == UserId, cancellationToken)
            ?? throw new NotFoundException("Diabetes settings", UserId);
        return new(settings.Id, settings.TargetGlucose, settings.CarbRatio, settings.CorrectionFactor, settings.InsulinDurationHours, settings.UpdatedAt);
    }

    public async Task<DiabetesSettingsDto> UpdateSettingsAsync(UpdateDiabetesSettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.TargetGlucose <= 0 || request.CarbRatio <= 0 || request.CorrectionFactor <= 0 || request.InsulinDurationHours <= 0)
        {
            throw new ValidationException("All settings must be greater than zero.");
        }

        var now = timeProvider.GetUtcNow();
        var settings = await db.DiabetesSettings.FirstOrDefaultAsync(s => s.UserId == UserId, cancellationToken);
        if (settings is null)
        {
            settings = new DiabetesSettings { Id = Guid.NewGuid(), UserId = UserId };
            db.DiabetesSettings.Add(settings);
        }
        settings.TargetGlucose = request.TargetGlucose;
        settings.CarbRatio = request.CarbRatio;
        settings.CorrectionFactor = request.CorrectionFactor;
        settings.InsulinDurationHours = request.InsulinDurationHours;
        settings.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return new(settings.Id, settings.TargetGlucose, settings.CarbRatio, settings.CorrectionFactor, settings.InsulinDurationHours, settings.UpdatedAt);
    }
}
