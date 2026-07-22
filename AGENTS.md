# AGENTS.md

## Project overview

**Insulin & Coffee** is an application for recording meals, calculating carbohydrates, storing insulin-related meal data, and reusing information from previous meals.

The project supports type 1 diabetes workflows. Changes related to carbohydrates, glucose, insulin, meal totals, or confirmation state must be explicit, predictable, testable, and placed in the correct architectural layer.

Do not treat calculated insulin values as generic UI-only data. They are business data and must stay consistent across the backend, database, and frontend contracts.

## Technology stack

### Backend

- C# / .NET 9
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- Dependency Injection
- Async/await
- Clean Architecture-style separation

### Frontend

- Angular
- TypeScript
- Standalone components
- Angular services for API communication
- Reactive Forms where form validation is required

## Repository structure

```text
backend/
  src/
    InsulinAndCoffee.Api/
    InsulinAndCoffee.Application/
    InsulinAndCoffee.Domain/
    InsulinAndCoffee.Infrastructure/
  tests/
    InsulinAndCoffee.Application.Tests/

frontend/
  src/app/
```

Root solution:

```text
InsulinAndCoffee.sln
```

Always inspect current files before editing. This application has evolved quickly, and older notes may use stale names.


## Core project rules

These rules are mandatory for every change:

1. Place HTTP concerns only in `backend/src/InsulinAndCoffee.Api`; controllers must delegate business workflows to the Application layer.
2. Place reusable carbohydrate, insulin, glucose, and meal-total calculations in `backend/src/InsulinAndCoffee.Domain` when they can be expressed without HTTP, EF Core, PostgreSQL, or Angular dependencies.
3. Keep workflow orchestration, DTO mapping, validation, and persistence coordination in `backend/src/InsulinAndCoffee.Application`.
4. Keep EF Core configuration, migrations, and persistence implementations in `backend/src/InsulinAndCoffee.Infrastructure`.
5. Use `frontend/src/app/core/api.service.ts` for backend communication; Angular components must not call API URLs directly.
6. Use `decimal` for insulin, carbohydrate, glucose, ratio, correction-factor, and weight calculations, and preserve the project’s existing rounding behavior.
7. Pass `CancellationToken` through controllers, application services, and asynchronous EF Core calls.
8. Add or update tests whenever business rules, calculations, meal confirmation behavior, dashboard filtering, or item recalculation changes.

### Explicit prohibitions

- **DO NOT** put insulin formulas, carbohydrate formulas, EF Core queries, or meal workflow decisions in API controllers.
- **DO NOT** duplicate backend calculation logic in Angular components or treat a valid numeric value of `0` as missing data.
- **DO NOT** introduce new DTO or field aliases when an existing contract name already exists.
- **DO NOT** use `DateTime.Now` or `DateTimeOffset.UtcNow` directly for current-day dashboard logic; follow the existing `TimeProvider` pattern.
- **DO NOT** modify files outside the task scope unless the change is required for the project to compile; report every necessary out-of-scope change before applying it.
- **DO NOT** perform unrelated refactoring, renaming, formatting, folder restructuring, or dependency upgrades while implementing a feature or fixing a bug.
- **DO NOT** change existing API routes, DTO contracts, database column names, or frontend model fields without updating the complete backend–frontend contract and relevant tests.
- **DO NOT** create a new service, repository, DTO, helper, or abstraction before checking whether an equivalent implementation already exists.
- **DO NOT** add a database migration for changes that do not modify the persisted schema.
- **DO NOT** edit existing EF Core migrations; create a new migration for every new schema change.
- **DO NOT** silently change insulin, carbohydrate, correction, rounding, or confirmation behavior. Any such change must be explicitly requested and covered by tests.
- **DO NOT** catch exceptions only to suppress them, return default values, or hide failed insulin and meal calculations.
- **DO NOT** use non-null assertions, unsafe TypeScript casts, or `any` merely to bypass frontend type errors.
- **DO NOT** leave generated code with compilation errors, failing tests, placeholder implementations, commented-out alternatives, or unresolved TODOs.

## Change scope constraints

