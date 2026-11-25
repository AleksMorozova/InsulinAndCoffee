using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace InsulinAndCoffee.Application.Services;

public class AssistantDraftBuilder(IAppDbContext db, ICurrentUser currentUser, MealCalculationService calculation,
    ILogger<AssistantDraftBuilder>? logger = null, AiRequestContext? tracking = null)
{
    private static string? FoodContext(InsulinAndCoffee.Domain.Entities.FoodItem food) =>
        (food.MeasurementType == FoodMeasurementType.Grams ? food.CarbsPer100g : food.CarbsPerUnit) is not decimal carbs ? null
            : food.MeasurementType == FoodMeasurementType.Grams ? $"{carbs} g / 100 g" : $"{carbs} g / 1 {food.MeasurementType}";
    public static AssistantDraft HydrateLegacy(AssistantDraft draft, string question = "")
    {
        draft = WithIdentities(draft);
        if (draft.Unresolved is not null || draft.Issues.Count == 0) return draft;
        var parsed = AssistantIntentText.Interpret(question, false)?.Foods ?? [];
        var entries = draft.Issues.Select((detail, index) =>
        {
            var match = System.Text.RegularExpressions.Regex.Match(detail,
                @"^(?:No saved food matched |Не найден сохранённый продукт: |Не знайдено збережений продукт: |Choose one complete saved order for |Выбери сохранённый заказ |Обери збережене замовлення )(.+?)(?: from | из | із |\.$)");
            var name = match.Success ? match.Groups[1].Value : draft.Pending?.Name ?? "";
            var extracted = parsed.FirstOrDefault(query => name.Length > 0 && AssistantFoodMatching.Score(name, true).Compile()(query.Name) > 0);
            var query = extracted ?? draft.Pending ?? new(name, null, null);
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{index}/{detail}"));
            return new AssistantUnresolvedItem(new Guid(hash[..16]), extracted?.OriginalText ?? (name.Length > 0 ? name : detail), query,
                AssistantResolutionReason.NoMatches, detail, []);
        }).ToArray();
        return draft with { Unresolved = entries, Pending = entries.Length == 1 ? entries[0].Query : null };
    }

    public static AssistantDraft WithIdentities(AssistantDraft draft)
    {
        var seen = new HashSet<Guid>();
        var identified = draft with { Items = draft.Items.Select((item, index) =>
        {
            if (item.DraftItemId is Guid existing && existing != Guid.Empty && seen.Add(existing)) return item;
            // Stable across old JSON reads and metadata changes; first mutation persists the ID.
            var salt = 0;
            Guid identity;
            do { identity = new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                $"{index}/{item.SourceItemId}/{item.FoodItemId}/{item.FoodName}/{salt++}"))[..16]); } while (!seen.Add(identity));
            return item with { DraftItemId = identity };
        }).ToArray() };
        var groups = identified.Items.Where(item => item.SourceItemId.HasValue)
            .GroupBy(item => (Kind: SourceKind(item), item.SourceItemId)).Where(group => group.Count() > 1)
            .Where(group => !group.All(item => identified.SeparateItemIds?.Contains(item.DraftItemId!.Value) == true))
            .Select(group => new AssistantDuplicateGroup(group.Select(item => item.DraftItemId!.Value).ToArray(), CanCombine(group.ToArray()))).ToArray();
        return identified with { DuplicateGroups = groups };
    }

    private static string SourceKind(CalculatedMealItemDto item) => item.SourceDeliveryMealItemId.HasValue ? "DeliveryMealItem"
        : item.SourceMealItemId.HasValue ? "MealItem" : item.SourceDeliveryMealId.HasValue ? "DeliveryMeal" : "Food";

    private static bool CanCombine(CalculatedMealItemDto[] items) => items.Length > 1 && items.All(item => item.Quantity > 0 && Enum.IsDefined(item.MeasurementType)) &&
        (items[0].MeasurementType == FoodMeasurementType.Grams ? items[0].CarbsPer100g : items[0].CarbsPerUnit) is >= 0 && items.Skip(1).All(item => SameReference(items[0], item)) &&
        items.Sum(item => item.Quantity) <= 10000m && InsulinAndCoffee.Domain.Calculations.FoodCarbCalculator.Calculate(items[0].MeasurementType,
            items.Sum(item => item.Quantity), items[0].CarbsPer100g, items[0].CarbsPerUnit) == items.Sum(item => item.CalculatedCarbs);

    private static bool SameReference(CalculatedMealItemDto left, CalculatedMealItemDto right) =>
        HasConsistentCarbs(left) && HasConsistentCarbs(right) && left.SourceItemId.HasValue && SourceKind(left) == SourceKind(right) && left.SourceItemId == right.SourceItemId && left.FoodItemId == right.FoodItemId &&
        left.SourceDeliveryMealId == right.SourceDeliveryMealId && left.SourceDeliveryMealItemId == right.SourceDeliveryMealItemId && left.SourceMealItemId == right.SourceMealItemId &&
        left.MeasurementType == right.MeasurementType && left.CarbsPer100g == right.CarbsPer100g && left.CarbsPerUnit == right.CarbsPerUnit &&
        left.CarbOverride == right.CarbOverride && left.Provenance == right.Provenance && (!left.CarbOverride.HasValue || left.Quantity == right.Quantity);

    private static bool HasConsistentCarbs(CalculatedMealItemDto item) => item.Quantity > 0 && Enum.IsDefined(item.MeasurementType) &&
        (item.MeasurementType == FoodMeasurementType.Grams ? item.CarbsPer100g : item.CarbsPerUnit) is >= 0 &&
        item.EffectiveCarbs == (item.CarbOverride ?? item.CalculatedCarbs) && item.CalculatedCarbs ==
            InsulinAndCoffee.Domain.Calculations.FoodCarbCalculator.Calculate(item.MeasurementType, item.Quantity, item.CarbsPer100g, item.CarbsPerUnit);

    private async Task<CalculatedMealItemDto> SetQuantityAsync(CalculatedMealItemDto item, decimal quantity, CancellationToken token)
    {
        var input = ToInput(item) with { Quantity = quantity, CarbOverride = item.CarbOverride.HasValue
            ? InsulinAndCoffee.Domain.Calculations.FoodCarbCalculator.ScaleTotal(item.CarbOverride.Value, item.Quantity, quantity) : null };
        var updated = (await calculation.CalculateItemsAsync([input], null, null, token))[0];
        return updated with { DraftItemId = item.DraftItemId, Provenance = item.Provenance };
    }
    private void Trace(AssistantFoodQuery query, string status, AssistantCandidateKind? kind, Guid? selected, int count, int score, long started) =>
        logger?.LogInformation("Assistant match: operation {OperationId}, conversation {ConversationId}, correlation {CorrelationId}, sources {Sources}, candidates {CandidateCount}, score {Score}, selected {SelectedId}, type {CandidateType}, resolution {Resolution}, elapsed {ElapsedMs}",
            tracking?.OperationId, tracking?.ConversationId, tracking?.CorrelationId,
            query.DeliveryMealId.HasValue ? "DeliveryMeal" : query.DeliveryMealItemId.HasValue ? "DeliveryMealItem" : query.FoodItemId.HasValue ? "Food"
                : string.IsNullOrWhiteSpace(query.Restaurant) ? "DeliveryMealItem,Food" : "DeliveryMealItem,DeliveryMeal",
            count, score, selected, kind, status, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

    public async Task<AssistantDraft> ResolveAsync(AssistantDraft? current, AssistantResolution resolution, CancellationToken token, Guid? operationId = null)
    {
        if (current is not null) current = WithIdentities(current);
        if (resolution.Action != "remove" && (resolution.Quantity is <= 0 or > 10000 || resolution.Unit is { } unit && !Enum.IsDefined(unit)))
            throw new ValidationException("Specify a positive quantity and a valid unit.");
        var unresolved = current?.Unresolved?.SingleOrDefault(item => item.Id == resolution.UnresolvedId)
            ?? throw new ValidationException("Choose an unresolved item from the current draft.");
        if (resolution.Action is not ("search" or "select" or "manual" or "remove" or "update")) throw new ValidationException("Unknown clarification action.");
        var remaining = current!.Unresolved!.Where(item => item.Id != unresolved.Id).ToList();
        var items = current.Items.ToList(); var sources = current.Sources?.ToList() ?? [];
        if (resolution.Action != "remove")
        {
            var query = unresolved.Query with { Quantity = resolution.Quantity ?? unresolved.Query.Quantity,
                Unit = resolution.Unit ?? unresolved.Query.Unit, UnitInferred = resolution.Unit.HasValue ? false : unresolved.Query.UnitInferred };
            if (resolution.SearchName is not null)
            {
                if (string.IsNullOrWhiteSpace(resolution.SearchName) || resolution.SearchName.Trim().Length > 80) throw new ValidationException("Enter a food name of 1–80 characters.");
                var name = resolution.SearchName.Trim();
                query = query with { Name = name, Restaurant = string.Equals(name, query.Name, StringComparison.OrdinalIgnoreCase) ? query.Restaurant : null };
            }
            if (resolution.Action == "manual")
            {
                if (query.Quantity is null or <= 0 or > 10000 || query.Unit is null || !Enum.IsDefined(query.Unit.Value)
                    || resolution.Carbs is null or < 0 or > 999999.99m || decimal.Round(resolution.Carbs.Value, 2) != resolution.Carbs.Value)
                    throw new ValidationException("Enter quantity, unit and non-negative total carbohydrates (up to two decimals).");
                MealCalculationService.ValidateQuantity(query.Unit.Value, query.Quantity.Value);
                var basis = InsulinAndCoffee.Domain.Calculations.FoodCarbCalculator.ReferenceFromTotal(query.Unit.Value, query.Quantity.Value, resolution.Carbs.Value);
                if (basis > (query.Unit == FoodMeasurementType.Grams ? 99999.99m : 999999.99m)) throw new ValidationException("The manual reference exceeds the supported storage range.");
                var manual = (await calculation.CalculateItemsAsync([new(Guid.Empty, query.Quantity, MeasurementType: query.Unit,
                    FoodNameSnapshot: query.Name, CarbsPer100gSnapshot: query.Unit == FoodMeasurementType.Grams ? basis : null,
                    CarbsPerUnitSnapshot: query.Unit != FoodMeasurementType.Grams ? basis : null, CarbOverride: resolution.Carbs)], null, null, token))[0]
                    with { Provenance = new(CarbReferenceSource.Manual, CarbTransformation.Calculated) };
                items.Add(manual with { DraftItemId = unresolved.Id }); sources.Add(new(unresolved.Id, "Manual", manual.FoodName));
            }
            else
            {
                if (resolution.Action == "select")
                {
                    if (resolution.SourceId is null || resolution.SourceKind is not ("Food" or "DeliveryMealItem" or "DeliveryMeal")) throw new ValidationException("Choose a saved source.");
                    query = query with { FoodItemId = resolution.SourceKind == "Food" ? resolution.SourceId : null,
                        DeliveryMealItemId = resolution.SourceKind == "DeliveryMealItem" ? resolution.SourceId : null,
                        DeliveryMealId = resolution.SourceKind == "DeliveryMeal" ? resolution.SourceId : null, Restaurant = null };
                }
                var result = await BuildAsync([query], token, operationId) ?? throw new ValidationException("Enter a food to resolve.");
                items.AddRange(result.Items.Select((item, index) => index == 0 ? item with { DraftItemId = unresolved.Id } : item)); sources.AddRange(result.Sources ?? []);
                remaining.AddRange((result.Unresolved ?? []).Select(item => item with { Id = unresolved.Id, OriginalText = unresolved.OriginalText,
                    RequestedItemId = unresolved.RequestedItemId ?? unresolved.Id, OperationId = unresolved.OperationId ?? operationId }));
            }
        }
        return WithIdentities(current with { Items = items, Sources = sources, Unresolved = remaining, Issues = remaining.Select(item => item.Detail).ToArray(),
            Pending = remaining.Count == 1 ? remaining[0].Query : null, PendingEdit = null, EditFeedback = null, TotalCarbs = Math.Round(items.Sum(item => item.EffectiveCarbs), 2) });
    }
    public async Task<AssistantDraft> EditItemAsync(AssistantDraft? current, AssistantDraftEdit edit, CancellationToken token)
    {
        if (current is not null) current = WithIdentities(current);
        var index = edit.DraftItemId.HasValue ? current?.Items.ToList().FindIndex(item => item.DraftItemId == edit.DraftItemId) ?? -1 : edit.ItemIndex;
        if (current is not null && index < 0 && edit.DraftItemId.HasValue && edit.Action == "remove") return current;
        if (current is null || index < 0 || index >= current.Items.Count || edit.Action is not ("set" or "remove" or "combine" or "keepSeparate"))
            throw new ValidationException("Choose an item from the current draft.");
        var items = current.Items.ToList();
        if (edit.Action is "combine" or "keepSeparate")
        {
            var group = items.Where(item => SourceKind(item) == SourceKind(items[index]) && item.SourceItemId.HasValue && item.SourceItemId == items[index].SourceItemId).ToArray();
            if (group.Length < 2) throw new ValidationException("These entries are no longer a duplicate group. Refresh your meal.");
            if (edit.Action == "keepSeparate") return WithIdentities(current with {
                SeparateItemIds = (current.SeparateItemIds ?? []).Concat(group.Select(item => item.DraftItemId!.Value)).Distinct().ToArray(), EditFeedback = null });
            if (!CanCombine(group)) throw new ValidationException("These quantities or carbohydrate values are incompatible. Keep the entries separate.");
            await PrepareAsync(group, token, current.Sources);
            var combined = await SetQuantityAsync(group[0], group.Sum(item => item.Quantity), token);
            if (combined.EffectiveCarbs != group.Sum(item => item.EffectiveCarbs)) throw new ValidationException("Combining would change saved carbohydrate totals. Keep these entries separate.");
            foreach (var entry in group.Skip(1)) items.Remove(entry);
            items[items.IndexOf(group[0])] = combined;
        }
        // Removal only needs the persisted entry identity; a deleted/edited source must not block it.
        else if (edit.Action == "remove") items.RemoveAt(index);
        else
        {
            if (edit.Quantity is null or <= 0 or > 10000) throw new ValidationException("Specify a positive quantity up to 10,000.");
            await PrepareAsync([items[index]], token, current.Sources);
            items[index] = await SetQuantityAsync(items[index], edit.Quantity.Value, token);
        }
        return WithIdentities(current with { Items = items, TotalCarbs = Math.Round(items.Sum(item => item.EffectiveCarbs), 2), EditFeedback = null, PendingEdit = null,
            Sources = current.Sources?.Where(source => items.Any(item => item.FoodName == source.Name)).ToArray() });
    }
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
            if (item.CarbOverride.HasValue && sources?.Any(source => source.Kind == "Manual" && source.Name == item.FoodName) == true)
            { inputs.Add(ToInput(item)); continue; }
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
        var current = (await calculation.CalculateItemsAsync(inputs, null, null, cancellationToken)).Select(item =>
            item.FoodItemId == Guid.Empty && item.SourceMealItemId is null && item.SourceDeliveryMealId is null && item.SourceDeliveryMealItemId is null
                && item.CarbOverride.HasValue && sources?.Any(source => source.Kind == "Manual" && source.Name == item.FoodName) == true
                ? item with { Provenance = new(CarbReferenceSource.Manual, CarbTransformation.Calculated) } : item).ToArray();
        if (current.Where((item, index) => (item with { Provenance = null, SourceMealItemId = null, SourceDeliveryMealId = null, SourceDeliveryMealItemId = null })
            != (items[index] with { Provenance = null, SourceMealItemId = null, SourceDeliveryMealId = null, SourceDeliveryMealItemId = null, DraftItemId = null })).Any())
            throw new ValidationException("Saved food values changed. Ask for an updated meal draft.");
        return current.Select((item, index) => item with { DraftItemId = items[index].DraftItemId }).ToArray();
    }

    public async Task<AssistantDraft?> UpdateAsync(AssistantSearch search, AssistantDraft? current, CancellationToken token, Guid? operationId = null)
    {
        if (current is not null) current = WithIdentities(current);
        var op = search.Operation ?? "create";
        if (op == "batch")
        {
            if (search.Operations is not { Length: > 0 and <= 6 } operations || operations.Any(operation => operation.Operation is not ("remove" or "removeAll" or "set" or "decrease")))
                throw new ValidationException("Send supported independent meal edits.");
            var updated = current;
            foreach (var operation in operations)
            {
                updated = await UpdateAsync(operation, updated, token, operationId);
                if (updated?.EditFeedback is not null) return (current ?? new([], [], 0m)) with { EditFeedback = updated.EditFeedback };
            }
            return updated;
        }
        // A provider's generic create is not permission to erase a conversation's existing meal.
        if (op == "create" && current is null) return await BuildAsync(search.Foods, token, operationId);
        if (op == "create") op = "add";
        if (op == "replaceMeal") return await BuildAsync(search.Foods, token, operationId);
        if (op == "clear") return new AssistantDraft([], [], 0m);
        if (op == "show") return current;

        var items = current?.Items.ToList() ?? [];
        var sources = current?.Sources?.ToList() ?? [];
        IReadOnlyList<AssistantUnresolvedItem> additionUnresolved = [];
        if (op == "clarify" && current?.PendingEdit is { } edit)
        {
            if (search.Foods.Length != 1) return current;
            return await UpdateAsync(edit with { Target = search.Foods[0].Name }, current, token, operationId);
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
        if (quantityEdit && portion?.Unit is { } requestedUnit && matches.Length > 1)
            matches = matches.Where(item => item.MeasurementType == requestedUnit).ToArray();
        var requestedSource = portion?.DeliveryMealItemId ?? portion?.DeliveryMealId ?? portion?.FoodItemId;
        if (quantityEdit && requestedSource.HasValue)
            matches = matches.Where(item => item.SourceItemId == requestedSource).ToArray();
        if (op is "add" or "replace" or "clarify")
        {
            if (op == "clarify" && current?.Unresolved?.Count > 0)
            {
                if (current.Unresolved.Count != 1) return Feedback("Choose the unresolved item to clarify.");
                var food = search.Foods.SingleOrDefault();
                if (food is null) return current;
                var id = food.FoodItemId ?? food.DeliveryMealItemId ?? food.DeliveryMealId;
                return await ResolveAsync(current, new(current.Unresolved[0].Id, id.HasValue ? "select" : "search", id,
                    food.DeliveryMealItemId.HasValue ? "DeliveryMealItem" : food.DeliveryMealId.HasValue ? "DeliveryMeal" : "Food",
                    food.Quantity, food.Unit, SearchName: id.HasValue ? null : food.Name), token, operationId);
            }
            if (op == "clarify" && current?.Issues.Count > 1)
                return Feedback("Several foods still need clarification. Resend the complete meal with the intended foods and quantities.");
            if (op == "replace" && matches.Length != 1) return Feedback("Which item should I replace?", search);
            if (op == "clarify" && current?.Pending is null) return current;
            var queries = op == "clarify" ? search.Foods.Select(food => food with
            {
                Quantity = food.Quantity ?? current?.Pending?.Quantity,
                Unit = food.Unit ?? current?.Pending?.Unit,
                Restaurant = food.FoodItemId.HasValue ? null : food.Restaurant ?? current?.Pending?.Restaurant
            }).ToArray() : search.Foods;
            var addition = await BuildAsync(queries, token, operationId);
            if (addition is null) return current;
            additionUnresolved = addition.Unresolved ?? [];
            if (op == "replace" && addition.Issues.Count > 0) return Feedback(string.Join(" ", addition.Issues));
            if (op == "replace") items.Remove(matches[0]);
            items.AddRange(addition.Items);
            sources.AddRange(addition.Sources ?? []);
            return Finish(op is "add" or "replace" ? (current?.Issues ?? []).Concat(addition.Issues).ToArray() : addition.Issues,
                (op is "add" or "replace") && current?.Pending is not null ? current.Pending : addition.Pending);
        }

        if ((op is "remove" or "removeAll") && current?.Unresolved is { Count: > 0 } pendingItems)
        {
            var pendingScore = pendingItems.Select(item => score(item.Query.Name)).DefaultIfEmpty(0).Max();
            var pendingMatches = pendingItems.Where(item => pendingScore > 0 && score(item.Query.Name) == pendingScore).ToArray();
            if (pendingMatches.Length == 1 && (matches.Length == 0 || pendingScore > bestScore))
                return await ResolveAsync(current, new(pendingMatches[0].Id, "remove"), token);
            if (pendingMatches.Length > 0) return Feedback("Choose the exact pending or resolved entry to remove.");
        }
        if (matches.Length == 0) return op is "remove" or "removeAll" ? current : Feedback("That food is not in your draft. Ask to add it first.");
        if (matches.Length > 1 && (op is "increase" or "decrease") && matches.Skip(1).All(item => SameReference(matches[0], item)))
        {
            if (op == "increase") matches = [matches[0]]; // Preserve separate entries; add to one compatible entry.
            else if (portion?.Quantity is > 0 && portion.Quantity <= matches.Sum(item => item.Quantity))
            {
                var left = portion.Quantity.Value;
                foreach (var entry in matches)
                {
                    if (left == 0) break;
                    if (left >= entry.Quantity) { left -= entry.Quantity; items.Remove(entry); }
                    else { await PrepareAsync([entry], token, current?.Sources); items[items.IndexOf(entry)] = await SetQuantityAsync(entry, entry.Quantity - left, token); left = 0; }
                }
                return Finish(current?.Issues ?? [], current?.Pending);
            }
        }
        if (matches.Length > 1 && (op is "set" or "remove" or "removeAll") && matches.Skip(1).All(item => SameReference(matches[0], item)))
        {
            // An explicit final quantity reconciles this logical source; never sum old duplicates.
            foreach (var duplicate in matches.Skip(1)) items.Remove(duplicate);
            matches = [matches[0]];
        }
        if (matches.Length > 1)
            return Feedback("Which item should I change: " + await DescribeMatchesAsync(matches, token) + "?",
                quantityEdit ? search : null);
        var selected = matches[0];
        if (op is "remove" or "removeAll")
        {
            items.Remove(selected);
            return Finish(current?.Issues ?? [], current?.Pending);
        }
        if (portion?.Quantity is not > 0 || (portion.Unit is { } unit && unit != selected.MeasurementType))
            return Feedback("Specify a positive quantity in the saved unit.");
        if (portion.Unit is null && selected.MeasurementType == FoodMeasurementType.Grams)
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
                (selected.MeasurementType == FoodMeasurementType.Piece && decimal.Truncate(quantity) != quantity))
                return Feedback("That quantity is incompatible with the saved item.");
            await PrepareAsync([selected], token, current?.Sources);
            var updated = await SetQuantityAsync(selected, quantity, token);
            if (selected.SourceDeliveryMealItemId.HasValue && (updated.FoodName != selected.FoodName || updated.MeasurementType != selected.MeasurementType
                || updated.CarbsPer100g != selected.CarbsPer100g || updated.CarbsPerUnit != selected.CarbsPerUnit))
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
            WithIdentities(new(items, issues, Math.Round(items.Sum(item => item.EffectiveCarbs), 2),
                sources.Where(source => items.Any(item => item.FoodName == source.Name)).Distinct().ToArray(),
                issues.Count == 1 ? pending : null, Unresolved: op is "add" or "replace" or "clarify"
                    ? (current?.Unresolved ?? []).Concat(additionUnresolved).ToArray() : current?.Unresolved, SeparateItemIds: current?.SeparateItemIds));
    }

    private async Task<string> DescribeMatchesAsync(CalculatedMealItemDto[] items, CancellationToken token)
    {
        var ids = items.Where(item => item.SourceDeliveryMealItemId.HasValue).Select(item => item.SourceDeliveryMealItemId!.Value).ToArray();
        var owner = currentUser.UserId;
        var places = ids.Length == 0 ? new Dictionary<Guid, string>() : await db.DeliveryMealItems.AsNoTracking()
            .Where(item => ids.Contains(item.Id) && item.DeliveryMeal!.UserId == owner)
            .Select(item => new { item.Id, Place = item.DeliveryMeal!.PlaceName + " · " + item.DeliveryMeal.DishName })
            .ToDictionaryAsync(item => item.Id, item => item.Place, token);
        return string.Join(", ", items.Select((item, index) => $"Option {index + 1}: {item.FoodName} · {item.Quantity} {item.MeasurementType} · {item.EffectiveCarbs} g carbs · source " +
            (item.SourceDeliveryMealItemId is Guid id && places.TryGetValue(id, out var place) ? place : SourceKind(item) == "Food" ? "Saved food" : "Saved historical meal") +
            $" · {item.Provenance?.ReferenceSource}"));
    }

    private static MealItemInputDto ToInput(CalculatedMealItemDto item) =>
        new(item.FoodItemId, item.Quantity, MeasurementType: item.MeasurementType, FoodNameSnapshot: item.FoodName,
            CarbsPer100gSnapshot: item.CarbsPer100g, CarbsPerUnitSnapshot: item.CarbsPerUnit, CarbOverride: item.CarbOverride,
            SourceMealItemId: item.SourceMealItemId, SourceDeliveryMealId: item.SourceDeliveryMealId, SourceDeliveryMealItemId: item.SourceDeliveryMealItemId);

    public async Task<AssistantDraft?> BuildAsync(AssistantFoodQuery[] queries, CancellationToken cancellationToken, Guid? operationId = null)
    {
        if (queries.Length == 0) return null;
        var userId = currentUser.UserId;
        var inputs = new List<MealItemInputDto>();
        var issues = new List<string>();
        var sources = new List<AssistantDraftSource>();
        var unresolved = new List<AssistantUnresolvedItem>();
        AssistantFoodQuery? pending = null;
        var originOperation = operationId ?? Guid.NewGuid();
        for (var queryIndex = 0; queryIndex < queries.Length; queryIndex++)
        {
            var original = queries[queryIndex];
            var requestedItem = new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{originOperation}/{queryIndex}"))[..16]);
            var started = Stopwatch.GetTimestamp();
            var query = AssistantFoodMatching.Query(original);
            static string Short(string value) => value[..Math.Min(value.Length, 80)];
            logger?.LogDebug("Assistant extraction {Name}, quantity {Quantity}, unit {Unit}, normalized {Normalized}, aliases {Aliases}, place {Place}",
                Short(original.Name), original.Quantity, original.Unit, Short(AssistantFoodMatching.Normalize(query.Name)), AssistantFoodMatching.Terms(query.Name).Take(12).Select(Short).ToArray(), query.Restaurant is null ? null : Short(query.Restaurant));
            var ownedItems = db.DeliveryMealItems.AsNoTracking().Include(item => item.DeliveryMeal).Where(item => item.DeliveryMeal!.UserId == userId);
            var candidates = query.DeliveryMealItemId is Guid itemId
                ? await ownedItems.Where(item => item.Id == itemId).ToListAsync(cancellationToken)
                : query.FoodItemId.HasValue || query.DeliveryMealId.HasValue ? []
                : await FoodKnowledgeSearchService.FindItems(ownedItems, query, false).Take(2).ToListAsync(cancellationToken);
            if (query.DeliveryMealItemId.HasValue && candidates.Count == 0)
                throw new ValidationException("The selected order item is unavailable.");
            if (candidates.Count > 0)
            {
                var scoreItem = AssistantFoodMatching.Score(query.Name, itemName: true).Compile();
                if (!query.DeliveryMealItemId.HasValue) candidates = candidates.Where(item => scoreItem(item.Name) == scoreItem(candidates[0].Name)).ToList();
                var competingFoods = !query.DeliveryMealItemId.HasValue && string.IsNullOrWhiteSpace(query.Restaurant)
                    ? await FoodKnowledgeSearchService.FindFoods(db.FoodItems.AsNoTracking().Where(food => food.UserId == userId), query.Name, false).Take(2).ToListAsync(cancellationToken) : [];
                if (candidates.Count != 1 || competingFoods.Count > 0)
                { Reject(query, AssistantResolutionReason.Ambiguous, $"More than one saved item matched {query.Name}. Choose a specific saved food or order item.",
                    candidates.Select(item => new AssistantCandidate(item.Id, AssistantCandidateKind.DeliveryMealItem, item.Name, scoreItem(item.Name), item.DeliveryMeal!.PlaceName + " · " + item.DeliveryMeal.DishName))
                        .Concat(competingFoods.Select(food => new AssistantCandidate(food.Id, AssistantCandidateKind.Food, food.Name, AssistantFoodMatching.Score(query.Name).Compile()(food.Name), FoodContext(food)))).ToArray()); continue; }
                var candidate = candidates[0];
                if (query.Unit.HasValue && query.Unit != candidate.MeasurementType)
                { Reject(query, AssistantResolutionReason.UnitMismatch, $"Specify a quantity in {candidate.MeasurementType} for {candidate.Name}.", [new(candidate.Id, AssistantCandidateKind.DeliveryMealItem, candidate.Name, scoreItem(candidate.Name))]); continue; }
                inputs.Add(new(Guid.Empty, query.Quantity ?? candidate.Quantity, SourceDeliveryMealItemId: candidate.Id));
                sources.Add(new(candidate.Id, "AskPastMeItem", candidate.Name, query.Unit is null && query.Quantity.HasValue || query.UnitInferred));
                Trace(query, "Resolved", AssistantCandidateKind.DeliveryMealItem, candidate.Id, candidates.Count, scoreItem(candidate.Name), started);
                continue;
            }
            if (query.DeliveryMealId is Guid wholeId)
            {
                if (query.Quantity is not (null or 1m) || query.Unit is not (null or FoodMeasurementType.Portion or FoodMeasurementType.Piece))
                    throw new ValidationException("Select a complete saved order once, then edit its individual item quantities.");
                var whole = await db.DeliveryMeals.AsNoTracking().Include(order => order.Items).SingleOrDefaultAsync(order => order.Id == wholeId && order.UserId == userId, cancellationToken)
                    ?? throw new ValidationException("The saved order is unavailable.");
                AddOrder(whole); Trace(query, "Resolved", AssistantCandidateKind.DeliveryMeal, whole.Id, 1, 4, started); continue;
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
                if (orders.Count == 0 && query.Name.Length > 0)
                {
                    Reject(query, AssistantResolutionReason.NoMatches, $"No saved item matched {query.Name} from {query.Restaurant}.", []);
                    continue;
                }
                if (orders.Count != 1 || query.Quantity is not (null or 1m) ||
                    query.Unit is not (null or FoodMeasurementType.Piece or FoodMeasurementType.Portion) || orders[0].Carbs < 0)
                {
                    Reject(query, AssistantResolutionReason.OrderUnavailable, $"Choose one complete saved order for {query.Name} from {query.Restaurant}.",
                        orders.Select(order => new AssistantCandidate(order.Id, AssistantCandidateKind.DeliveryMeal, order.DishName, 3)).ToArray());
                    continue;
                }
                var order = orders[0];
                AddOrder(order);
                Trace(query, "Resolved", AssistantCandidateKind.DeliveryMeal, order.Id, orders.Count, 3, started);
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
                var choices = foods.Select(food => new AssistantCandidate(food.Id, AssistantCandidateKind.Food, food.Name, AssistantFoodMatching.Score(query.Name).Compile()(food.Name), FoodContext(food))).ToList();
                if (foods.Count == 0)
                {
                    var nearFoods = await FoodKnowledgeSearchService.FindFoods(owned, query.Name, true).Take(4).ToListAsync(cancellationToken);
                    var nearItems = await FoodKnowledgeSearchService.FindItems(ownedItems, query, true).Take(4).ToListAsync(cancellationToken);
                    var rank = AssistantFoodMatching.Score(query.Name, true).Compile();
                    choices.AddRange(nearFoods.Select(food => new AssistantCandidate(food.Id, AssistantCandidateKind.Food, food.Name, rank(food.Name), FoodContext(food))));
                    var itemRank = AssistantFoodMatching.Score(query.Name, true, itemName: true).Compile();
                    choices.AddRange(nearItems.Select(item => new AssistantCandidate(item.Id, AssistantCandidateKind.DeliveryMealItem, item.Name, itemRank(item.Name), item.DeliveryMeal!.PlaceName + " · " + item.DeliveryMeal.DishName)));
                }
                Reject(query, choices.Count == 0 ? AssistantResolutionReason.NoMatches : AssistantResolutionReason.Ambiguous,
                    choices.Count == 0 ? $"No saved food matched {query.Name}." : $"Choose a saved source for {query.Name}.", choices);
                continue;
            }

            var food = foods[0];
            var quantity = query.Quantity ?? (food.MeasurementType == FoodMeasurementType.Grams ? null : 1m);
            if ((query.Unit.HasValue && query.Unit != food.MeasurementType) || quantity is null or < 0.1m
                || (food.MeasurementType == FoodMeasurementType.Piece && decimal.Truncate(quantity.Value) != quantity.Value))
            {
                Reject(query, quantity is null ? AssistantResolutionReason.QuantityRequired : AssistantResolutionReason.UnitMismatch,
                    $"Specify a compatible quantity for {query.Name} ({food.MeasurementType}).", [new(food.Id, AssistantCandidateKind.Food, food.Name, 3)]);
                continue;
            }

            if ((food.MeasurementType == FoodMeasurementType.Grams ? food.CarbsPer100g : food.CarbsPerUnit) is null)
            {
                Reject(query, AssistantResolutionReason.MissingCarbs, $"Saved carbohydrate values are unavailable for {query.Name}.", [new(food.Id, AssistantCandidateKind.Food, food.Name, 3)]);
                continue;
            }

            inputs.Add(new MealItemInputDto(food.Id, quantity, null, null));
            if (query.Quantity.HasValue && query.Unit is null || query.UnitInferred) sources.Add(new(food.Id, "Food", food.Name, true));
            Trace(query, "Resolved", AssistantCandidateKind.Food, food.Id, foods.Count, AssistantFoodMatching.Score(query.Name).Compile()(food.Name), started);

            void Reject(AssistantFoodQuery failed, AssistantResolutionReason reason, string detail, IReadOnlyList<AssistantCandidate> choices)
            {
                // No nutrient estimate is made for an unresolved item. A unitless large amount is
                // only a clearly labeled suggestion, editable before resolving it.
                if (failed.Quantity >= 10m && failed.Unit is null) failed = failed with { Unit = FoodMeasurementType.Grams, UnitInferred = true };
                pending = failed; issues.Add(detail);
                unresolved.Add(new(requestedItem, original.OriginalText ?? original.Name, failed, reason, detail, choices, originOperation, requestedItem));
                logger?.LogInformation("Assistant alternatives: reason {AmbiguityReason}, IDs {CandidateIds}, kinds {CandidateKinds}, scores {CandidateScores}",
                    reason, choices.Select(choice => choice.Id).ToArray(), choices.Select(choice => choice.Kind).ToArray(), choices.Select(choice => choice.Score).ToArray());
                Trace(failed, reason.ToString(), null, null, choices.Count, choices.Select(choice => choice.Score).DefaultIfEmpty(0).Max(), started);
            }
        }

        var items = await calculation
            .CalculateItemsAsync(inputs, null, null, cancellationToken);
        return WithIdentities(new AssistantDraft(items.Select(item => item with { DraftItemId = Guid.NewGuid() }).ToArray(), issues, Math.Round(items.Sum(item => item.EffectiveCarbs), 2), sources,
            issues.Count == 1 ? pending : null, Unresolved: unresolved));

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
