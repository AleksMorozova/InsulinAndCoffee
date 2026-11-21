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
            meals = meals.Where(AssistantFoodMatching.Positive(CandidatePriority(search.Foods)));
        }

        var priority = CandidatePriority(search.Foods);
        var candidates = await meals.OrderByDescending(priority)
            .ThenByDescending(meal => meal.MealTime).ThenBy(meal => meal.Id)
            .Take(250).Include(meal => meal.Items).ToListAsync(cancellationToken);
        var rank = priority.Compile();
        var ranked = candidates.Select(meal => (Meal: meal, Rank: rank(meal)))
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

    public static Expression<Func<Meal, int>> CandidatePriority(AssistantFoodQuery[] foods)
    {
        var meal = Expression.Parameter(typeof(Meal), "meal");
        Expression matched = Expression.Constant(0);
        Expression exact = Expression.Constant(true);
        var exactItem = Expression.Parameter(typeof(MealItem), "exactItem");
        Expression covered = Expression.Constant(false);
        foreach (var food in foods)
        {
            var query = AssistantFoodMatching.Query(food);
            var name = string.IsNullOrWhiteSpace(query.Restaurant) ? query.Name : query.Restaurant + " " + query.Name;
            var itemScore = AssistantFoodMatching.On<MealItem>(item => item.FoodNameSnapshot, AssistantFoodMatching.Score(name, true));
            Expression Has(int minimum) => Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), [typeof(MealItem)],
                Expression.Property(meal, nameof(Meal.Items)), AssistantFoodMatching.Positive(itemScore, minimum));
            matched = Expression.Add(matched, Expression.Condition(Has(2), Expression.Constant(1), Expression.Constant(0)));
            exact = Expression.AndAlso(exact, Has(4));
            var exactName = AssistantFoodMatching.Normalize(name);
            covered = Expression.OrElse(covered, Expression.Equal(
                Expression.Call(typeof(AssistantFoodMatching), nameof(AssistantFoodMatching.Normalize), null,
                    Expression.Property(exactItem, nameof(MealItem.FoodNameSnapshot))), Expression.Constant(exactName)));
        }
        exact = Expression.AndAlso(exact, Expression.Call(typeof(Enumerable), nameof(Enumerable.All), [typeof(MealItem)],
            Expression.Property(meal, nameof(Meal.Items)), Expression.Lambda<Func<MealItem, bool>>(covered, exactItem)));
        Expression priority = foods.Length == 0 ? Expression.Constant(1) : Expression.Condition(
            Expression.Equal(matched, Expression.Constant(foods.Length)),
            Expression.Condition(Expression.AndAlso(exact, Expression.Equal(
                Expression.Property(Expression.Property(meal, nameof(Meal.Items)), "Count"), Expression.Constant(foods.Length))),
                Expression.Constant(3), Expression.Constant(2)),
            Expression.Condition(Expression.GreaterThan(matched, Expression.Constant(0)), Expression.Constant(1), Expression.Constant(0)));
        return Expression.Lambda<Func<Meal, int>>(priority, meal);
    }

    public static IQueryable<Meal> FilterCandidates(IQueryable<Meal> meals, string[] terms)
    {
        Expression<Func<Meal, bool>> predicate = meal => false;
        foreach (var term in terms)
        {
            Expression<Func<Meal, bool>> matches = meal =>
                meal.Items.Any(item => AssistantFoodMatching.Normalize(item.FoodNameSnapshot).Contains(term));
            var body = new ReplaceParameter(matches.Parameters[0], predicate.Parameters[0]).Visit(matches.Body);
            predicate = Expression.Lambda<Func<Meal, bool>>(Expression.OrElse(predicate.Body, body), predicate.Parameters);
        }
        return meals.Where(predicate);
    }

    private sealed class ReplaceParameter(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? target : node;
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
                        item.CarbsPer100gSnapshot, item.CarbsPerUnitSnapshot) : null, CarbProvenance.Of(item));
            }).ToArray(), readings, meal.Items.Count > 0, meal.CarbsVerifiedAt);

    private static string Normalize(string value) => AssistantFoodMatching.Normalize(value);
    private static bool Matches(string food, string snapshot) => AssistantFoodMatching.Score(food, true).Compile()(snapshot) >= 2;
}
