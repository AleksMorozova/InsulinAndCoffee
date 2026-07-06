using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class SettingsService(IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<DiabetesSettingsDto> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await db.DiabetesSettings.AsNoTracking().FirstAsync(s => s.UserId == DefaultUser.Id, cancellationToken);
        return new(settings.Id, settings.TargetGlucose, settings.CarbRatio, settings.CorrectionFactor, settings.InsulinDurationHours, settings.UpdatedAt);
    }

    public async Task<DiabetesSettingsDto> UpdateSettingsAsync(UpdateDiabetesSettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.TargetGlucose <= 0 || request.CarbRatio <= 0 || request.CorrectionFactor <= 0 || request.InsulinDurationHours <= 0)
        {
            throw new ValidationException("All settings must be greater than zero.");
        }

        var settings = await db.DiabetesSettings.FirstAsync(s => s.UserId == DefaultUser.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        settings.TargetGlucose = request.TargetGlucose;
        settings.CarbRatio = request.CarbRatio;
        settings.CorrectionFactor = request.CorrectionFactor;
        settings.InsulinDurationHours = request.InsulinDurationHours;
        settings.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return new(settings.Id, settings.TargetGlucose, settings.CarbRatio, settings.CorrectionFactor, settings.InsulinDurationHours, settings.UpdatedAt);
    }
}
