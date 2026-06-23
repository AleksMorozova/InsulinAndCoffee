using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class KnownMealService(IAppDbContext db)
{
    public async Task<KnownMealSectionsDto> GetSectionsAsync(string? search, CancellationToken cancellationToken)
    {
        var baseQuery = db.KnownMeals
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

        return new KnownMealSectionsDto(favorites, mostUsed, recentlyUsed, searchResults);
    }

    public async Task<KnownMealDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var knownMeal = await db.KnownMeals
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id && k.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Known meal was not found.");

        return ToDto(knownMeal);
    }

    public async Task<KnownMealDto> CreateAsync(UpsertKnownMealRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var knownMeal = new KnownMeal
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
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.KnownMeals.Add(knownMeal);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(knownMeal);
    }

    public async Task<KnownMealDto> CreateFromMealAsync(Guid mealId, CreateKnownMealFromMealRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlaceName) || string.IsNullOrWhiteSpace(request.DishName))
        {
            throw new ValidationException("Place name and dish name are required.");
        }

        var meal = await db.Meals
            .AsNoTracking()
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == mealId && m.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Meal was not found.");

        var notes = meal.Notes;
        if (string.IsNullOrWhiteSpace(notes))
        {
            notes = $"Saved from {meal.MealType} on {meal.MealTime:yyyy-MM-dd}.";
        }

        var knownMeal = new KnownMeal
        {
            Id = Guid.NewGuid(),
            UserId = DefaultUser.Id,
            PlaceName = request.PlaceName.Trim(),
            DishName = request.DishName.Trim(),
            PortionDescription = request.PortionDescription.Trim(),
            Carbs = meal.TotalCarbs,
            UsualInsulinUnits = meal.ConfirmedBolus,
            LastPreMealGlucose = meal.PreMealGlucose,
            ResultRating = request.ResultRating,
            Tags = NormalizeTags(request.Tags),
            Notes = notes,
            IsFavorite = request.IsFavorite,
            UsageCount = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.KnownMeals.Add(knownMeal);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(knownMeal);
    }

    public async Task<KnownMealDto> UpdateAsync(Guid id, UpsertKnownMealRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var knownMeal = await db.KnownMeals.FirstOrDefaultAsync(k => k.Id == id && k.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Known meal was not found.");

        knownMeal.PlaceName = request.PlaceName.Trim();
        knownMeal.DishName = request.DishName.Trim();
        knownMeal.PortionDescription = request.PortionDescription.Trim();
        knownMeal.Carbs = request.Carbs;
        knownMeal.UsualInsulinUnits = request.UsualInsulinUnits;
        knownMeal.LastPreMealGlucose = request.LastPreMealGlucose;
        knownMeal.ResultRating = request.ResultRating;
        knownMeal.Tags = NormalizeTags(request.Tags);
        knownMeal.Notes = request.Notes;
        knownMeal.IsFavorite = request.IsFavorite;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(knownMeal);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var knownMeal = await db.KnownMeals.FirstOrDefaultAsync(k => k.Id == id && k.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Known meal was not found.");

        db.KnownMeals.Remove(knownMeal);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<KnownMealDto> ToggleFavoriteAsync(Guid id, CancellationToken cancellationToken)
    {
        var knownMeal = await db.KnownMeals.FirstOrDefaultAsync(k => k.Id == id && k.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Known meal was not found.");

        knownMeal.IsFavorite = !knownMeal.IsFavorite;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(knownMeal);
    }

    public async Task<UseKnownMealDto> UseAgainAsync(Guid id, CancellationToken cancellationToken)
    {
        var knownMeal = await db.KnownMeals.FirstOrDefaultAsync(k => k.Id == id && k.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Known meal was not found.");

        knownMeal.UsageCount += 1;
        knownMeal.LastUsedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var notes = $"Ask Past Me: {knownMeal.PlaceName} - {knownMeal.DishName}. {knownMeal.PortionDescription}";
        if (!string.IsNullOrWhiteSpace(knownMeal.Notes))
        {
            notes += $"{Environment.NewLine}{knownMeal.Notes}";
        }

        return new UseKnownMealDto(knownMeal.Id, knownMeal.Carbs, knownMeal.UsualInsulinUnits, notes);
    }

    private static void Validate(UpsertKnownMealRequest request)
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

    private static KnownMealDto ToDto(KnownMeal knownMeal) =>
        new(
            knownMeal.Id,
            knownMeal.PlaceName,
            knownMeal.DishName,
            knownMeal.PortionDescription,
            knownMeal.Carbs,
            knownMeal.UsualInsulinUnits,
            knownMeal.LastPreMealGlucose,
            knownMeal.ResultRating,
            knownMeal.Tags,
            knownMeal.Notes,
            knownMeal.IsFavorite,
            knownMeal.UsageCount,
            knownMeal.LastUsedAt,
            knownMeal.CreatedAt);
}
