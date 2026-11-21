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
            if (item.SourceMealItemId.HasValue || item.SourceDeliveryMealId.HasValue || item.SourceDeliveryMealItemId.HasValue)
            {
                inputs.Add(ToInput(item));
                continue;
            }
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
            inputs.Add(ToInput(item) with { SourceDeliveryMealId = order.Id });
        }
        var current = await calculation.CalculateItemsAsync(inputs, null, null, cancellationToken);
        if (current.Where((item, index) => (item with { Provenance = null, SourceMealItemId = null, SourceDeliveryMealId = null, SourceDeliveryMealItemId = null })
            != (items[index] with { Provenance = null, SourceMealItemId = null, SourceDeliveryMealId = null, SourceDeliveryMealItemId = null })).Any())
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
        var score = AssistantFoodMatching.Score(target ?? "").Compile();
        var bestScore = items.Select(item => score(item.FoodName)).DefaultIfEmpty(0).Max();
        var matches = items.Where(item => string.IsNullOrWhiteSpace(target) ||
            bestScore > 0 && score(item.FoodName) == bestScore).ToArray();
        if (op is "add" or "replace" or "clarify")
        {
            if (current?.Issues.Count > 1)
                return Feedback("Several foods still need clarification. Resend the complete meal with the intended foods and quantities.");
            if (op == "replace" && current?.Issues.Count > 0)
                return Feedback("Clarify the unresolved food before replacing another item.");
            if (op == "replace" && matches.Length != 1) return Feedback("Which item should I replace?", search);
            if (op == "clarify" && current?.Pending is null) return current;
            var queries = op == "clarify" ? search.Foods.Select(food => food with
            {
                Quantity = food.Quantity ?? current?.Pending?.Quantity,
                Unit = food.Unit ?? current?.Pending?.Unit,
                Restaurant = food.FoodItemId.HasValue ? null : food.Restaurant ?? current?.Pending?.Restaurant
            }).ToArray() : search.Foods;
            var addition = await BuildAsync(queries, token);
            if (addition is null) return current;
            if (op == "replace" && addition.Issues.Count > 0) return Feedback(string.Join(" ", addition.Issues));
            if (op == "replace") items.Remove(matches[0]);
            items.AddRange(addition.Items);
            sources.AddRange(addition.Sources ?? []);
            return Finish(op == "add" ? (current?.Issues ?? []).Concat(addition.Issues).ToArray() : addition.Issues,
                op == "add" && current?.Pending is not null ? current.Pending : addition.Pending);
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
                (selected.FoodItemId == Guid.Empty && !selected.SourceDeliveryMealItemId.HasValue && quantity != 1m))
                return Feedback("That quantity is incompatible with the saved item.");
            var updated = (await calculation.CalculateItemsAsync(
                [ToInput(selected) with { Quantity = quantity }], null, null, token))[0];
            if (selected.SourceDeliveryMealItemId.HasValue && (updated.FoodName != selected.FoodName || updated.MeasurementType != selected.MeasurementType
                || updated.CarbsPer100g != selected.CarbsPer100g || updated.CarbsPerUnit != selected.CarbsPerUnit || updated.CarbOverride != selected.CarbOverride))
                return Feedback("The saved order item changed. Choose it again before changing quantities.");
            // A quantity operation transforms the trusted, persisted reference; it does not change its origin.
            if (!selected.SourceMealItemId.HasValue && !selected.SourceDeliveryMealId.HasValue && !selected.SourceDeliveryMealItemId.HasValue)
                updated = updated with { Provenance = selected.Provenance };
            items[items.IndexOf(selected)] = updated;
        }
        return Finish(current?.Issues ?? [], current?.Pending);

        AssistantDraft Feedback(string message, AssistantSearch? pendingEdit = null) =>
            (current ?? new AssistantDraft([], [], 0m)) with { EditFeedback = message, PendingEdit = pendingEdit };

        AssistantDraft Finish(IReadOnlyList<string> issues, AssistantFoodQuery? pending = null) =>
            new(items, issues, Math.Round(items.Sum(item => item.EffectiveCarbs), 2),
                sources.Where(source => items.Any(item => item.FoodName == source.Name)).Distinct().ToArray(),
                issues.Count == 1 ? pending : null);
    }

    private static MealItemInputDto ToInput(CalculatedMealItemDto item) =>
        new(item.FoodItemId, item.Quantity, MeasurementType: item.MeasurementType, FoodNameSnapshot: item.FoodName,
            CarbsPer100gSnapshot: item.CarbsPer100g, CarbsPerUnitSnapshot: item.CarbsPerUnit, CarbOverride: item.CarbOverride,
            SourceMealItemId: item.SourceMealItemId, SourceDeliveryMealId: item.SourceDeliveryMealId, SourceDeliveryMealItemId: item.SourceDeliveryMealItemId);

    public async Task<AssistantDraft?> BuildAsync(AssistantFoodQuery[] queries, CancellationToken cancellationToken)
    {
        if (queries.Length == 0) return null;
        var userId = currentUser.UserId;
        var inputs = new List<MealItemInputDto>();
        var issues = new List<string>();
        var sources = new List<AssistantDraftSource>();
        AssistantFoodQuery? pending = null;
        foreach (var original in queries)
        {
            var query = AssistantFoodMatching.Query(original);
            var ownedItems = db.DeliveryMealItems.AsNoTracking().Where(item => item.DeliveryMeal!.UserId == userId);
            var candidates = query.DeliveryMealItemId is Guid itemId
                ? await ownedItems.Where(item => item.Id == itemId).ToListAsync(cancellationToken)
                : query.FoodItemId.HasValue || query.DeliveryMealId.HasValue ? []
                : await FoodKnowledgeSearchService.FindItems(ownedItems, query, false).Take(2).ToListAsync(cancellationToken);
            if (query.DeliveryMealItemId.HasValue && candidates.Count == 0)
                throw new ValidationException("The selected order item is unavailable.");
            if (candidates.Count > 0)
            {
                var scoreItem = AssistantFoodMatching.Score(query.Name).Compile();
                if (!query.DeliveryMealItemId.HasValue) candidates = candidates.Where(item => scoreItem(item.Name) == scoreItem(candidates[0].Name)).ToList();
                var hasSavedFood = !query.DeliveryMealItemId.HasValue && string.IsNullOrWhiteSpace(query.Restaurant)
                    && await FoodKnowledgeSearchService.FindFoods(db.FoodItems.AsNoTracking().Where(food => food.UserId == userId), query.Name, false).AnyAsync(cancellationToken);
                if (candidates.Count != 1 || hasSavedFood)
                { pending = query; issues.Add($"More than one saved item matched {query.Name}. Choose a specific saved food or order item."); continue; }
                var candidate = candidates[0];
                if (query.Unit.HasValue && query.Unit != candidate.MeasurementType)
                { pending = query; issues.Add($"Specify a quantity in {candidate.MeasurementType} for {candidate.Name}."); continue; }
                inputs.Add(new(Guid.Empty, query.Quantity ?? candidate.Quantity, SourceDeliveryMealItemId: candidate.Id));
                sources.Add(new(candidate.Id, "AskPastMeItem", candidate.Name));
                continue;
            }
            if (query.DeliveryMealId is Guid wholeId)
            {
                if (query.Quantity is not (null or 1m) || query.Unit is not (null or FoodMeasurementType.Portion or FoodMeasurementType.Piece))
                    throw new ValidationException("Select a complete saved order once, then edit its individual item quantities.");
                var whole = await db.DeliveryMeals.AsNoTracking().Include(order => order.Items).SingleOrDefaultAsync(order => order.Id == wholeId && order.UserId == userId, cancellationToken)
                    ?? throw new ValidationException("The saved order is unavailable.");
                AddOrder(whole); continue;
            }
            if (!string.IsNullOrWhiteSpace(query.Restaurant))
            {
                var orders = await FoodKnowledgeSearchService.FindOrders(db.DeliveryMeals.AsNoTracking().Include(order => order.Items)
                    .Where(order => order.UserId == userId), query, false).Take(2).ToListAsync(cancellationToken);
                if (orders.Count > 1)
                {
                    var placeScore = AssistantFoodMatching.Score(query.Restaurant).Compile();
                    var dishScore = AssistantFoodMatching.Score(query.Name).Compile();
                    orders = orders.Where(order => placeScore(order.PlaceName) == placeScore(orders[0].PlaceName)
                        && (query.Name.Length == 0 || dishScore(order.DishName) == dishScore(orders[0].DishName))).ToList();
                }
                if (orders.Count != 1 || query.Quantity is not (null or 1m) ||
                    query.Unit is not (null or FoodMeasurementType.Piece or FoodMeasurementType.Portion) || orders[0].Carbs < 0)
                {
                    pending = query;
                    issues.Add($"Choose one complete saved order for {query.Name} from {query.Restaurant}.");
                    continue;
                }
                var order = orders[0];
                AddOrder(order);
                continue;
            }
            var owned = db.FoodItems.AsNoTracking().Where(food => food.UserId == userId);
            var foods = query.FoodItemId is Guid selectedId
                ? await owned.Where(food => food.Id == selectedId).ToListAsync(cancellationToken)
                : await FoodKnowledgeSearchService.FindFoods(owned, query.Name, false).Take(2).ToListAsync(cancellationToken);
            if (query.FoodItemId.HasValue && foods.Count == 0)
                throw new ValidationException("The selected food is no longer available. Search again.");
            if (foods.Count > 1)
            {
                var foodScore = AssistantFoodMatching.Score(query.Name).Compile();
                foods = foods.Where(food => foodScore(food.Name) == foodScore(foods[0].Name)).ToList();
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
                pending = query;
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
        return new AssistantDraft(items, issues, Math.Round(items.Sum(item => item.EffectiveCarbs), 2), sources,
            issues.Count == 1 ? pending : null);

        void AddOrder(InsulinAndCoffee.Domain.Entities.DeliveryMeal order)
        {
            if (inputs.Count + Math.Max(1, order.Items.Count) > 100) throw new ValidationException("A draft supports at most 100 items.");
            if (order.Items.Count > 0)
            {
                foreach (var item in order.Items.OrderBy(item => item.DisplayOrder))
                { inputs.Add(new(Guid.Empty, item.Quantity, SourceDeliveryMealItemId: item.Id)); sources.Add(new(item.Id, "AskPastMeItem", item.Name)); }
            }
            else
            {
                var label = $"{order.PlaceName} · {order.DishName}";
                inputs.Add(new(Guid.Empty, 1m, SourceDeliveryMealId: order.Id));
                sources.Add(new(order.Id, "AskPastMe", label));
            }
        }
    }
}
