using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Text;
using InsulinAndCoffee.Application.Dtos;

namespace InsulinAndCoffee.Application.Services;

public static class AssistantFoodMatching
{
    private static readonly string[][] FoodAliases =
    [
        ["sushi", "суши", "суші"],
        ["roll", "rolls", "ролы", "роллы", "роли", "роли́"],
        ["shawarma", "шаурма", "шаурму", "шаурмы", "шаверма", "шаверму"],
        ["borscht", "борщ", "борща", "борщу"],
        ["porridge", "каша", "каши", "каші"],
        ["buckwheat", "buckwheat porridge", "гречка", "гречки", "гречку", "гречневая каша", "гречневой каши", "гречневую кашу", "гречана каша", "гречаної каші", "гречану кашу"],
        ["bread", "хлеб", "хлеба", "хліб", "хліба"],
        ["pizza", "пицца", "піца", "пиццу", "піцу", "пиццы", "піци"],
        ["potatoes", "potato", "картошка", "картошки", "картошку", "картопля", "картоплі", "картоплю"],
        ["california", "калифорния", "каліфорнія", "california roll", "california sushi", "калифорнию", "каліфорнію", "калифорнии", "каліфорнії"],
        ["chicken", "курка", "курятина", "курица"],
        ["rice", "рис"],
        ["fried potatoes", "смажена картопля", "смаженої картоплі", "жареная картошка", "жареной картошки", "жареную картошку"],
        ["wheat porridge", "пшенична каша", "пшеничная каша"],
        ["potato pie", "пиріжок з картоплею", "пирожок с картошкой"]
    ];
    private static readonly string[][] RestaurantAliases =
    [
        ["le grill", "le gril", "ле гриль", "ле грілль"],
        ["huli huli", "хули хули", "хулі хулі"]
    ];

    // Infrastructure translates this pure function to PostgreSQL built-ins; no database function is installed.
    public const string Separators = @"[\s \-–—·,.?!]+";
    private static readonly Expression<Func<string, string>> Normalizer = name => Normalize(name);

    public static string Normalize(string name) => Regex.Replace(name.Normalize(NormalizationForm.FormKC).ToLowerInvariant()
        .Replace("[meal assistant local fixture v1] ", "").Replace("’", "'"), Separators, " ").Trim();
    internal static string[] Terms(string name)
    {
        var normalized = Normalize(name);
        var exact = FoodAliases.Concat(RestaurantAliases).FirstOrDefault(group => group.Contains(normalized));
        if (exact is not null) return exact;
        // A category prefix is not a restaurant: preserve exact ranking, also match a known dish alias.
        var dish = Regex.Replace(normalized, @"^(?:rolls?|роллы?|суши|суші|sushi)\s+", "");
        var family = FoodAliases.FirstOrDefault(group => group.Contains(dish));
        return dish != normalized && family is not null ? new[] { normalized }.Concat(family).Distinct().ToArray() : [normalized];
    }

    internal static AssistantFoodQuery Query(AssistantFoodQuery query)
    {
        query = AssistantPortionText.Normalize(query);
        if (!string.IsNullOrWhiteSpace(query.Restaurant)) return query;
        var name = Normalize(FoodKnowledgeSearchService.LookupName(query.Name));
        foreach (var restaurant in RestaurantAliases.SelectMany(group => group).OrderByDescending(name => name.Length))
        {
            if (name == restaurant) return query with { Name = "", Restaurant = restaurant };
            if (!name.EndsWith(" " + restaurant, StringComparison.Ordinal)) continue;
            var dish = name[..^(restaurant.Length + 1)].Trim();
            foreach (var connector in new[] { " from", " из", " з" })
                if (dish.EndsWith(connector, StringComparison.Ordinal)) dish = dish[..^connector.Length].Trim();
            return query with { Name = dish, Restaurant = restaurant };
        }
        return query;
    }

    internal static Expression<Func<string, int>> Score(string query, bool discovery = false, bool itemName = false)
    {
        var name = Normalize(query);
        var aliases = Terms(name);
        if (itemName)
        {
            var dish = Regex.Replace(name, @"^(?:rolls?|роллы?|суши|суші|sushi)\s+", "");
            if (dish != name) aliases = aliases.Concat(Terms(dish)).Distinct().ToArray();
        }
        var input = Normalizer.Parameters[0];
        var normalized = Normalizer.Body;
        Expression exact = Expression.Equal(normalized, Expression.Constant(name));
        Expression alias = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(string)],
            Expression.Constant(aliases), normalized);
        Expression qualified = Expression.Constant(false);
        Expression partial = Expression.Constant(false);
        var padded = Expression.Call(typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string), typeof(string)])
            ?? throw new InvalidOperationException("Food phrase matching is unavailable."), Expression.Constant(" "), normalized, Expression.Constant(" "));
        foreach (var term in aliases.Where(term => term.Length > 0))
        {
            // Discovery/history accept whole phrases; existing draft targets accept configured food categories.
            // Neither permits a weak phrase to resolve a new source (which requires score >= 3).
            if (discovery || FoodAliases.Any(group => group.Contains(name)))
                qualified = Expression.OrElse(qualified, Expression.Call(padded, nameof(string.Contains), null, Expression.Constant(" " + term + " ")));
            partial = Expression.OrElse(partial, Expression.Call(normalized, nameof(string.Contains), null, Expression.Constant(term)));
        }
        return Expression.Lambda<Func<string, int>>(
            Expression.Condition(exact, Expression.Constant(4),
                Expression.Condition(alias, Expression.Constant(3),
                    Expression.Condition(qualified, Expression.Constant(2),
                        Expression.Condition(discovery ? partial : Expression.Constant(false), Expression.Constant(1), Expression.Constant(0))))), input);
    }

    internal static Expression<Func<T, int>> On<T>(Expression<Func<T, string>> name, Expression<Func<string, int>> score) =>
        Expression.Lambda<Func<T, int>>(new Replace(score.Parameters[0], name.Body).Visit(score.Body)
            ?? throw new InvalidOperationException("The food matching expression is unavailable."), name.Parameters);

    internal static Expression<Func<T, bool>> Positive<T>(Expression<Func<T, int>> score, int minimum = 1) =>
        Expression.Lambda<Func<T, bool>>(Expression.GreaterThanOrEqual(score.Body, Expression.Constant(minimum)), score.Parameters);

    private sealed class Replace(Expression source, Expression target) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node) => node == source ? target : base.Visit(node);
    }
}
