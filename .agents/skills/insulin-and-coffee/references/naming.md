# Naming conventions

## Product language

Use consistent domain language.

Preferred concepts:

- Meal
- KnownMeal
- MealMemory
- CarbEstimate
- LazyMeal or DeliveryMeal, but do not mix both unless intentionally migrating
- UserNote
- FoodItem

If renaming a concept:

1. Rename backend entity/service/DTO.
2. Rename API route if needed.
3. Rename frontend model/service/component.
4. Rename database table/columns via migration if appropriate.
5. Add compatibility only if existing clients need it.

## C# naming

- Entities: singular noun, e.g. `KnownMeal`.
- DTOs: suffix `Dto`.
- Requests: suffix `Request`.
- Services: suffix `Service`.
- Interfaces: prefix `I`.
- Async methods: suffix `Async`.

## Angular naming

- Components: `known-meals-page.component.ts`.
- Services: `known-meals.service.ts`.
- Models: `known-meal.model.ts`.
- Keep names close to backend DTOs where useful.
