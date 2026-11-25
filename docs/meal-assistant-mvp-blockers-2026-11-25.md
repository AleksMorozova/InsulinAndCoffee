# Meal Assistant MVP blocker fixes

## Root causes and implementation

1. `AssistantIntentText.Interpret` parsed the quantity in an entire “I want / Хочу”
   expression before splitting its food list. It now splits first, parses each
   quantity/unit independently, and recognizes English `set NAME to AMOUNT` in
   compound edit commands. The existing portion normalizer handles explicit
   serving/piece prefixes before the numeric fallback. No food-specific parsing
   exceptions were introduced.
2. Splitting consults matching **owned saved names** from Foods and Ask Past Me,
   using a filtered database query when conjunctions occur. This preserves names
   such as “Mac and cheese” within a larger list. Quoted names also remain whole,
   including unknown names that need clarification. Existing ambiguity handling,
   structured LLM fallback and the six-food limit remain in place.
3. Calculator preparation previously returned the latest draft regardless of
   what the caller displayed. It now validates the displayed version both before
   preparing items and after preparation. Another client's change before or
   during preparation produces a conflict instead of different exported items.
4. Angular sends its displayed version. On a conflict it blocks export, loads
   authoritative state and asks the user to review Current Meal and explicitly
   click Use in Calculator again. Failed refresh leaves export blocked. No
   automatic navigation or automatic retry exports the refreshed meal.

## API contract

The existing authenticated endpoint remains:

`GET /api/meal-assistant/conversations/{id}/calculator-draft?expectedDraftVersion=7`

- `expectedDraftVersion` is required and nonnegative, including valid version 0.
- Success response remains the existing `CalculatedMealItemDto[]` array.
- Missing, negative or malformed versions return HTTP 400 ProblemDetails.
- Stale versions return HTTP 409 ProblemDetails with
  `code: draft_version_conflict` and `currentDraftVersion`.
- User ownership checks remain enforced.
- Backend and frontend must be deployed together; older clients that omit the
  version must refresh to the updated frontend.

## Files changed for this task

Production code:

- `backend/src/InsulinAndCoffee.Application/Services/AssistantIntentText.cs`
- `backend/src/InsulinAndCoffee.Application/Services/MealAssistantService.cs`
- `backend/src/InsulinAndCoffee.Api/Controllers/MealAssistantController.cs`
- `frontend/src/app/core/api.service.ts`
- `frontend/src/app/pages/meal-assistant.component.ts`

New backend tests:

- `backend/tests/InsulinAndCoffee.Application.Tests/AssistantReleaseBlockerTests.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AssistantExportPostgreSqlTests.cs`

Updated backend tests for the required version and preserved-value regressions,
under `backend/tests/InsulinAndCoffee.Application.Tests/`:

- `AssistantUxReliabilityTests.cs`, `MealAssistantTests.cs`, `MealOperationTests.cs`
- `SafeMealDeduplicationTests.cs`, `SafeDeduplicationPostgreSqlTests.cs`
- `MealOperationPostgreSqlTests.cs`, `PersonalFoodMemoryAcceptanceTests.cs`
- `PartialMealAssemblyAcceptanceTests.cs`

Updated Angular tests:

- `frontend/src/app/core/api.service.spec.ts`
- `frontend/src/app/pages/meal-assistant.component.spec.ts`

Pre-existing UX changes and skill/audit artifacts were preserved. No migration,
dependency, unrelated service refactor or git commit was created.

## Verification results

- `dotnet test InsulinAndCoffee.sln --configuration Release --no-restore
  --verbosity minimal`: **377 passed, 0 failed, 0 skipped**. `ASSISTANT_TEST_POSTGRES`
  pointed at localhost PostgreSQL on port 5433; tests created and removed their
  own isolated databases.
- Angular suite: **152 passed**. Used a temporary Karma configuration with manual
  headless Edge capture because the installed Windows launcher exits early.
  Project Karma configuration was unchanged; the browser disconnect message
  occurred during shutdown after the successful test summary.
- `npm run build`: **passed**. Existing component-style warning budgets remain
  exceeded (Meal Assistant 7.23 kB; Ask Past Me 5.77 kB). Budgets were unchanged.
- `git diff --check`: **passed**.

New tests assemble actual drafts for the requested English/Russian/Ukrainian
quantified lists, mixed unit lists and compound remove/set commands without a
provider. They assert distinct item identities, quantities, units, totals and
idempotent replay. Further checks cover saved conjunction names, quoted unknown
names and ambiguous competing references.

PostgreSQL/HTTP tests verify missing/negative versions, successful export,
another client's change before export, an intercepted change during preparation,
409 refresh information, and explicit export of the refreshed version. Angular
tests verify the version parameter, no navigation on conflict, blocked export
while refresh is pending/failed, and explicit retry using the updated values.

The full regression suites also cover Add/Remove/Replace/SetQuantity, California
and Sushi deduplication/reconciliation, pending clarification identities,
historical scaling, receipt idempotency, carbohydrate overrides, provenance and
the existing historical-repeat carbohydrate-adjustment workflow. Source foods
and user-confirmed carbohydrate values are not rewritten by these fixes.

## Remaining limitations

Free-form language outside deterministic grammar still uses the existing
structured interpreter or clarification path. Unknown conjunction-containing
names can be quoted to make their boundary explicit. No live OpenAI call,
production performance benchmark or production database modification was needed.
These changes do not address the separate durable-client-retry, duplicate provider
billing or telemetry latency risks identified by the review.