- Modify only files directly required by the task.
- Preserve unrelated user changes already present in the working tree.
- Before changing a public contract, identify all backend, frontend, persistence, and test usages.
- Prefer extending an existing implementation over introducing a parallel workflow.
- Keep UI-only tasks UI-only unless a backend change is explicitly required.
- Keep backend-only tasks backend-only unless the public API contract changes.
- When the requested task conflicts with these rules or the current architecture, stop and explain the conflict before implementing it.

## Data integrity constraints

- A meal may exist without `ConfirmedBolus`; do not assume insulin confirmation is mandatory during meal creation.
- `SuggestedBolus`, `MealBolus`, `CorrectionBolus`, and `ConfirmedBolus` represent different concepts and must not be substituted for one another.
- Updating, adding, or removing a meal item must use the existing recalculation workflow.
- Food carbohydrate values used by an existing meal must come from the stored snapshot where the current model requires it, not from silently refreshed food-library data.
- A numeric value of `0` is valid for carbohydrates, glucose-related calculations, correction values, and insulin fields where the existing contract permits it.

## Correct pattern example

A controller receives HTTP input and delegates to an application service. The reusable calculation stays outside the controller.

```csharp
// API layer: HTTP concerns only
[HttpPost("calculate")]
public async Task<ActionResult<CalculatedMealDto>> Calculate(
    CalculateMealRequest request,
    CancellationToken cancellationToken)
{
    var result = await mealService.CalculateMealAsync(request, cancellationToken);
    return Ok(result);
}

// Domain layer: reusable business calculation
public static class MealCarbCalculator
{
    public static decimal Calculate(decimal weightGrams, decimal carbsPer100g)
    {
        if (weightGrams < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weightGrams));
        }

        if (carbsPer100g < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(carbsPer100g));
        }

        return Math.Round(weightGrams * carbsPer100g / 100m, 2);
    }
}
```

Forbidden alternative:

```csharp
// DO NOT calculate carbohydrates or query EF Core directly in a controller.
var totalCarbs = request.Items.Sum(x => x.WeightGrams * x.CarbsPer100g / 100m);
var meals = await dbContext.Meals.ToListAsync(cancellationToken);
```

## Verified important paths

Backend:

- `backend/src/InsulinAndCoffee.Api/Controllers/MealsController.cs`
- `backend/src/InsulinAndCoffee.Api/Controllers/DashboardController.cs`
- `backend/src/InsulinAndCoffee.Application/Services/MealService.cs`
- `backend/src/InsulinAndCoffee.Application/Dtos/CommonDtos.cs`
- `backend/src/InsulinAndCoffee.Domain/Calculations/MealCarbCalculator.cs`
- `backend/src/InsulinAndCoffee.Domain/Entities/Meal.cs`
- `backend/src/InsulinAndCoffee.Domain/Entities/MealItem.cs`
- `backend/src/InsulinAndCoffee.Domain/Entities/FoodItem.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Persistence/InsulinCoffeeDbContext.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Persistence/Migrations/`

Frontend:

- `frontend/src/app/app.routes.ts`
- `frontend/src/app/core/api.service.ts`
- `frontend/src/app/pages/dashboard/dashboard.component.ts`
- `frontend/src/app/pages/calculator/calculator.component.ts`
- `frontend/src/app/pages/history/history.component.ts`
- `frontend/src/app/pages/meal-details/meal-details.component.ts`
- `frontend/src/app/pages/delivery-meals/delivery-meals.component.ts`
- `frontend/src/app/pages/foods/foods.component.ts`
- `frontend/src/app/pages/settings/settings.component.ts`
- `frontend/src/app/pages/supplies/supplies.component.ts`

## Verified routes and API names

Frontend routes:

- `/` -> Dashboard
- `/calculator` -> New Meal / Current Meal
- `/history` -> Meal History
- `/meals/:id` -> Meal Details
- `/delivery-meals` -> Ask Past Me
- `/foods` -> Food Library
- `/supplies` -> Supplies
- `/settings` -> Settings

Dashboard API:

- `GET /api/dashboard`
- `GET /api/dashboard/today`
- `DashboardController.Get`
- `DashboardController.GetToday`
- `MealService.GetDashboardAsync`
- `ApiService.getDashboard`

Meal API:

- `POST /api/meals/calculate`
- `POST /api/meals`
- `GET /api/meals`
- `GET /api/meals/{id}`
- `PATCH /api/meals/{id}/confirmed-bolus`
- `PATCH /api/meals/{id}/items`
- `PUT /api/meals/{mealId}/items/{itemId}`
- `DELETE /api/meals/{mealId}/items/{itemId}`

