using System.Linq.Expressions;
using System.Text;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Calculations;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class MealHistorySearchService(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<AssistantMeal>> SearchAsync(AssistantSearch search, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var meals = db.Meals.AsNoTracking().Where(meal => meal.UserId == userId);
        if (search.Foods.Length > 0)
        {
            var terms = search.Foods.SelectMany(food => Alternatives(food.Name)).SelectMany(Terms).Distinct().ToArray();
            meals = FilterCandidates(meals, terms);
        }

        var candidates = await meals.OrderByDescending(meal => meal.MealTime).ThenBy(meal => meal.Id)
            .Take(250).Include(meal => meal.Items).ToListAsync(cancellationToken);
        var ranked = candidates.Select(meal => (Meal: meal, Rank: Rank(meal, search.Foods)))
            .Where(candidate => candidate.Rank > 0)
            .OrderByDescending(candidate => candidate.Rank)
            .ThenByDescending(candidate => candidate.Meal.MealTime)
            .Take(5).ToArray();
        if (ranked.Length == 0) return [];

        var ids = ranked.Select(candidate => candidate.Meal.Id).ToArray();
        var readings = await db.GlucoseReadings.AsNoTracking()
            .Where(reading => reading.UserId == userId && reading.MealId.HasValue && ids.Contains(reading.MealId.Value))
            .OrderBy(reading => reading.ReadingTime)
            .Select(reading => new { MealId = reading.MealId!.Value,
                Reading = new AssistantGlucoseReading(reading.Value, reading.ReadingTime, reading.ReadingType) })
            .ToListAsync(cancellationToken);
        var byMeal = readings.ToLookup(reading => reading.MealId, reading => reading.Reading);
        return ranked.Select(candidate => Map(candidate.Meal, candidate.Rank, search.Foods,
            byMeal[candidate.Meal.Id].ToArray())).ToArray();
    }

    public static IQueryable<Meal> FilterCandidates(IQueryable<Meal> meals, string[] terms)
    {
        Expression<Func<Meal, bool>> predicate = meal => false;
        foreach (var term in terms)
        {
            Expression<Func<Meal, bool>> matches = meal =>
                meal.Items.Any(item => item.FoodNameSnapshot.ToLower().Contains(term));
            var body = new ReplaceParameter(matches.Parameters[0], predicate.Parameters[0]).Visit(matches.Body);
            predicate = Expression.Lambda<Func<Meal, bool>>(Expression.OrElse(predicate.Body, body), predicate.Parameters);
        }
        return meals.Where(predicate);
    }

    private sealed class ReplaceParameter(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? target : node;
    }

    private static int Rank(Meal meal, AssistantFoodQuery[] foods)
    {
        if (foods.Length == 0) return 1;
        var matched = foods.Count(food => meal.Items.Any(item => Matches(food.Name, item.FoodNameSnapshot)));
        if (matched == 0) return 0;
        if (matched < foods.Length) return 1;
        return meal.Items.Count == foods.Length && foods.All(food =>
            meal.Items.Any(item => Normalize(food.Name) == Normalize(item.FoodNameSnapshot))) ? 3 : 2;
    }

    private static AssistantMeal Map(Meal meal, int rank, AssistantFoodQuery[] foods,
        IReadOnlyList<AssistantGlucoseReading> readings) =>
        new(meal.Id, meal.MealTime, meal.MealType, meal.TotalCarbs, meal.SuggestedBolus, meal.ConfirmedBolus,
            meal.PreMealGlucose, foods.Length == 0 ? "Recent" : rank == 3 ? "Exact composition" : rank == 2 ? "Similar composition" : "Partial match",
            meal.Items.OrderBy(item => item.FoodNameSnapshot).Select(item =>
            {
                var requested = foods.FirstOrDefault(food => Matches(food.Name, item.FoodNameSnapshot));
                var comparable = requested?.Quantity is >= 0.1m && requested.Unit == item.MeasurementType
                    && (item.MeasurementType != FoodMeasurementType.Piece || decimal.Truncate(requested.Quantity.Value) == requested.Quantity.Value)
                    && Normalize(requested.Name) == Normalize(item.FoodNameSnapshot) && item.CarbOverride is null
                    && (item.MeasurementType == FoodMeasurementType.Grams ? item.CarbsPer100gSnapshot : item.CarbsPerUnitSnapshot) is >= 0;
                return new AssistantMealItem(item.FoodNameSnapshot, item.Quantity, item.MeasurementType,
                    item.CarbOverride ?? item.CalculatedCarbs, requested?.Quantity, requested?.Unit,
                    comparable ? FoodCarbCalculator.Calculate(item.MeasurementType, requested?.Quantity ?? 0,
                        item.CarbsPer100gSnapshot, item.CarbsPerUnitSnapshot) : null);
            }).ToArray(), readings, meal.Items.Count > 0);

    private static string Normalize(string value) => string.Join(' ', value.Normalize(NormalizationForm.FormKC)
        .ToLowerInvariant().Split([' ', '-', ',', '.', '’', '\''], StringSplitOptions.RemoveEmptyEntries));

    private static readonly string[][] Aliases =
    [
        ["chicken", "курка", "курятина", "курица"],
        ["rice", "рис"],
        ["fried potatoes", "смажена картопля", "жареная картошка"],
        ["wheat porridge", "пшенична каша", "пшеничная каша"],
        ["potato pie", "пиріжок з картоплею", "пирожок с картошкой"]
    ];

    private static IEnumerable<string> Alternatives(string name)
    {
        var normalized = Normalize(name);
        return Aliases.FirstOrDefault(group => group.Contains(normalized, StringComparer.Ordinal)) ?? FoodKnowledgeSearchService.Terms(normalized);
    }

    private static IEnumerable<string> Terms(string value) => Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);
    private static bool Matches(string food, string snapshot)
    {
        var name = Normalize(snapshot);
        var words = Terms(name).ToHashSet(StringComparer.Ordinal);
        return Alternatives(food).Any(alias => Terms(alias).All(words.Contains));
    }
}
