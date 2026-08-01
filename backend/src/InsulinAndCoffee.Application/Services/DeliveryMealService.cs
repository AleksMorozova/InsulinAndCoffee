using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class DeliveryMealService(IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<DeliveryMealSectionsDto> GetSectionsAsync(string? search, CancellationToken cancellationToken)
    {
        var baseQuery = db.DeliveryMeals
            .AsNoTracking()
            .Where(k => k.UserId == DefaultUser.Id);

        var favorites = await baseQuery
            .Where(k => k.IsFavorite)
            .OrderByDescending(k => k.LastUsedAt ?? k.CreatedAt)
            .Take(8)
            .Select(k => ToDto(k))
            .ToListAsync(cancellationToken);

        var mostUsed = await baseQuery
            .Where(k => k.UsageCount > 0)
            .OrderByDescending(k => k.UsageCount)
            .ThenByDescending(k => k.LastUsedAt)
            .Take(8)
            .Select(k => ToDto(k))
            .ToListAsync(cancellationToken);

        var recentlyUsed = await baseQuery
            .Where(k => k.LastUsedAt != null)
            .OrderByDescending(k => k.LastUsedAt)
            .Take(8)
            .Select(k => ToDto(k))
            .ToListAsync(cancellationToken);

        var searchResultsQuery = baseQuery;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            searchResultsQuery = searchResultsQuery.Where(k =>
                k.PlaceName.ToLower().Contains(term) ||
                k.DishName.ToLower().Contains(term) ||
                k.Tags.ToLower().Contains(term));
        }

        var searchResults = await searchResultsQuery
            .OrderByDescending(k => k.IsFavorite)
            .ThenByDescending(k => k.UsageCount)
            .ThenBy(k => k.PlaceName)
            .ThenBy(k => k.DishName)
            .Take(40)
            .Select(k => ToDto(k))
            .ToListAsync(cancellationToken);

        return new DeliveryMealSectionsDto(favorites, mostUsed, recentlyUsed, searchResults);
    }

    public async Task<DeliveryMealDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var deliveryMeal = await db.DeliveryMeals
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id && k.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new NotFoundException("Delivery meal", id);

        return ToDto(deliveryMeal);
    }

    public async Task<DeliveryMealDto> CreateAsync(UpsertDeliveryMealRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var now = timeProvider.GetUtcNow();

        var deliveryMeal = new DeliveryMeal
        {
            Id = Guid.NewGuid(),
            UserId = DefaultUser.Id,
            PlaceName = request.PlaceName.Trim(),
            DishName = request.DishName.Trim(),
            PortionDescription = request.PortionDescription.Trim(),
            Carbs = request.Carbs,
            UsualInsulinUnits = request.UsualInsulinUnits,
            LastPreMealGlucose = request.LastPreMealGlucose,
            ResultRating = request.ResultRating,
            Tags = NormalizeTags(request.Tags),
            Notes = request.Notes,
            IsFavorite = request.IsFavorite,
            UsageCount = 0,
            CreatedAt = now
        };

        db.DeliveryMeals.Add(deliveryMeal);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(deliveryMeal);
    }

    public async Task<DeliveryMealDto> CreateFromMealAsync(Guid mealId, CreateDeliveryMealFromMealRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlaceName) || string.IsNullOrWhiteSpace(request.DishName))
        {
            throw new ValidationException("Place name and dish name are required.");
        }

        var meal = await db.Meals
            .AsNoTracking()
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == mealId && m.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new NotFoundException("Meal", mealId);

        if (meal.ConfirmedBolus is null)
        {
            throw new ValidationException("Confirm the insulin dose before saving this meal as a delivery meal.");
        }

        var notes = meal.Notes;
        if (string.IsNullOrWhiteSpace(notes))
        {
            notes = $"Saved from {meal.MealType} on {meal.MealTime:yyyy-MM-dd}.";
        }

        var now = timeProvider.GetUtcNow();
        var deliveryMeal = new DeliveryMeal
        {
            Id = Guid.NewGuid(),
            UserId = DefaultUser.Id,
            PlaceName = request.PlaceName.Trim(),
            DishName = request.DishName.Trim(),
            PortionDescription = request.PortionDescription.Trim(),
            Carbs = meal.TotalCarbs,
            UsualInsulinUnits = meal.ConfirmedBolus.Value,
            LastPreMealGlucose = meal.PreMealGlucose,
            ResultRating = request.ResultRating,
            Tags = NormalizeTags(request.Tags),
            Notes = notes,
            IsFavorite = request.IsFavorite,
            UsageCount = 0,
            CreatedAt = now
        };

        db.DeliveryMeals.Add(deliveryMeal);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(deliveryMeal);
    }

    public async Task<DeliveryMealDto> UpdateAsync(Guid id, UpsertDeliveryMealRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var deliveryMeal = await db.DeliveryMeals.FirstOrDefaultAsync(k => k.Id == id && k.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new NotFoundException("Delivery meal", id);

        deliveryMeal.PlaceName = request.PlaceName.Trim();
        deliveryMeal.DishName = request.DishName.Trim();
        deliveryMeal.PortionDescription = request.PortionDescription.Trim();
        deliveryMeal.Carbs = request.Carbs;
        deliveryMeal.UsualInsulinUnits = request.UsualInsulinUnits;
        deliveryMeal.LastPreMealGlucose = request.LastPreMealGlucose;
        deliveryMeal.ResultRating = request.ResultRating;
        deliveryMeal.Tags = NormalizeTags(request.Tags);
        deliveryMeal.Notes = request.Notes;
        deliveryMeal.IsFavorite = request.IsFavorite;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(deliveryMeal);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deliveryMeal = await db.DeliveryMeals.FirstOrDefaultAsync(k => k.Id == id && k.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new NotFoundException("Delivery meal", id);

        db.DeliveryMeals.Remove(deliveryMeal);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DeliveryMealDto> ToggleFavoriteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deliveryMeal = await db.DeliveryMeals.FirstOrDefaultAsync(k => k.Id == id && k.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new NotFoundException("Delivery meal", id);

        deliveryMeal.IsFavorite = !deliveryMeal.IsFavorite;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(deliveryMeal);
    }

    public async Task<UseDeliveryMealDto> CreateMealDraftFromDeliveryMealAsync(Guid id, CancellationToken cancellationToken)
    {
        var deliveryMeal = await db.DeliveryMeals.FirstOrDefaultAsync(k => k.Id == id && k.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new NotFoundException("Delivery meal", id);

        var now = timeProvider.GetUtcNow();
        deliveryMeal.UsageCount += 1;
        deliveryMeal.LastUsedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        var notes = $"Delivery meal: {deliveryMeal.PlaceName} - {deliveryMeal.DishName}. {deliveryMeal.PortionDescription}";
        if (!string.IsNullOrWhiteSpace(deliveryMeal.Notes))
        {
            notes += $"{Environment.NewLine}{deliveryMeal.Notes}";
        }

        return new UseDeliveryMealDto(deliveryMeal.Id, deliveryMeal.Carbs, deliveryMeal.UsualInsulinUnits, notes);
    }

    private static void Validate(UpsertDeliveryMealRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlaceName) || string.IsNullOrWhiteSpace(request.DishName))
        {
            throw new ValidationException("Place name and dish name are required.");
        }

        if (string.IsNullOrWhiteSpace(request.PortionDescription))
        {
            throw new ValidationException("Portion description is required.");
        }

        if (request.Carbs <= 0)
        {
            throw new ValidationException("Carbs must be greater than zero.");
        }

        if (request.UsualInsulinUnits < 0)
        {
            throw new ValidationException("Usual insulin units cannot be negative.");
        }

        if (request.LastPreMealGlucose < 0)
        {
            throw new ValidationException("Last pre-meal glucose cannot be negative.");
        }
    }

    private static string NormalizeTags(string tags) =>
        string.Join(", ", tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase));

    private static DeliveryMealDto ToDto(DeliveryMeal deliveryMeal) =>
        new(
            deliveryMeal.Id,
            deliveryMeal.PlaceName,
            deliveryMeal.DishName,
            deliveryMeal.PortionDescription,
            deliveryMeal.Carbs,
            deliveryMeal.UsualInsulinUnits,
            deliveryMeal.LastPreMealGlucose,
            deliveryMeal.ResultRating,
            deliveryMeal.Tags,
            deliveryMeal.Notes,
            deliveryMeal.IsFavorite,
            deliveryMeal.UsageCount,
            deliveryMeal.LastUsedAt,
            deliveryMeal.CreatedAt);
}
