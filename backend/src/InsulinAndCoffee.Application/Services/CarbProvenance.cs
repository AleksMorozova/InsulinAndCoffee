using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;

namespace InsulinAndCoffee.Application.Services;

internal static class CarbProvenance
{
    internal static CarbProvenanceDto Of(MealItem item) => new(item.CarbReferenceSource, item.CarbTransformation, item.SourceMealVerifiedAt);
    internal static CarbProvenanceDto Of(DeliveryMeal order) => new(order.CarbReferenceSource, order.CarbTransformation, order.SourceMealVerifiedAt);
    internal static CarbProvenanceDto Of(Meal meal)
    {
        var sources = meal.Items.Select(item => item.CarbOverride.HasValue ? CarbReferenceSource.Manual : item.CarbReferenceSource).ToList();
        if (meal.CarbAdjustment != 0) sources.Add(CarbReferenceSource.Manual);
        var source = sources.Count == 0 || sources.Contains(CarbReferenceSource.Unknown) ? CarbReferenceSource.Unknown
            : sources.Distinct().Count() == 1 ? sources[0] : CarbReferenceSource.Mixed;
        var operations = meal.Items.Aggregate(CarbTransformation.None, (value, item) => value | item.CarbTransformation);
        if (meal.CarbAdjustment != 0) operations |= CarbTransformation.Calculated;
        return new(source, operations);
    }
}