Meal service methods:

- `MealService.CalculateMealAsync`
- `MealService.CreateMealAsync`
- `MealService.GetMealsAsync`
- `MealService.GetMealAsync`
- `MealService.ConfirmMealBolusAsync`
- `MealService.AddMealItemsAsync`
- `MealService.UpdateMealItemAsync`
- `MealService.RemoveMealItemAsync`
- `MealService.GetDashboardAsync`

Frontend API service methods:

- `ApiService.calculateMeal`
- `ApiService.createMeal`
- `ApiService.getMeals`
- `ApiService.getMeal`
- `ApiService.confirmMealBolus`
- `ApiService.addMealItems`
- `ApiService.updateMealItem`
- `ApiService.removeMealItem`
- `ApiService.getDashboard`

## Current DTO and field names

Use the existing DTO and entity names exactly.

Important DTOs in `CommonDtos.cs`:

- `DashboardDto`
- `DashboardMealDto`
- `CreateMealRequest`
- `ConfirmMealBolusRequest`
- `AddMealItemsRequest`
- `UpdateMealItemRequest`
- `MealSummaryDto`
- `MealDetailDto`
- `MealItemInputDto`
- `MealItemDto`
- `CalculatedMealItemDto`

Important field names:

- `ConfirmedBolus`
- `SuggestedBolus`
- `MealBolus`
- `CorrectionBolus`
- `TotalCarbs`
- `PreMealGlucose`
- `WeightGrams`
- `CarbsPer100g`
- `CarbsPer100gSnapshot`
- `CalculatedCarbs`

Do not introduce stale aliases such as `MealDetailsDto`, `WeightInGrams`, `CarbohydratesPer100Grams`, or `ConfirmedInsulinDose` unless the code is intentionally being renamed across the full contract.

## Backend architecture rules

### API layer

`backend/src/InsulinAndCoffee.Api` contains controllers, HTTP binding, response status mapping, dependency injection, middleware, and startup configuration.

Controllers must remain thin. They may:

- receive route, query, and body input;
- call application services;
- pass `CancellationToken`;
- convert service results into HTTP responses.

Controllers must not:

- contain insulin formulas;
- contain carbohydrate formulas;
- execute Entity Framework queries directly;
- mutate domain entities directly;
- implement meal workflow decisions.

### Application layer

`backend/src/InsulinAndCoffee.Application` contains application services, use cases, DTOs, validation for application workflows, orchestration of persistence and domain calculations, and mapping where current project patterns place it.

Application services may coordinate EF Core queries through the configured context, but reusable formulas should live in the Domain layer when they can be expressed using plain values or domain objects.

### Domain layer

`backend/src/InsulinAndCoffee.Domain` contains entities, domain-specific validation, reusable calculations, and business rules that do not depend on HTTP, EF Core, PostgreSQL, or Angular.

Current reusable carbohydrate aggregation lives in:

```text
backend/src/InsulinAndCoffee.Domain/Calculations/MealCarbCalculator.cs
```

Prefer adding reusable meal/carbohydrate/insulin calculations to Domain when they do not require infrastructure.

### Infrastructure layer

`backend/src/InsulinAndCoffee.Infrastructure` contains EF Core persistence, entity configuration, migrations, and integration-specific implementations.

Infrastructure may depend on Application and Domain abstractions. Application and Domain must not depend on Infrastructure implementation details.

## Frontend architecture rules

Angular components should focus on presentation and user interaction.

Components may:

- collect user input;
- display validation and loading/error/empty states;
- call Angular services;
- manage view state;
- transform data for display only.

Components must not:

- duplicate backend insulin formulas;
- duplicate backend carbohydrate formulas;
- implement independent medical business rules;
- call backend URLs directly when `ApiService` already has the pattern;
- mix unrelated API access into feature components.

Backend-calculated values are the source of truth for insulin and carbohydrate calculations.

## Calculation and medical-safety rules

