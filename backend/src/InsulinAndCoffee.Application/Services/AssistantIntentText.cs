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

    private static AssistantFoodQuery Parse(string text)
    {
        var match = Regex.Match(text.Trim(), @"^(?<quantity>\d+(?:[.,]\d+)?)\s*(?:(?<unit>г|гр|g|grams?|грамм(?:а|ов)?|грамів|порции|порцій|servings?|pieces?)\s+)?(?<name>.+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return AssistantPortionText.Normalize(new(text.Trim(), null, null, OriginalText: text.Trim()));
        var quantity = decimal.Parse(match.Groups["quantity"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        FoodMeasurementType? type = unit.Length == 0 ? null : unit.StartsWith('г') || unit.StartsWith('g') ? FoodMeasurementType.Grams
            : unit.StartsWith("piece") ? FoodMeasurementType.Piece : FoodMeasurementType.Portion;
        return new(match.Groups["name"].Value.Trim(), quantity, type, OriginalText: text.Trim());
    }
}
