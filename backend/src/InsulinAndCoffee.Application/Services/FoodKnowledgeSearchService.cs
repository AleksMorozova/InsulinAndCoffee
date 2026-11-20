using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class FoodKnowledgeSearchService(IAppDbContext db, ICurrentUser currentUser)
{
    private static readonly string[][] Aliases =
    [
        ["sushi", "суши", "суші"],
        ["roll", "rolls", "ролы", "роллы", "роли", "роли́"],
        ["shawarma", "шаурма", "шаурму", "шаверма", "шаверму"],
        ["borscht", "борщ", "борща", "борщу"],
        ["porridge", "каша", "каши", "каші"],
        ["bread", "хлеб", "хлеба", "хліб", "хліба"],
        ["pizza", "пицца", "піца", "пиццу", "піцу", "пиццы", "піци"],
        ["potatoes", "potato", "картошка", "картошки", "картошку", "картопля", "картоплі", "картоплю"],
        ["california", "калифорния", "каліфорнія", "california roll", "california sushi", "калифорнию", "каліфорнію"],
        ["le grill", "ле гриль", "ле грілль"],
        ["huli huli", "хули хули", "хулі хулі"]
    ];

    internal static string[] Terms(string name)
    {
        var normalized = string.Join(' ', name.Normalize(System.Text.NormalizationForm.FormKC).ToLowerInvariant()
            .Split([' ', '-', ',', '.', '?', '!', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return Aliases.FirstOrDefault(group => group.Contains(normalized)) ?? [normalized];
    }

    internal static string LookupName(string message)
    {
        var normalized = message.Trim().TrimEnd('.', '!', '?').TrimEnd();
        foreach (var suffix in new[] { " как обычно", " як завжди", " as usual" })
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return normalized[..^suffix.Length];
        return normalized;
    }

    internal static bool IsKnownTerm(string message) => Terms(message).Length > 1;

    private static IEnumerable<string> DiscoveryTerms(string name)
    {
        var terms = Terms(name);
        return terms[0] is "sushi" or "roll" ? terms.Concat(Terms(terms[0] == "sushi" ? "roll" : "sushi")) : terms;
    }

    public async Task<AssistantKnowledge> SearchAsync(AssistantFoodQuery[] foods, CancellationToken cancellationToken)
    {
        var owner = currentUser.UserId;
        var saved = new List<AssistantSavedFood>();
        var orders = new List<AssistantOrder>();
        foreach (var food in foods)
        {
            var restaurants = string.IsNullOrWhiteSpace(food.Restaurant) ? [] : Terms(food.Restaurant);
            foreach (var term in DiscoveryTerms(LookupName(food.Name)))
            {
                if (restaurants.Length == 0) saved.AddRange(await db.FoodItems.AsNoTracking()
                    .Where(item => item.UserId == owner && item.Name.ToLower().Contains(term))
                    .OrderByDescending(item => item.Name.ToLower() == term).ThenBy(item => item.Name).ThenBy(item => item.Id)
                    .Take(5).Select(item => new AssistantSavedFood(item.Id, item.Name, item.MeasurementType, item.CarbsPer100g, item.CarbsPerUnit))
                    .ToListAsync(cancellationToken));
                orders.AddRange(await db.DeliveryMeals.AsNoTracking()
                    .Where(item => item.UserId == owner && (restaurants.Length == 0 || restaurants.Contains(item.PlaceName.ToLower())) && (item.PlaceName.ToLower().Contains(term)
                        || item.DishName.ToLower().Contains(term) || item.PortionDescription.ToLower().Contains(term)
                        || item.Tags.ToLower().Contains(term) || (item.Notes != null && item.Notes.ToLower().Contains(term))))
                    .OrderByDescending(item => item.PlaceName.ToLower() == term || item.DishName.ToLower() == term)
                    .ThenByDescending(item => item.LastUsedAt).ThenBy(item => item.Id).Take(5)
                    .Select(item => new AssistantOrder(item.Id, item.PlaceName, item.DishName, item.PortionDescription, item.Carbs, item.LastUsedAt))
                    .ToListAsync(cancellationToken));
            }
        }
        return new(saved.DistinctBy(item => item.Id).Take(8).ToArray(), orders.DistinctBy(item => item.Id).Take(8).ToArray());
    }
}
