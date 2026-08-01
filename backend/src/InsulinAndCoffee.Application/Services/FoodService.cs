using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class FoodService(IAppDbContext db, TimeProvider timeProvider)
{
    private const int MaxPageSize = 100;

    public async Task<PaginatedResult<FoodItemDto>> GetFoodsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePagination(page, pageSize);

        var query = db.FoodItems
            .AsNoTracking()
            .Where(f => f.UserId == DefaultUser.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(f => f.Name.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = (long)(page - 1) * pageSize;
        if (offset > int.MaxValue)
        {
            throw new ValidationException("Requested page is too large.");
        }

        var items = await query
            .OrderByDescending(f => f.IsFavorite)
            .ThenBy(f => f.Name)
            .Skip((int)offset)
            .Take(pageSize)
            .Select(f => ToDto(f))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PaginatedResult<FoodItemDto>(items, page, pageSize, totalCount, totalPages);
    }

    public async Task<FoodItemDto> CreateFoodAsync(UpsertFoodItemRequest request, CancellationToken cancellationToken)
    {
        ValidateFood(request);
        var now = timeProvider.GetUtcNow();

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
            CreatedAt = now
        };

        db.FoodItems.Add(food);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(food);
    }

    public async Task<FoodItemDto> UpdateFoodAsync(Guid id, UpsertFoodItemRequest request, CancellationToken cancellationToken)
    {
        ValidateFood(request);

        var food = await db.FoodItems.FirstOrDefaultAsync(f => f.Id == id && f.UserId == DefaultUser.Id, cancellationToken)
            ?? throw new NotFoundException("Food item", id);

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
            ?? throw new NotFoundException("Food item", id);

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

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page <= 0)
        {
            throw new ValidationException("Page must be greater than zero.");
        }

        if (pageSize <= 0 || pageSize > MaxPageSize)
        {
            throw new ValidationException($"Page size must be between 1 and {MaxPageSize}.");
        }
    }

    private static FoodItemDto ToDto(FoodItem food) =>
        new(food.Id, food.Name, food.CarbsPer100g, food.ProteinPer100g, food.FatPer100g, food.CaloriesPer100g, food.IsFavorite, food.CreatedAt);
}
