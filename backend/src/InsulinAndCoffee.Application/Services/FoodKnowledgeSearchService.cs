using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class FoodKnowledgeSearchService(IAppDbContext db, ICurrentUser currentUser)
{
    internal static string[] Terms(string name) => AssistantFoodMatching.Terms(name);

    internal static string LookupName(string message)
    {
        var normalized = message.Trim().TrimEnd('.', '!', '?').TrimEnd();
        foreach (var suffix in new[] { " как обычно", " як завжди", " as usual" })
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return normalized[..^suffix.Length];
        return normalized;
    }

    internal static bool IsKnownTerm(string message) => Terms(message).Length > 1;

    internal static IQueryable<FoodItem> FindFoods(IQueryable<FoodItem> foods, string name, bool discovery)
    {
        var score = AssistantFoodMatching.On<FoodItem>(food => food.Name, AssistantFoodMatching.Score(name, discovery));
        return foods.Where(AssistantFoodMatching.Positive(score, discovery ? 1 : 3)).OrderByDescending(score).ThenBy(food => food.Id);
    }

    internal static IQueryable<DeliveryMeal> FindOrders(IQueryable<DeliveryMeal> orders, AssistantFoodQuery food, bool discovery)
    {
        Expression<Func<DeliveryMeal, int>> place = order => 0;
        if (!string.IsNullOrWhiteSpace(food.Restaurant))
        {
            place = AssistantFoodMatching.On<DeliveryMeal>(order => order.PlaceName, AssistantFoodMatching.Score(food.Restaurant));
            orders = orders.Where(AssistantFoodMatching.Positive(place, 3));
        }
        if (string.IsNullOrWhiteSpace(food.Name)) return orders.OrderByDescending(place).ThenBy(order => order.Id);
        var dish = AssistantFoodMatching.On<DeliveryMeal>(order => order.DishName, AssistantFoodMatching.Score(food.Name, discovery));
        if (!discovery) return orders.Where(AssistantFoodMatching.Positive(dish, 3)).OrderByDescending(place).ThenByDescending(dish).ThenBy(order => order.Id);
        var text = AssistantFoodMatching.On<DeliveryMeal>(order => order.PlaceName + " " + order.DishName + " " + order.PortionDescription + " " + order.Tags + " " + (order.Notes ?? ""),
            AssistantFoodMatching.Score(food.Name, true));
        return orders.Where(AssistantFoodMatching.Positive(text)).OrderByDescending(place).ThenByDescending(dish).ThenBy(order => order.Id);
    }

    internal static IQueryable<DeliveryMealItem> FindItems(IQueryable<DeliveryMealItem> items, AssistantFoodQuery food, bool discovery)
    {
        if (!string.IsNullOrWhiteSpace(food.Restaurant))
            items = items.Where(AssistantFoodMatching.Positive(AssistantFoodMatching.On<DeliveryMealItem>(item => item.DeliveryMeal!.PlaceName,
                AssistantFoodMatching.Score(food.Restaurant)), 3));
        if (string.IsNullOrWhiteSpace(food.Name)) return items.OrderBy(item => item.DeliveryMealId).ThenBy(item => item.DisplayOrder);
        var score = AssistantFoodMatching.On<DeliveryMealItem>(item => item.Name, AssistantFoodMatching.Score(food.Name, discovery));
        return items.Where(AssistantFoodMatching.Positive(score, discovery ? 1 : 3)).OrderByDescending(score).ThenBy(item => item.Id);
    }

    public async Task<AssistantKnowledge> SearchAsync(AssistantFoodQuery[] foods, CancellationToken cancellationToken)
    {
        var owner = currentUser.UserId;
        var saved = new List<AssistantSavedFood>();
        var orders = new List<AssistantOrder>();
        var items = new List<AssistantOrderItem>();
        foreach (var original in foods)
        {
            var food = AssistantFoodMatching.Query(original with { Name = LookupName(original.Name) });
            var searches = food.Name.Length > 0 && Terms(food.Name)[0] is "sushi" or "roll"
                ? new[] { food.Name, Terms(food.Name)[0] == "sushi" ? "roll" : "sushi" } : [food.Name];
            foreach (var name in searches)
            {
                items.AddRange(await FindItems(db.DeliveryMealItems.AsNoTracking().Where(item => item.DeliveryMeal!.UserId == owner), food with { Name = name }, true)
                    .Take(8).Select(item => new AssistantOrderItem(item.Id, item.DeliveryMealId, item.DeliveryMeal!.PlaceName, item.DeliveryMeal.DishName,
                        item.Name, item.Quantity, item.MeasurementType, item.CarbsPer100gSnapshot, item.CarbsPerUnitSnapshot,
                        item.CarbOverride ?? item.CalculatedCarbs, new(item.CarbReferenceSource, item.CarbTransformation, item.SourceMealVerifiedAt))).ToListAsync(cancellationToken));
                if (string.IsNullOrWhiteSpace(food.Restaurant))
                    saved.AddRange(await FindFoods(db.FoodItems.AsNoTracking().Where(item => item.UserId == owner), name, true)
                        .Take(8).Select(item => new AssistantSavedFood(item.Id, item.Name, item.MeasurementType, item.CarbsPer100g, item.CarbsPerUnit,
                            new(item.CarbReferenceSource, InsulinAndCoffee.Domain.Enums.CarbTransformation.None, null)))
                        .ToListAsync(cancellationToken));
                orders.AddRange(await FindOrders(db.DeliveryMeals.AsNoTracking().Where(item => item.UserId == owner), food with { Name = name }, true)
                    .Take(8).Select(item => new AssistantOrder(item.Id, item.PlaceName, item.DishName, item.PortionDescription, item.Carbs, item.LastUsedAt,
                        new(item.CarbReferenceSource, item.CarbTransformation, item.SourceMealVerifiedAt)))
                    .ToListAsync(cancellationToken));
            }
        }
        return new(saved.DistinctBy(item => item.Id).Take(8).ToArray(), orders.DistinctBy(item => item.Id).Take(8).ToArray(), items.DistinctBy(item => item.Id).Take(8).ToArray());
    }
}
