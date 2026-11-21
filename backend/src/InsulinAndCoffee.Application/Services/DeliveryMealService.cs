using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Enums;
using InsulinAndCoffee.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public class DeliveryMealService(IAppDbContext db, TimeProvider timeProvider, ICurrentUser? currentUser = null, MealCalculationService? calculation = null)
{
    private Guid UserId => currentUser?.UserId ?? DefaultUser.Id;
    public async Task<DeliveryMealSectionsDto> GetSectionsAsync(string? search, CancellationToken cancellationToken)
    {
        var baseQuery = db.DeliveryMeals
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(k => k.UserId == UserId);

        var favorites = await baseQuery
            .Where(k => k.IsFavorite)
            .OrderByDescending(k => k.LastUsedAt ?? k.CreatedAt)
            .Take(8)
            .Select(k => ToDto(k))
            .ToListAsync(cancellationToken);

        var mostUsed = await baseQuery
            .Where(k => k.UsageCount > 0)
            .OrderByDescending(k => k.UsageCount)
            .ThenByDescending(k => k.LastUsedAt)
            .Take(8)
            .Select(k => ToDto(k))
            .ToListAsync(cancellationToken);

        var recentlyUsed = await baseQuery
            .Where(k => k.LastUsedAt != null)
            .OrderByDescending(k => k.LastUsedAt)
            .Take(8)
            .Select(k => ToDto(k))
            .ToListAsync(cancellationToken);

        var searchResultsQuery = baseQuery;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            searchResultsQuery = searchResultsQuery.Where(k =>
                k.PlaceName.ToLower().Contains(term) ||
                k.DishName.ToLower().Contains(term) ||
                k.Tags.ToLower().Contains(term) || k.Items.Any(item => item.Name.ToLower().Contains(term)));
        }

        var searchResults = await searchResultsQuery
            .OrderByDescending(k => k.IsFavorite)
            .ThenByDescending(k => k.UsageCount)
            .ThenBy(k => k.PlaceName)
            .ThenBy(k => k.DishName)
            .Take(40)
            .Select(k => ToDto(k))
            .ToListAsync(cancellationToken);

        return new DeliveryMealSectionsDto(favorites, mostUsed, recentlyUsed, searchResults);
    }

    public async Task<DeliveryMealDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var deliveryMeal = await db.DeliveryMeals
            .AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(k => k.Id == id && k.UserId == UserId, cancellationToken)
            ?? throw new NotFoundException("Delivery meal", id);

        return ToDto(deliveryMeal);
    }

    public async Task<DeliveryMealDto> CreateAsync(UpsertDeliveryMealRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var now = timeProvider.GetUtcNow();

        var deliveryMeal = new DeliveryMeal
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            PlaceName = request.PlaceName.Trim(),
            DishName = request.DishName.Trim(),
            PortionDescription = NormalizePortionDescription(request.PortionDescription),
            Carbs = request.Carbs,
            UsualInsulinUnits = request.UsualInsulinUnits,
            LastPreMealGlucose = request.LastPreMealGlucose,
            ResultRating = request.ResultRating,
            Tags = NormalizeTags(request.Tags),
            Notes = request.Notes,
            IsFavorite = request.IsFavorite,
            UsageCount = 0,
            CreatedAt = now,
            CarbReferenceSource = CarbReferenceSource.Manual
        };

        if (request.Items is { Count: > 0 })
            await SetItemsAsync(deliveryMeal, request, false, cancellationToken);
        SetAggregateProvenance(deliveryMeal);
        db.DeliveryMeals.Add(deliveryMeal);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(deliveryMeal);
    }

    public async Task<DeliveryMealDto> CreateFromMealAsync(Guid mealId, CreateDeliveryMealFromMealRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlaceName) || string.IsNullOrWhiteSpace(request.DishName))
        {
            throw new ValidationException("Place name and dish name are required.");
        }

        var meal = await db.Meals
            .AsNoTracking()
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == mealId && m.UserId == UserId, cancellationToken)
            ?? throw new NotFoundException("Meal", mealId);

        if (meal.ConfirmedBolus is null)
        {
            throw new ValidationException("Confirm the insulin dose before saving this meal as a delivery meal.");
        }

        var notes = meal.Notes;
        if (string.IsNullOrWhiteSpace(notes))
        {
            notes = $"Saved from {meal.MealType} on {meal.MealTime:yyyy-MM-dd}.";
        }

        var now = timeProvider.GetUtcNow();
        var deliveryMeal = new DeliveryMeal
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            PlaceName = request.PlaceName.Trim(),
            DishName = request.DishName.Trim(),
            PortionDescription = NormalizePortionDescription(request.PortionDescription),
            Carbs = meal.TotalCarbs,
            UsualInsulinUnits = meal.ConfirmedBolus.Value,
            LastPreMealGlucose = meal.PreMealGlucose,
            ResultRating = request.ResultRating,
            Tags = NormalizeTags(request.Tags),
            Notes = notes,
            IsFavorite = request.IsFavorite,
            UsageCount = 0,
            CreatedAt = now,
            CarbReferenceSource = CarbProvenance.Of(meal).ReferenceSource,
            CarbTransformation = CarbProvenance.Of(meal).Transformation | CarbTransformation.HistoricalReuse,
            SourceMealVerifiedAt = meal.CarbsVerifiedAt
        };

        db.DeliveryMeals.Add(deliveryMeal);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(deliveryMeal);
    }

    public async Task<DeliveryMealDto> UpdateAsync(Guid id, UpsertDeliveryMealRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var deliveryMeal = await db.DeliveryMeals.Include(order => order.Items).FirstOrDefaultAsync(k => k.Id == id && k.UserId == UserId, cancellationToken)
            ?? throw new NotFoundException("Delivery meal", id);

        if ((request.Items is not null || deliveryMeal.Items.Count > 0) && request.ExpectedRevision != deliveryMeal.Revision)
            throw new DeliveryMealConflictException();
        if (request.Items is { Count: 0 } && deliveryMeal.Items.Count > 0)
            throw new ValidationException("Keep at least one structured item. Existing order totals are not converted into item values.");
        if (request.Items is null && deliveryMeal.Items.Count > 0 && request.Carbs != deliveryMeal.Carbs)
            throw new ValidationException("Edit the structured items to change this order total.");
        if (request.Items is { Count: > 0 })
            await SetItemsAsync(deliveryMeal, request, true, cancellationToken);

        if (deliveryMeal.Carbs != request.Carbs || deliveryMeal.PortionDescription != NormalizePortionDescription(request.PortionDescription)
            || deliveryMeal.DishName != request.DishName.Trim() || deliveryMeal.PlaceName != request.PlaceName.Trim())
        {
            deliveryMeal.CarbReferenceSource = CarbReferenceSource.Manual;
            deliveryMeal.CarbTransformation = CarbTransformation.None;
            deliveryMeal.SourceMealVerifiedAt = null;
        }
        deliveryMeal.PlaceName = request.PlaceName.Trim();
        deliveryMeal.DishName = request.DishName.Trim();
        deliveryMeal.PortionDescription = NormalizePortionDescription(request.PortionDescription);
        deliveryMeal.Carbs = request.Carbs;
        deliveryMeal.UsualInsulinUnits = request.UsualInsulinUnits;
        deliveryMeal.LastPreMealGlucose = request.LastPreMealGlucose;
        deliveryMeal.ResultRating = request.ResultRating;
        deliveryMeal.Tags = NormalizeTags(request.Tags);
        deliveryMeal.Notes = request.Notes;
        deliveryMeal.IsFavorite = request.IsFavorite;
        SetAggregateProvenance(deliveryMeal);
        deliveryMeal.Revision = checked(deliveryMeal.Revision + 1);
        await SaveAsync(cancellationToken);
        return ToDto(deliveryMeal);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deliveryMeal = await db.DeliveryMeals.FirstOrDefaultAsync(k => k.Id == id && k.UserId == UserId, cancellationToken)
            ?? throw new NotFoundException("Delivery meal", id);

        db.DeliveryMeals.Remove(deliveryMeal);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DeliveryMealDto> ToggleFavoriteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deliveryMeal = await db.DeliveryMeals.Include(order => order.Items).FirstOrDefaultAsync(k => k.Id == id && k.UserId == UserId, cancellationToken)
            ?? throw new NotFoundException("Delivery meal", id);

        deliveryMeal.IsFavorite = !deliveryMeal.IsFavorite;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(deliveryMeal);
    }

    public async Task<UseDeliveryMealDto> CreateMealDraftFromDeliveryMealAsync(Guid id, CancellationToken cancellationToken)
    {
        var deliveryMeal = await db.DeliveryMeals.FirstOrDefaultAsync(k => k.Id == id && k.UserId == UserId, cancellationToken)
            ?? throw new NotFoundException("Delivery meal", id);

        var now = timeProvider.GetUtcNow();
        deliveryMeal.UsageCount += 1;
        deliveryMeal.LastUsedAt = now;
        await SaveAsync(cancellationToken);

        var notes = $"Delivery meal: {deliveryMeal.PlaceName} - {deliveryMeal.DishName}. {deliveryMeal.PortionDescription}";
        if (!string.IsNullOrWhiteSpace(deliveryMeal.Notes))
        {
            notes += $"{Environment.NewLine}{deliveryMeal.Notes}";
        }

        var items = await db.DeliveryMealItems.AsNoTracking().Where(item => item.DeliveryMealId == id)
            .OrderBy(item => item.DisplayOrder).ToListAsync(cancellationToken);
        var prepared = items.Count == 0 ? null : await Calculator.CalculateItemsAsync(items.Select(item =>
            new MealItemInputDto(Guid.Empty, item.Quantity, SourceDeliveryMealItemId: item.Id)).ToArray(), null, null, cancellationToken);
        return new UseDeliveryMealDto(deliveryMeal.Id, deliveryMeal.Carbs, deliveryMeal.UsualInsulinUnits, notes,
            CarbProvenance.Of(deliveryMeal) with { Transformation = deliveryMeal.CarbTransformation | CarbTransformation.HistoricalReuse }, prepared);
    }

    private MealCalculationService Calculator => calculation ?? new MealCalculationService(db, currentUser, timeProvider);

    public async Task<DeliveryMealPreview> PreviewAsync(DeliveryMealPreviewRequest request, CancellationToken token)
    {
        if (request.Items.Count is < 1 or > 100 || request.Items.Any(item => string.IsNullOrWhiteSpace(item.Name) || item.Name.Trim().Length > 160
            || item.Quantity <= 0 || decimal.Round(item.Quantity, 2) != item.Quantity
            || new[] { item.CarbsPer100g, item.CarbsPerUnit, item.CarbOverride }.Any(value => value.HasValue && decimal.Round(value.Value, 2) != value.Value)))
            throw new ValidationException("Enter 1–100 named items with a positive quantity (up to two decimal places).");
        var linkedIds = request.Items.Where(item => item.FoodItemId.HasValue).Select(item => item.FoodItemId.GetValueOrDefault()).Distinct().ToArray();
        var foods = await db.FoodItems.AsNoTracking().Where(food => linkedIds.Contains(food.Id) && food.UserId == UserId).ToDictionaryAsync(food => food.Id, token);
        if (foods.Count != linkedIds.Length) throw new ValidationException("A linked food is unavailable.");
        var calculated = await Calculator.CalculateItemsAsync(request.Items.Select(item => new MealItemInputDto(item.FoodItemId ?? Guid.Empty,
            item.Quantity, MeasurementType: item.MeasurementType, FoodNameSnapshot: item.Name.Trim(),
            CarbsPer100gSnapshot: item.CarbsPer100g, CarbsPerUnitSnapshot: item.CarbsPerUnit, CarbOverride: item.CarbOverride)).ToArray(), null, null, token);
        var result = calculated.Select((item, index) => item with { Provenance = new(
            request.Items[index].FoodItemId is Guid id && foods[id].MeasurementType == item.MeasurementType
                && (item.MeasurementType == FoodMeasurementType.Grams ? foods[id].CarbsPer100g == item.CarbsPer100g : foods[id].CarbsPerUnit == item.CarbsPerUnit)
                ? foods[id].CarbReferenceSource : CarbReferenceSource.Manual, CarbTransformation.Calculated) }).ToArray();
        var total = Math.Round(result.Sum(item => item.EffectiveCarbs), 2);
        return new(result, total, request.SavedTotal.HasValue ? total - request.SavedTotal.Value : null);
    }

    private async Task SetItemsAsync(DeliveryMeal order, UpsertDeliveryMealRequest request, bool editing, CancellationToken token)
    {
        var inputs = request.Items ?? [];
        if (inputs.Where(item => item.Id.HasValue).Select(item => item.Id).Distinct().Count() != inputs.Count(item => item.Id.HasValue)
            || inputs.Any(item => item.Id.HasValue && !order.Items.Any(saved => saved.Id == item.Id)))
            throw new ValidationException("Item identities must belong to this order and cannot be duplicated.");
        var preview = await PreviewAsync(new(inputs, order.Carbs), token);
        if (request.Carbs != preview.TotalCarbs)
            throw new ValidationException($"Item total is {preview.TotalCarbs} g; submitted total is {request.Carbs} g. Review and accept the calculated total.");
        if (editing && order.Carbs != preview.TotalCarbs && !request.AcceptCalculatedTotal)
            throw new ValidationException($"Saved total is {order.Carbs} g; item total is {preview.TotalCarbs} g (difference {preview.TotalCarbs - order.Carbs} g). Explicitly accept this change.");
        foreach (var removed in order.Items.Where(saved => !inputs.Any(input => input.Id == saved.Id)).ToArray())
        {
            db.DeliveryMealItems.Remove(removed);
            order.Items.Remove(removed);
        }
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            var value = preview.Items[index];
            var item = input.Id is Guid id ? order.Items.Single(saved => saved.Id == id) : new DeliveryMealItem { Id = Guid.NewGuid(), DeliveryMealId = order.Id };
            var referenceChanged = !input.Id.HasValue || item.Name != value.FoodName || item.MeasurementType != value.MeasurementType
                || item.FoodItemId != input.FoodItemId || item.CarbsPer100gSnapshot != value.CarbsPer100g || item.CarbsPerUnitSnapshot != value.CarbsPerUnit;
            var quantityChanged = item.Quantity != value.Quantity || item.CarbOverride != value.CarbOverride;
            item.Name = value.FoodName; item.Quantity = value.Quantity; item.MeasurementType = value.MeasurementType;
            item.CarbsPer100gSnapshot = value.MeasurementType == FoodMeasurementType.Grams ? value.CarbsPer100g : null;
            item.CarbsPerUnitSnapshot = value.MeasurementType == FoodMeasurementType.Grams ? null : value.CarbsPerUnit;
            item.CalculatedCarbs = value.CalculatedCarbs; item.CarbOverride = value.CarbOverride;
            item.FoodItemId = input.FoodItemId; item.DisplayOrder = index;
            if (referenceChanged) { item.CarbReferenceSource = value.Provenance?.ReferenceSource ?? CarbReferenceSource.Unknown; item.SourceMealVerifiedAt = null; }
            if (quantityChanged) item.SourceMealVerifiedAt = null;
            item.CarbTransformation |= CarbTransformation.Calculated;
            if (!input.Id.HasValue)
            {
                order.Items.Add(item);
                // Explicitly Added: generated stable UUIDs must not make EF infer an existing child.
                db.DeliveryMealItems.Add(item);
            }
        }
        order.SourceMealVerifiedAt = null;
    }

    public async Task<IReadOnlyList<CalculatedMealItemDto>> ReuseItemsAsync(Guid id, ReuseDeliveryMealRequest request, CancellationToken token)
    {
        var order = await db.DeliveryMeals.AsNoTracking().Include(order => order.Items).SingleOrDefaultAsync(order => order.Id == id && order.UserId == UserId, token)
            ?? throw new NotFoundException("Delivery meal", id);
        if (request.ExpectedRevision != order.Revision) throw new DeliveryMealConflictException();
        if (request.ItemIds.Count == 0 || request.ItemIds.Distinct().Count() != request.ItemIds.Count
            || request.ItemIds.Any(itemId => !order.Items.Any(item => item.Id == itemId)))
            throw new ValidationException("Select one or more items from this structured order.");
        return await Calculator.CalculateItemsAsync(order.Items.Where(item => request.ItemIds.Contains(item.Id)).OrderBy(item => item.DisplayOrder)
            .Select(item => new MealItemInputDto(Guid.Empty, item.Quantity, SourceDeliveryMealItemId: item.Id)).ToArray(), null, null, token);
    }

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { db.DiscardChanges(); throw new DeliveryMealConflictException(); }
    }

    private static void SetAggregateProvenance(DeliveryMeal order)
    {
        if (order.Items.Count == 0) return;
        var sources = order.Items.Select(item => item.CarbOverride.HasValue ? CarbReferenceSource.Manual : item.CarbReferenceSource).Distinct().ToArray();
        order.CarbReferenceSource = sources.Contains(CarbReferenceSource.Unknown) ? CarbReferenceSource.Unknown
            : sources.Length == 1 ? sources[0] : CarbReferenceSource.Mixed;
        order.CarbTransformation = order.Items.Aggregate(CarbTransformation.Calculated, (value, item) => value | item.CarbTransformation);
    }

    private static void Validate(UpsertDeliveryMealRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlaceName) || string.IsNullOrWhiteSpace(request.DishName))
        {
            throw new ValidationException("Place name and dish name are required.");
        }

        if (request.Carbs < 0 || request.Carbs == 0 && request.Items is not { Count: > 0 })
        {
            throw new ValidationException("Carbs must be greater than zero.");
        }

        if (request.UsualInsulinUnits < 0)
        {
            throw new ValidationException("Usual insulin units cannot be negative.");
        }

        if (request.LastPreMealGlucose < 0)
        {
            throw new ValidationException("Last pre-meal glucose cannot be negative.");
        }
    }

    // Existing non-null column stores absence as empty text; null/blank API input is valid.
    private static string NormalizePortionDescription(string? description) => description?.Trim() ?? string.Empty;

    private static string NormalizeTags(string tags) =>
        string.Join(", ", tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase));

    private static DeliveryMealDto ToDto(DeliveryMeal deliveryMeal) =>
        new(
            deliveryMeal.Id,
            deliveryMeal.PlaceName,
            deliveryMeal.DishName,
            deliveryMeal.PortionDescription,
            deliveryMeal.Carbs,
            deliveryMeal.UsualInsulinUnits,
            deliveryMeal.LastPreMealGlucose,
            deliveryMeal.ResultRating,
            deliveryMeal.Tags,
            deliveryMeal.Notes,
            deliveryMeal.IsFavorite,
            deliveryMeal.UsageCount,
            deliveryMeal.LastUsedAt,
            deliveryMeal.CreatedAt, CarbProvenance.Of(deliveryMeal), deliveryMeal.Items.OrderBy(item => item.DisplayOrder).Select(item => new DeliveryMealItemDto(
                item.Id, item.Name, item.Quantity, item.MeasurementType, item.CarbsPer100gSnapshot, item.CarbsPerUnitSnapshot,
                item.CalculatedCarbs, item.CarbOverride, item.CarbOverride ?? item.CalculatedCarbs, item.DisplayOrder, item.FoodItemId,
                new(item.CarbReferenceSource, item.CarbTransformation, item.SourceMealVerifiedAt))).ToArray(), deliveryMeal.Revision);
}