- Use `decimal` for insulin, carbohydrates, glucose, weight, ratios, and correction factors.
- Do not replace `decimal` with `double` or `float` in business calculations.
- Reuse existing rounding conventions. Search for existing `Math.Round` usage before adding a new rounding rule.
- Do not duplicate formulas inline across services, controllers, or components.
- Support valid positive and negative correction values where the business logic requires correction bolus calculations.
- A real value of `0` must be treated as a valid value, not as loading, missing, or error state.
- Do not add medical advice, outcome prediction, or new health guidance unless explicitly requested and implemented with care.

## Time handling

Use the project’s existing `TimeProvider` conventions when determining current time or current day.

Do not use `DateTime.Now` or `DateTimeOffset.UtcNow` directly for current-day dashboard logic.

Pay attention to:

- timestamps stored in UTC;
- the user’s local calendar day;
- inclusive start / exclusive end filtering for daily ranges.

Do not introduce a large timezone subsystem unless the current architecture needs it.

## EF Core and database rules

- Use asynchronous EF Core operations in request-handling and application-service code.
- Pass `CancellationToken` through controller, service, and database calls.
- Use `AsNoTracking()` for read-only queries unless tracking is required.
- Avoid loading all meals into memory for totals, counts, or dashboard summaries when the database can aggregate/filter efficiently.
- Avoid N+1 queries.
- Add EF Core migrations when schema changes require them.
- Never edit an existing migration to represent a new schema change; generate a new migration instead.
- If a nullable business field such as `ConfirmedBolus` becomes nullable in the entity, make sure the generated migration updates the database column too.

## Dashboard rules

Dashboard data should come from a dedicated dashboard request when possible:

- `GET /api/dashboard`
- `GET /api/dashboard/today`

The dashboard should not calculate meal totals on the frontend. It should display backend-provided data from `DashboardDto` and `DashboardMealDto`.

Keep dashboard responsibilities separated:

- Controller: HTTP contract only.
- Application service/query: builds the dashboard summary.
- Database query: filters and aggregates today’s meals.
- DTOs: return only data required by the dashboard.
- Frontend service: calls the dashboard endpoint.
- Dashboard component: presentation and user actions.

## Meal Details rules

Meal Details should use existing meal item and confirmation flows:

- add items with `AddMealItemsAsync` / `ApiService.addMealItems`;
- update item weight with `UpdateMealItemAsync` / `ApiService.updateMealItem`;
- remove items with `RemoveMealItemAsync` / `ApiService.removeMealItem`;
- confirm insulin with `ConfirmMealBolusAsync` / `ApiService.confirmMealBolus`.

When meal items change, preserve existing recalculation behavior through `RecalculateMealTotals` and the current domain calculation helpers.

Do not silently change unrelated meal creation, dashboard, history, or delivery-meal behavior while polishing Meal Details UI.

## Ask Past Me / delivery meals

The Ask Past Me page currently routes through:

```text
/delivery-meals
```

Use existing delivery-meal components, services, and routes for memory/reuse workflows. Do not send Ask Past Me actions to History unless a task explicitly requests that change.

## Testing expectations

When changing business logic, add or update relevant tests under:

```text
backend/tests/InsulinAndCoffee.Application.Tests/
```

Cover regressions for:

- carbohydrate totals;
- bolus and correction calculations;
- confirmed vs unconfirmed bolus behavior;
- dashboard day filtering;
- meal count and ordering;
- previous-day meal exclusion;
- item add/update/remove recalculation;
- zero values being valid values.

For frontend UI-only changes, at minimum run the Angular build or the project’s existing frontend validation command when practical.

## Agent behavior

When requirements are ambiguous:

- Ask before changing architecture.
- Ask before renaming public APIs or DTO fields.
- Ask before changing the database schema.
- Ask before modifying insulin, carbohydrate, correction, rounding, or confirmation logic.
- Prefer one focused clarification question over making an architectural or medical-business assumption.

## Workflow for agents

Before editing:

1. Inspect the current file and neighboring files.
2. Verify exact paths, route names, DTO names, method names, and field names.
3. Check for existing reusable helpers before adding new ones.
4. Preserve unrelated user changes in the working tree.

While editing:

- Follow existing project style.
- Keep changes scoped to the request.
- Avoid broad refactors during UI polish or bug-fix tasks.
- Prefer small, named methods over duplicated formulas.
- Keep frontend display logic separate from backend business logic.

Before finishing:

- Run targeted tests or validation in proportion to the change.
- Report any tests that were not run.
- Summarize changed files and business-logic impact.

