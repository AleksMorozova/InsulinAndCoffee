# Architecture

Insulin & Coffee should be simple, useful, and maintainable.

## Preferred layers

Backend:

- API/controllers: HTTP endpoints, request/response mapping, status codes.
- Application/services: use cases and orchestration.
- Domain/entities/value objects: business rules and invariants.
- Infrastructure: EF Core, Ollama/local LLM client, external integrations.

Frontend:

- Angular components for UI.
- Services for API calls.
- Typed models for request/response contracts.
- Feature folders for related screens.

## Design priorities

1. Boring architecture over clever abstractions.
2. Explicit API contracts.
3. Deterministic calculation logic.
4. LLM is optional and replaceable.
5. Keep medical-adjacent logic auditable.
6. Keep features usable even when LLM is unavailable.

## Feature boundaries

Likely domains:

- Meals
- KnownMeals
- LazyDelivery / Delivery meals
- Carb estimates
- Meal notes
- Meal memory queries
- User settings / insulin ratio notes
- Food items / ingredients

## Do not

- Do not couple Angular directly to database shape.
- Do not put EF Core logic in controllers.
- Do not let LLM output directly mutate core entities without user confirmation.
- Do not create large generic repositories unless the project already uses them.
