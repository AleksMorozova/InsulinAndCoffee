# Backend rules

## .NET style

- Use async APIs with `CancellationToken`.
- Keep controllers thin.
- Keep services explicit and readable.
- Use DTOs for API boundaries.
- Prefer constructor injection.
- Validate request models.
- Return meaningful HTTP statuses.

## Controllers

Controller naming should reflect resource intent.

Good:

- `MealsController`
- `KnownMealsController`
- `MealMemoryController`
- `DeliveryMealsController` or `LazyMealsController`

Avoid route names that no longer match product language.
If “delivery” was renamed to “lazy meals”, update route, DTOs, service names, frontend models, and database naming consistently.

## Service methods

Prefer use-case names:

- `CreateFromMealAsync`
- `GetSectionsAsync`
- `SearchKnownMealsAsync`
- `RememberMealAsync`
- `AskMealMemoryAsync`

## Error handling

- Use `404` when an entity is missing.
- Use `400` for invalid user input.
- Use `409` for duplicates/conflicts.
- Do not swallow exceptions silently.
- Log unexpected exceptions with useful context.

## Observability

When useful, include:

- correlation id
- meal id
- known meal id
- user id if auth exists
- LLM provider/model when LLM is used
- latency for LLM calls
