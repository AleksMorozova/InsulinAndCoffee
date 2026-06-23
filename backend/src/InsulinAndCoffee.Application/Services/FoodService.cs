using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class FoodService(IAppDbContext db)
{
    public async Task<IReadOnlyList<FoodItemDto>> GetFoodsAsync(string? search, CancellationToken cancellationToken)
    {
        var query = db.FoodItems
            .AsNoTracking()
            .Where(f => f.UserId == DefaultUser.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(f => f.Name.ToLower().Contains(term));
        }

        return await query
            .OrderByDescending(f => f.IsFavorite)
            .ThenBy(f => f.Name)
            .Select(f => ToDto(f))
            .ToListAsync(cancellationToken);
    }

    public async Task<FoodItemDto> CreateFoodAsync(UpsertFoodItemRequest request, CancellationToken cancellationToken)
    {
        ValidateFood(request);

        var food = new FoodItem
        {
            Id = Guid.NewGuid(),
            UserId = DefaultUser.Id,
            Name = request.Name.Trim(),
            CarbsPer100g = request.CarbsPer100g,
            ProteinPer100g = request.ProteinPer100g,
            FatPer100g = request.FatPer100g,
            CaloriesPer100g = request.CaloriesPer100g,
            IsFavorite = request.IsFavorite,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.FoodItems.Add(food);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(food);
    }

    public async Task<FoodItemDto> UpdateFoodAsync(Guid id, UpsertFoodItemRequest request, CancellationToken cancellationToken)
    {
        ValidateFood(request);

        var food = await db.FoodItems.FirstOrDefaultAsync(f => f.Id == id && f.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Food item was not found.");

        food.Name = request.Name.Trim();
        food.CarbsPer100g = request.CarbsPer100g;
        food.ProteinPer100g = request.ProteinPer100g;
        food.FatPer100g = request.FatPer100g;
        food.CaloriesPer100g = request.CaloriesPer100g;
        food.IsFavorite = request.IsFavorite;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(food);
    }

    public async Task DeleteFoodAsync(Guid id, CancellationToken cancellationToken)
    {
        var food = await db.FoodItems.FirstOrDefaultAsync(f => f.Id == id && f.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Food item was not found.");

        db.FoodItems.Remove(food);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateFood(UpsertFoodItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Food name is required.");
        }

        if (request.CarbsPer100g < 0 || request.ProteinPer100g < 0 || request.FatPer100g < 0 || request.CaloriesPer100g < 0)
        {
            throw new ValidationException("Nutrition values cannot be negative.");
        }
    }

    private static FoodItemDto ToDto(FoodItem food) =>
        new(food.Id, food.Name, food.CarbsPer100g, food.ProteinPer100g, food.FatPer100g, food.CaloriesPer100g, food.IsFavorite, food.CreatedAt);
}
