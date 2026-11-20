using System.Globalization;
using System.Text.RegularExpressions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Application.Services;

internal static partial class AssistantPortionText
{
    [GeneratedRegex(@"^(?:(?<amount>\d+(?:[.,]\d+)?|one|two|three|half|один|одна|одну|два|две|дві|три|половина)\s+)?(?<unit>grams?|г|грамм(?:а|ов)?|грамів|грам(?:и)?|pieces?|slices?|кусоч(?:ек|ка|ков|ком)|шмат(?:ок|ки|ків)|порц(?:ия|ии|ию|ій|ія|ії|ію)|servings?|portions?)(?:\s+(?:of\s+)?(?<name>.+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Prefix();

    [GeneratedRegex(@"^(?<name>.+?)\s+(?<amount>[+-]?\s*\d+(?:[.,]\d+)?)\s*(?<unit>g|grams?|г|грамм(?:а|ов)?|грамів|грам(?:и)?|pieces?|slices?|кусоч(?:ек|ка|ков|ком)|шмат(?:ок|ки|ків)|порц(?:ия|ии|ию|ій|ія|ії|ію)|servings?|portions?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Suffix();

    internal static AssistantFoodQuery Normalize(AssistantFoodQuery food)
    {
        if (food.FoodItemId.HasValue) return food;
        var match = Prefix().Match(food.Name.Trim());
        if (!match.Success) match = Suffix().Match(food.Name.Trim());
        if (!match.Success) return food;
        var text = match.Groups["unit"].Value.ToLowerInvariant();
        var unit = text == "g" || text.StartsWith("gram") || text.StartsWith('г') ? FoodMeasurementType.Grams
            : text.StartsWith("порц") || text.StartsWith("serving") || text.StartsWith("portion")
                ? FoodMeasurementType.Portion : FoodMeasurementType.Piece;
        var amount = string.Concat(match.Groups["amount"].Value.Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant();
        decimal? quantity = amount switch
        {
            "one" or "один" or "одна" or "одну" => 1m,
            "two" or "два" or "две" or "дві" => 2m,
            "three" or "три" => 3m,
            "half" or "половина" => 0.5m,
            "" when unit != FoodMeasurementType.Grams => 1m,
            _ => decimal.TryParse(amount.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var parsed) ? parsed : null
        };
        return food with
        {
            Name = match.Groups["name"].Success ? match.Groups["name"].Value.Trim() : food.Name,
            Quantity = food.Quantity ?? quantity,
            Unit = food.Unit ?? unit
        };
    }
}
