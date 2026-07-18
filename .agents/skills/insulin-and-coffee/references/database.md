# Database and EF Core

## EF Core rules

- Use migrations for schema changes.
- Review generated migrations before accepting.
- Use `AsNoTracking` for read-only queries where appropriate.
- Avoid unnecessary `Include` chains.
- Avoid N+1 queries.
- Keep entity relationships explicit.
- Use transactions for multi-entity state changes when consistency matters.

## Meal memory persistence

Store facts, not just text.

Useful fields:

- meal name
- date/time
- ingredients/items
- estimated carbs
- portion/weight
- source of estimate
- user notes
- tags
- confidence
- whether user confirmed it

## LLM data

Avoid storing opaque LLM answers as the only source of truth.
If storing LLM output, include:

- model name
- prompt version
- generated answer
- source meal ids
- timestamp
- user confirmation status

## Naming migrations

Migration names should describe business change:

- `AddKnownMeals`
- `RenameDeliveryMealsToLazyMeals`
- `AddMealMemoryNotes`
- `AddCarbEstimateConfidence`
