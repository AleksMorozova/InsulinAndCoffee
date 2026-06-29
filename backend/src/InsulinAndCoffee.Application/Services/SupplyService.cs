using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class SupplyService(IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<SupplyItemDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.SupplyItems
            .AsNoTracking()
            .Where(item => item.UserId == DefaultUser.Id)
            .OrderBy(item => item.Name)
            .Select(item => ToDto(item))
            .ToListAsync(cancellationToken);

    public async Task<SupplyItemDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.SupplyItems
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Supply item was not found.");

        return ToDto(item);
    }

    public async Task<SupplyItemDto> CreateAsync(CreateSupplyItemRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Unit, request.CurrentQuantity, request.DailyUsage, request.LowStockThresholdDays);
        var now = timeProvider.GetUtcNow();
        var item = new SupplyItem
        {
            Id = Guid.NewGuid(),
            UserId = DefaultUser.Id,
            Name = request.Name.Trim(),
            CurrentQuantity = request.CurrentQuantity,
            Unit = request.Unit.Trim(),
            DailyUsage = request.DailyUsage,
            LowStockThresholdDays = request.LowStockThresholdDays,
            LastUpdatedAt = now,
            CreatedAt = now
        };

        db.SupplyItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task<SupplyItemDto> UpdateAsync(Guid id, UpdateSupplyItemRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Unit, request.CurrentQuantity, request.DailyUsage, request.LowStockThresholdDays);
        var item = await db.SupplyItems
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Supply item was not found.");

        var now = timeProvider.GetUtcNow();
        item.Name = request.Name.Trim();
        item.CurrentQuantity = request.CurrentQuantity;
        item.Unit = request.Unit.Trim();
        item.DailyUsage = request.DailyUsage;
        item.LowStockThresholdDays = request.LowStockThresholdDays;
        item.LastUpdatedAt = now;
        item.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.SupplyItems
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Supply item was not found.");

        db.SupplyItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SupplyCheckResultDto>> GetSupplyCheckAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var items = await db.SupplyItems
            .AsNoTracking()
            .Where(item => item.UserId == DefaultUser.Id)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return items.Select(item => Calculate(item, today)).ToList();
    }

    public static SupplyCheckResultDto Calculate(SupplyItem item, DateOnly today)
    {
        if (item.DailyUsage <= 0)
        {
            return ToCheckDto(item, null, null, "Unknown");
        }

        var rawDaysLeft = item.CurrentQuantity / item.DailyUsage;
        var daysLeft = Math.Round(rawDaysLeft, 1, MidpointRounding.AwayFromZero);
        var maxDaysToAdd = DateOnly.MaxValue.DayNumber - today.DayNumber;
        var roundedDaysToAdd = decimal.Ceiling(rawDaysLeft);
        var daysToAdd = roundedDaysToAdd >= maxDaysToAdd
            ? maxDaysToAdd
            : decimal.ToInt32(roundedDaysToAdd);
        var runOutDate = today.AddDays(daysToAdd);
        var status = daysLeft <= 3
            ? "Critical"
            : daysLeft <= item.LowStockThresholdDays
                ? "Low"
                : "Ok";

        return ToCheckDto(item, daysLeft, runOutDate, status);
    }

    private static void Validate(string name, string unit, decimal currentQuantity, decimal dailyUsage, int lowStockThresholdDays)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Supply name is required.");
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ValidationException("Supply unit is required.");
        }

        if (currentQuantity < 0)
        {
            throw new ValidationException("Current quantity cannot be negative.");
        }

        if (dailyUsage < 0)
        {
            throw new ValidationException("Daily usage cannot be negative.");
        }

        if (lowStockThresholdDays < 0)
        {
            throw new ValidationException("Low stock threshold cannot be negative.");
        }
    }

    private static SupplyItemDto ToDto(SupplyItem item) =>
        new(item.Id, item.Name, item.CurrentQuantity, item.Unit, item.DailyUsage, item.LowStockThresholdDays,
            item.LastUpdatedAt, item.CreatedAt, item.UpdatedAt);

    private static SupplyCheckResultDto ToCheckDto(
        SupplyItem item,
        decimal? daysLeft,
        DateOnly? estimatedRunOutDate,
        string status) =>
        new(item.Id, item.Name, item.CurrentQuantity, item.Unit, item.DailyUsage, item.LowStockThresholdDays,
            daysLeft, estimatedRunOutDate, status);
}
