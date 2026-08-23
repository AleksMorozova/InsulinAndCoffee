# AGENTS.md

## Repository purpose

- Insulin & Coffee is a medical-adjacent meal, carbohydrate, and insulin tracking application.
- Changes to carbohydrates, glucose, insulin, meal totals, confirmation state, or historical meal data must be explicit, predictable, and tested.
- Do not present insulin calculations, meal outcomes, or glucose behavior as medical advice.
- Do not invent meal history, carbohydrate values, glucose reactions, or medical context when data is missing.

## Architecture boundaries

- API controllers must handle HTTP concerns only.
- API controllers must delegate business workflows to Application services.
- API controllers must not contain carbohydrate formulas, insulin formulas, EF Core queries, or meal workflow decisions.
- Application services must coordinate use cases, validation, DTO mapping, and persistence.
- Application services must access persistence through the existing `IAppDbContext` boundary.
- Domain code must contain entities and pure reusable calculations that do not depend on HTTP, EF Core, PostgreSQL, Angular, configuration, or system time.
- Infrastructure must own `AppDbContext`, EF Core configuration, PostgreSQL/Npgsql setup, migrations, and seed data.
- Do not introduce repositories, mediators, CQRS frameworks, background jobs, caches, messaging, or new service abstractions without an actual repository need and explicit task scope.

## Database and external services

- Runtime database access uses PostgreSQL through the existing EF Core/Npgsql infrastructure.
- Normal backend startup requires a non-empty `ConnectionStrings:DefaultConnection`.
- Backend startup applies EF Core migrations outside the `Testing` environment.
- There are currently no active external HTTP or LLM integrations in backend source.
- Do not introduce external HTTP services, LLM clients, file-system persistence, queues, caches, or other infrastructure without explicit scope.

## Public API and errors

- Public DTO fields, frontend models, and API service request/response shapes must stay synchronized.
- Public asynchronous C# methods must return `Task` or `Task<T>` and must not use `async void`.
- Do not rename, remove, or alias public contract fields without updating every backend, frontend, persistence, and test usage.
- Do not change existing API route behavior or HTTP status behavior without explicit scope.
- Validation failures must use the existing ProblemDetails convention with HTTP 400.
- Missing resources must use the existing ProblemDetails convention with HTTP 404.
- Unauthorized access must use the existing ProblemDetails convention with HTTP 403.
- Unexpected server errors must use the existing ProblemDetails convention with HTTP 500 and server-side logging.
- Do not catch exceptions just to suppress them, hide failed calculations, or return default medical-adjacent data.

## Calculation and data integrity

- Use `decimal` for backend carbohydrate, insulin, glucose, ratio, correction-factor, weight, quantity, and supply calculations.
- Preserve existing rounding behavior unless a task explicitly changes it and adds tests.
- Centralize formulas in existing calculation helpers instead of duplicating formulas in controllers, services, or Angular components.
- Backend-calculated carbohydrate and insulin values are the source of truth for medical-adjacent calculations.
- A numeric value of `0` is valid wherever the current contract permits it.
- Keep `MealBolus`, `CorrectionBolus`, `SuggestedBolus`, `ConfirmedBolus`, `CarbAdjustment`, calculated carbs, carb overrides, and effective carbs semantically distinct.
- A meal may exist without `ConfirmedBolus`.
- Confirmed meal item add, update, and remove flows must remain blocked unless the existing confirmation state is cleared.
- Clearing a confirmed bolus must return the meal to the pending confirmation state without changing unrelated meal calculation fields.
- Meal item add, update, and remove flows must recalculate meal totals through the existing meal recalculation workflow.
- Meal item changes that affect totals must clear the confirmed bolus where the current workflow does so.
- Food values used by historical meal items must come from saved meal item snapshots when snapshot fields exist.
- Historical meal items must not be reinterpreted from current Food metadata after a Food is edited.
- Carb overrides must affect effective item carbs without destroying the calculated carb snapshot.
- Meal-level carb adjustments must affect total carbs and bolus calculation without being merged into item snapshots.
- Final meal carbohydrates must not become negative.

## Time

- Use the injected `TimeProvider` for current-time and current-day backend behavior.
- Do not use `DateTime.Now` or `DateTimeOffset.UtcNow` directly for current-day application logic.
- Dashboard and day-based queries must use local calendar-day boundaries converted to UTC.
- Do not introduce a new timezone subsystem unless the current architecture requires it and the task explicitly covers it.

## EF Core and migrations

- Use asynchronous EF Core operations for request-handling persistence.
- Pass `CancellationToken` through controllers, Application services, and EF Core calls.
- Use `AsNoTracking()` for read-only queries unless tracking is required.
- Avoid loading complete tables into memory when filtering, counting, aggregation, or ordering can be done in the database.
- Avoid N+1 database access patterns.
- Schema changes require a new EF Core migration.
- Non-schema changes must not create EF Core migrations.
- Never edit an existing EF Core migration to represent a new schema change.

## Frontend

- Angular components must use the existing `ApiService` for backend communication.
- Angular components must not call backend URLs directly when `ApiService` already owns that API area.
- Angular components may manage presentation state, form state, loading states, empty states, errors, and user actions.
- Angular components must not duplicate backend carbohydrate, insulin, glucose, or meal-total formulas.
- Do not use unsafe TypeScript casts, non-null assertions, or `any` to bypass frontend contract mismatches.

## Tests and change discipline

- Inspect current code before editing because repository details change quickly.
- Preserve unrelated dirty worktree changes.
- Keep changes scoped to the requested behavior.
- Business-rule changes require relevant backend tests.
- Calculation changes require tests covering rounding, zero values, negative values where allowed, and validation behavior.
- Meal confirmation, item recalculation, carb override, carb adjustment, dashboard filtering, and historical snapshot changes require regression tests.
- Public API contract changes require synchronized backend and frontend tests or validation.
- Frontend-only UI changes should run the Angular build or existing frontend test command when practical.
