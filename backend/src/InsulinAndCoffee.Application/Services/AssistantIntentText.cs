using System.Globalization;
using System.Text.RegularExpressions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Application.Services;

internal static class AssistantIntentText
{
    // Conservative fast paths. Quantity edits, follow-ups and ambiguous grammar still use the existing interpreter.
    internal static AssistantSearch? Interpret(string message, bool hasDraft)
    {
        var text = message.Trim().TrimEnd('.', '?', '!');
        var extra = Regex.Match(text, @"^(?:добавь\s+ещ[её]|додай\s+ще|add\s+(?:another|more))\s+(.+)$", RegexOptions.IgnoreCase);
        if (hasDraft && extra.Success)
        {
            var food = Parse(extra.Groups[1].Value);
            return new([food with { Quantity = food.Quantity ?? 1m }], "draft", "increase", food.Name);
        }
        var want = Regex.Match(text, @"^(?:хочу|I want)\s+(.+)$", RegexOptions.IgnoreCase);
        if (want.Success && Parse(want.Groups[1].Value) is { Quantity: not null } requested)
            return new([requested], "draft", hasDraft ? "add" : "create");
        if (hasDraft)
        {
            var clauses = Regex.Split(text, @"\s+(?:и|та|and)\s+|;\s*", RegexOptions.IgnoreCase);
            var operations = clauses.Select(Edit).ToArray();
            if (operations.FirstOrDefault()?.Operation == "remove")
                for (var index = 1; index < operations.Length; index++)
                    if (operations[index] is null && FoodKnowledgeSearchService.IsKnownTerm(clauses[index]))
                        operations[index] = new([], "draft", "remove", clauses[index]);
            if (operations.Length > 1 && operations.Length <= 6 && operations.All(operation => operation is not null))
                return new([], "draft", "batch", Operations: operations.Select(operation => operation!).ToArray());
            var operation = Edit(text);
            if (operation is not null) return operation;
        }
        var add = Regex.Match(text, @"^(?:добавь|додай|add|хочу\s+(?:съесть|поесть)|хочу\s+з['’]?їсти|I\s+(?:want\s+to\s+eat|would\s+like\s+to\s+eat))\s+(.+)$", RegexOptions.IgnoreCase);
        if (add.Success && !Regex.IsMatch(add.Groups[1].Value, @"\b(?:ещ[её]|ще|more|another)\b", RegexOptions.IgnoreCase))
        {
            var parts = Regex.Split(add.Groups[1].Value, @"\s+(?:и|та|and)\s+|,(?!\d)\s*", RegexOptions.IgnoreCase);
            if (parts.Length is < 1 or > 6) return null;
            return new(parts.Select(Parse).ToArray(), "draft", hasDraft ? "add" : "create");
        }
        var portion = Parse(text);
        if (portion.Quantity.HasValue && FoodKnowledgeSearchService.IsKnownTerm(portion.Name))
            return new([portion], "draft", hasDraft ? "add" : "create");
        var lookup = Regex.Match(text, @"^(?:сколько\s+(?:(?:углеводов\s+)?я\s+)?(?:брала|брал|использовала|использовал|считала|считал)\s+(?:на|для)|сколько\s+углеводов\s+(?:в|для)|скільки\s+вуглеводів\s+(?:у|в|для)|how many carbs (?:did I (?:use|count) for|do I usually count for|in)|найди|знайди|find)\s+(?:(?:моей|моём|моем|my|моїй)\s+)?(.+)$", RegexOptions.IgnoreCase);
        if (lookup.Success && !Regex.IsMatch(text, @"\b(previous|last|eaten|ate|предыдущ\w*|ела|ел|їв|їла)\b", RegexOptions.IgnoreCase))
            return new([new(lookup.Groups[1].Value, null, null)], "lookup");
        return null;
    }

