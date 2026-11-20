using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class AssistantDraftBuilder(IAppDbContext db, ICurrentUser currentUser, MealCalculationService calculation)
{
    public async Task<IReadOnlyList<CalculatedMealItemDto>> PrepareAsync(
        IReadOnlyList<CalculatedMealItemDto> items, CancellationToken cancellationToken, IReadOnlyList<AssistantDraftSource>? sources = null)
    {
        if (items.Any(item => item.Quantity <= 0 || !Enum.IsDefined(item.MeasurementType)
            || (item.MeasurementType == FoodMeasurementType.Grams ? item.CarbsPer100g : item.CarbsPerUnit) is null or < 0))
            throw new ValidationException("The draft contains invalid quantities or unavailable carbohydrate data.");
        var inputs = new List<MealItemInputDto>();
        foreach (var item in items)
        {
            if (item.FoodItemId != Guid.Empty)
            {
                inputs.Add(new MealItemInputDto(item.FoodItemId, item.Quantity, CarbOverride: item.CarbOverride));
                continue;
            }
            var source = sources?.SingleOrDefault(source => source.Name == item.FoodName && source.Kind == "AskPastMe")
                ?? throw new ValidationException("The saved order reference is unavailable.");
            var owner = currentUser.UserId;
            var order = await db.DeliveryMeals.AsNoTracking().SingleOrDefaultAsync(
                order => order.Id == source.RecordId && order.UserId == owner, cancellationToken);
            if (order is null || order.Carbs != item.CarbsPerUnit || item.Quantity != 1m ||
                item.FoodName != $"{order.PlaceName} · {order.DishName}")
                throw new ValidationException("The saved order changed or was deleted. Refresh your draft.");
            inputs.Add(ToInput(item));
        }
        var current = await calculation.CalculateItemsAsync(inputs, null, null, cancellationToken);
        if (current.Where((item, index) => item != items[index]).Any())
            throw new ValidationException("Saved food values changed. Ask for an updated meal draft.");
        return current;
    }

    public async Task<AssistantDraft?> UpdateAsync(AssistantSearch search, AssistantDraft? current, CancellationToken token)
    {
        var op = search.Operation ?? "create";
        if (op == "create") return await BuildAsync(search.Foods, token);
        if (op == "clear") return new AssistantDraft([], [], 0m);
        if (op == "show") return current;

        var items = current?.Items.ToList() ?? [];
        var sources = current?.Sources?.ToList() ?? [];
        if (op == "clarify" && current?.PendingEdit is { } edit)
        {
            if (search.Foods.Length != 1) return current;
            return await UpdateAsync(edit with { Target = search.Foods[0].Name }, current, token);
        }

        var quantityEdit = op is "set" or "increase" or "decrease";
        var portion = search.Foods.Length == 1 ? search.Foods[0] : null;
        var target = string.IsNullOrWhiteSpace(search.Target) && quantityEdit ? portion?.Name : search.Target;
        if (quantityEdit && FoodKnowledgeSearchService.Terms(target ?? "").Any(term =>
                term is "carb" or "carbs" or "carbohydrates" or "углеводы" or "углеводов" or "вуглеводи" or "вуглеводів"))
            return Feedback("Change carbohydrate overrides in Calculator.");
        var matches = items.Where(item => string.IsNullOrWhiteSpace(target) ||
            FoodKnowledgeSearchService.Terms(target).Any(term => term.Length > 0 &&
                (" " + item.FoodName.ToLowerInvariant() + " ").Contains(" " + term + " ", StringComparison.Ordinal))).ToArray();
        if (!string.IsNullOrWhiteSpace(target))
        {
            var exact = items.Where(item => item.FoodName.Equals(target, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (exact.Length > 0) matches = exact;
        }
        if (op is "add" or "replace" or "clarify")
        {
            if (op == "replace" && matches.Length != 1) return Finish(["Which item should I replace?"]);
            if (op == "clarify" && current?.Pending is null) return current;
            var queries = op == "clarify" ? search.Foods.Select(food => food with
            {
                Quantity = food.Quantity ?? current?.Pending?.Quantity,
                Unit = food.Unit ?? current?.Pending?.Unit,
                Restaurant = food.Restaurant ?? current?.Pending?.Restaurant
            }).ToArray() : search.Foods;
            var addition = await BuildAsync(queries, token);
            if (addition is null) return current;
            if (op == "replace" && addition.Issues.Count > 0) return Finish(addition.Issues);
            if (op == "replace") items.Remove(matches[0]);
            items.AddRange(addition.Items);
            sources.AddRange(addition.Sources ?? []);
            return Finish(addition.Issues, addition.Pending);
        }

        if (matches.Length == 0) return Feedback("That food is not in your draft. Ask to add it first.");
        if (matches.Length > 1)
            return Feedback("Which item should I change: " + string.Join(", ", matches.Select(item => item.FoodName)) + "?",
                quantityEdit ? search : null);
        var selected = matches[0];
        if (op == "remove")
        {
            items.Remove(selected);
            return Finish(current?.Issues ?? [], current?.Pending);
        }
        if (portion?.Quantity is not > 0 || (portion.Unit is { } unit && unit != selected.MeasurementType))
            return Feedback("Specify a positive quantity in the saved unit.");
        var quantity = op switch
        {
            "set" => portion.Quantity.Value,
            "increase" => selected.Quantity + portion.Quantity.Value,
            "decrease" => selected.Quantity - portion.Quantity.Value,
            _ => throw new ValidationException("Unsupported draft operation.")
        };
        if (quantity == 0 && op == "decrease") items.Remove(selected);
        else
        {
            if (quantity is < 0.1m or > 10000m ||
                (selected.MeasurementType == FoodMeasurementType.Piece && decimal.Truncate(quantity) != quantity) ||
                (selected.FoodItemId == Guid.Empty && quantity != 1m))
                return Feedback("That quantity is incompatible with the saved item.");
            items[items.IndexOf(selected)] = (await calculation.CalculateItemsAsync(
                [ToInput(selected) with { Quantity = quantity }], null, null, token))[0];
        }
        return Finish(current?.Issues ?? [], current?.Pending);

        AssistantDraft Feedback(string message, AssistantSearch? pendingEdit = null) =>
            (current ?? new AssistantDraft([], [], 0m)) with { EditFeedback = message, PendingEdit = pendingEdit };

        AssistantDraft Finish(IReadOnlyList<string> issues, AssistantFoodQuery? pending = null) =>
            new(items, issues, Math.Round(items.Sum(item => item.EffectiveCarbs), 2),
                sources.Where(source => items.Any(item => item.FoodName == source.Name)).Distinct().ToArray(), pending);
    }

    private static MealItemInputDto ToInput(CalculatedMealItemDto item) =>
        new(item.FoodItemId, item.Quantity, MeasurementType: item.MeasurementType, FoodNameSnapshot: item.FoodName,
            CarbsPer100gSnapshot: item.CarbsPer100g, CarbsPerUnitSnapshot: item.CarbsPerUnit, CarbOverride: item.CarbOverride);

    public async Task<AssistantDraft?> BuildAsync(AssistantFoodQuery[] queries, CancellationToken cancellationToken)
    {
        if (queries.Length == 0) return null;
        var userId = currentUser.UserId;
        var inputs = new List<MealItemInputDto>();
        var issues = new List<string>();
        var sources = new List<AssistantDraftSource>();
        AssistantFoodQuery? pending = null;
        foreach (var query in queries)
        {
            if (!string.IsNullOrWhiteSpace(query.Restaurant))
            {
                var places = FoodKnowledgeSearchService.Terms(query.Restaurant);
                var dishes = FoodKnowledgeSearchService.Terms(query.Name);
                var orders = await db.DeliveryMeals.AsNoTracking().Where(order => order.UserId == userId
                    && places.Contains(order.PlaceName.ToLower()) && dishes.Contains(order.DishName.ToLower()))
                    .OrderBy(order => order.Id).Take(2).ToListAsync(cancellationToken);
                if (orders.Count != 1 || query.Quantity is not (null or 1m) ||
                    query.Unit is not (null or FoodMeasurementType.Piece or FoodMeasurementType.Portion) || orders[0].Carbs <= 0)
                {
                    pending = query;
                    issues.Add($"Choose one complete saved order for {query.Name} from {query.Restaurant}.");
                    continue;
                }
                var order = orders[0];
                var label = $"{order.PlaceName} · {order.DishName}";
                inputs.Add(new MealItemInputDto(Guid.Empty, 1m, MeasurementType: FoodMeasurementType.Portion,
                    FoodNameSnapshot: label, CarbsPerUnitSnapshot: order.Carbs));
                sources.Add(new AssistantDraftSource(order.Id, "AskPastMe", label));
                continue;
            }
            var names = FoodKnowledgeSearchService.Terms(query.Name);
            var name = names[0];
            var exactNames = names.Concat(names.Select(alias => "[meal assistant local fixture v1] " + alias)).ToArray();
            var foods = await db.FoodItems.AsNoTracking()
                .Where(food => food.UserId == userId && exactNames.Contains(food.Name.ToLower()))
                .OrderBy(food => food.Id).Take(2).ToListAsync(cancellationToken);
            if (foods.Count == 0)
            {
                var prefix = name + " ";
                var suffix = " " + name;
                var infix = " " + name + " ";
                foods = await db.FoodItems.AsNoTracking().Where(food => food.UserId == userId &&
                    (food.Name.ToLower().StartsWith(prefix) || food.Name.ToLower().EndsWith(suffix)
                    || food.Name.ToLower().Contains(infix))).OrderBy(food => food.Id).Take(2).ToListAsync(cancellationToken);
            }
            if (foods.Count != 1)
            {
                pending = query;
                issues.Add(foods.Count == 0 ? $"No saved food matched {query.Name}." : $"More than one saved food matched {query.Name}.");
                continue;
            }

            var food = foods[0];
            var quantity = query.Quantity ?? (food.MeasurementType == FoodMeasurementType.Grams ? null : 1m);
            if ((query.Unit.HasValue && query.Unit != food.MeasurementType) || quantity is null or < 0.1m
                || (food.MeasurementType == FoodMeasurementType.Piece && decimal.Truncate(quantity.Value) != quantity.Value))
            {
                issues.Add($"Specify a compatible quantity for {query.Name} ({food.MeasurementType}).");
                continue;
            }

            if ((food.MeasurementType == FoodMeasurementType.Grams ? food.CarbsPer100g : food.CarbsPerUnit) is null)
            {
                issues.Add($"Saved carbohydrate values are unavailable for {query.Name}.");
                continue;
            }

            inputs.Add(new MealItemInputDto(food.Id, quantity, null, null));
        }

        var items = await calculation
            .CalculateItemsAsync(inputs, null, null, cancellationToken);
        return new AssistantDraft(items, issues, Math.Round(items.Sum(item => item.EffectiveCarbs), 2), sources, pending);
    }
}