    private static AssistantSearch? Edit(string text)
    {
        if (Regex.IsMatch(text, @"^(?:clear (?:my |the )?meal|очисти(?: весь)? при[её]м пищи|удали все|прибери все)$", RegexOptions.IgnoreCase))
            return new([], "draft", "clear");
        var remove = Regex.Match(text, @"^(?:убери|удали|прибери|видали|remove|delete)\s+(.+)$", RegexOptions.IgnoreCase);
        if (remove.Success)
        {
            var target = remove.Groups[1].Value.Trim();
            var all = Regex.Match(target, @"^(?:все|всю|усі|всі|all)\s+(.+)$", RegexOptions.IgnoreCase);
            if (all.Success) return new([], "draft", "removeAll", all.Groups[1].Value.Trim());
            var amount = Parse(target);
            return amount.Quantity.HasValue ? new([amount], "draft", "decrease", amount.Name)
                : new([], "draft", "remove", target);
        }
        var set = Regex.Match(text, @"^(?<name>.+?)\s+(?:оставь|залиш|set\s+to)\s+(?<portion>.+)$", RegexOptions.IgnoreCase);
        if (!set.Success) set = Regex.Match(text, @"^(?:оставь|залиш)\s+(?<name>.+?)\s+(?<portion>(?:одну|одна|один|две|два|три|one|two|\d+(?:[.,]\d+)?)\s+\S+)$", RegexOptions.IgnoreCase);
        if (set.Success)
        {
            var name = set.Groups["name"].Value.Trim();
            var portion = Parse(set.Groups["portion"].Value + " " + name);
            if (portion.Quantity.HasValue && portion.Unit.HasValue) return new([portion], "draft", "set", name);
        }
        return null;
    }

    private static AssistantFoodQuery Parse(string text)
    {
        var words = Regex.Match(text.Trim(), @"^(one|two|three|один|одна|одну|два|две|дві|три)\s+(.+)$", RegexOptions.IgnoreCase);
        if (words.Success)
        {
            var normalized = AssistantPortionText.Normalize(new(text.Trim(), null, null));
            if (normalized.Quantity.HasValue) return normalized;
            var wordQuantity = words.Groups[1].Value.ToLowerInvariant() is "one" or "один" or "одна" or "одну" ? 1m
                : words.Groups[1].Value.ToLowerInvariant() is "three" or "три" ? 3m : 2m;
            return new(words.Groups[2].Value.Trim(), wordQuantity, null, OriginalText: text.Trim());
        }
        var match = Regex.Match(text.Trim(), @"^(?<quantity>\d+(?:[.,]\d+)?)\s*(?:(?<unit>г|гр|g|grams?|грамм(?:а|ов)?|грамів|порции|порцій|servings?|pieces?)\s+)?(?<name>.+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return AssistantPortionText.Normalize(new(text.Trim(), null, null, OriginalText: text.Trim()));
        var quantity = decimal.Parse(match.Groups["quantity"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        FoodMeasurementType? type = unit.Length == 0 ? null : unit.StartsWith('г') || unit.StartsWith('g') ? FoodMeasurementType.Grams
            : unit.StartsWith("piece") ? FoodMeasurementType.Piece : FoodMeasurementType.Portion;
        return new(match.Groups["name"].Value.Trim(), quantity, type, OriginalText: text.Trim());
    }

    internal static AssistantSearch RemoveRepeatedExtraction(AssistantSearch search, string message)
    {
        // Only a model extraction of a single explicit mention can be corrected here.
        // Deterministic lists, selections, separate operations and explicit repeat/quantity cues remain intact.
        if (search.Intent != "draft" || search.Operation is not (null or "create" or "add") ||
            Regex.IsMatch(message, @"(?i)\b(?:и|та|and|ещ[её]|ще|another|more|two|three|два|две|дві|три|несколько)\b|[,;]|\d")) return search;
        var seen = new HashSet<string>();
        var foods = search.Foods.Where(food =>
        {
            var query = AssistantFoodMatching.Query(food);
            var aliases = AssistantFoodMatching.Terms(query.Name).Select(AssistantFoodMatching.Normalize).Distinct().OrderByDescending(alias => alias.Length).ToArray();
            var mentions = Regex.Matches(AssistantFoodMatching.Normalize(message), @"(?<!\p{L})(?:" + string.Join("|", aliases.Select(Regex.Escape)) + @")(?!\p{L})").Count;
            if (mentions != 1) return true;
            var key = System.Text.Json.JsonSerializer.Serialize(new { Names = aliases.Order().ToArray(), query.Quantity, query.Unit,
                Restaurant = AssistantFoodMatching.Normalize(query.Restaurant ?? ""), query.FoodItemId, query.DeliveryMealId, query.DeliveryMealItemId });
            return seen.Add(key);
        }).ToArray();
        return search with { Foods = foods };
    }
}
