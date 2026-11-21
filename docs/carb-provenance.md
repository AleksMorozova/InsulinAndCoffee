# Carb provenance and explicit verification (PR #4)

## Gap analysis

Food creation and updates accepted a carbohydrate reference without recording its origin. Meal calculation already froze that reference, calculated carbs and any override separately; meal adjustments remained at meal level. Saving a meal, confirming insulin, creating a saved order, assistant draft serialization and Calculator reuse had no explicit carbohydrate verification model. Raw historical inputs could carry a snapshot but could not establish a trustworthy origin.

## Model and lifecycle

`CarbReferenceSource` identifies the reference: `Unknown` (0), `Manual` (1), `AiEstimate` (2), or `Mixed` (3). `CarbTransformation` is a flags enum: `None` (0), `Calculated` (1), `HistoricalReuse` (2). A manual reference multiplied by a quantity remains manual in origin and gains the Calculated flag.

- FoodItem stores the reference source. New manual entries are Manual. Editing the active carbohydrate basis or measurement type makes it Manual; favorite-only edits preserve the original source.
- MealItem freezes source, transformations and optional `SourceMealVerifiedAt` beside the existing numeric snapshot. CalculatedCarbs, CarbOverride and meal CarbAdjustment retain their existing meanings.
- DeliveryMeal stores the same metadata for its complete order value. Saving from a meal aggregates effective sources (an override/adjustment contributes Manual), adds HistoricalReuse and captures that source meal's verification timestamp. Relevant order edits clear this lineage and become Manual.
- Meal stores `CarbsVerifiedAt` and `CarbRevision`. Verification applies to the **entire saved meal snapshot**, including its foods, quantities, overrides and adjustment. It is not verification of a Food library entry, a later portion, glucose or insulin.
- A new meal is unverified, even when repeated from a verified source. `SourceMealVerifiedAt` describes past lineage only. Saving, editing, overriding or confirming/clearing insulin does not verify carbs.
- Adding/removing a food or changing quantity/override clears meal verification and advances the revision. Changes to current Food metadata do not reinterpret frozen historical snapshots.
- Meal aggregate provenance reports Unknown conservatively when any effective source is unknown; otherwise it reports the sole source or Mixed.

The backend has no nutritional AI estimator. AiEstimate is supported as a typed origin for trusted data, but an LLM food lookup never creates that origin. No extra LLM calls were added.

## Trusted actions and contracts

`POST /api/meals/{id}/carb-verification` accepts only `{ "expectedCarbRevision": 0 }`. The Application service checks ownership, uses TimeProvider for the timestamp and commits using an EF concurrency token. A stale review or competing edit returns HTTP 409 ProblemDetails with `code: carb_snapshot_changed`; the UI reloads the meal for review.

Verification timestamps and provenance are response metadata, not accepted client assertions. Food, order, meal creation/calculation and item snapshot requests reject unknown fields. Historical handoffs pass `SourceMealItemId`; saved order handoffs pass `SourceDeliveryMealId`. The backend resolves owned records and authoritative source/basis, ignoring any supplied historical name or basis. A requested measurement must match the source. `ClearCarbOverride` explicitly distinguishes clearing an inherited override from retaining it. A complete saved order remains one portion.

Assistant drafts serialize provenance and preserve it during quantity transformations. Preparation recalculates trusted metadata and checks existing numeric snapshots against their source before navigation. Calculator sends source identities, not verification assertions. Unreferenced legacy/raw snapshots remain Unknown.

## Migration and compatibility

`20261121132517_AddCarbProvenanceAndVerification` adds source to FoodItems; source/transformation/source-verification timestamp to MealItems and DeliveryMeals; revision/verification timestamp to Meals. The UP migration is additive. Existing enum fields default to 0, timestamps to null and revision to 0. It does not change existing quantities, references, overrides, adjustments, insulin or confirmation values. No historical verification is inferred and no legacy nutritional origin is invented.

Response fields and request source identities are additive; old requests still work with Unknown origin where no trusted reference exists. Unknown JSON properties on the affected input DTOs now return 400 rather than being silently ignored. Deploy the backend migration before the updated frontend. Normal existing startup migration behavior is retained. During development validation, migrations were applied only to disposable local PostgreSQL databases, not the application database or any hosted database.

New direct-carb meals also retain their entered total as a one-portion reference basis, allowing faithful later reuse. Existing direct-carb snapshots without a usable basis are not automatically repaired or invented.

## Changed files

- Domain: `Enums/CarbProvenance.cs`; `Entities/FoodItem.cs`, `Meal.cs`, `MealItem.cs`, `DeliveryMeal.cs`.
- Application: `Dtos/CommonDtos.cs`, `MealAssistantDtos.cs`; `Abstractions/CarbVerificationConflictException.cs`; `Services/CarbProvenance.cs`, `FoodService.cs`, `MealCalculationService.cs`, `MealService.cs`, `DeliveryMealService.cs`, `AssistantDraftBuilder.cs`, `FoodKnowledgeSearchService.cs`, `MealHistorySearchService.cs`.
- API: `Controllers/MealsController.cs`, `ExceptionHandling/GlobalExceptionHandler.cs`.
- Infrastructure: `Persistence/Configurations/MealConfiguration.cs`; the new migration and designer; `Migrations/AppDbContextModelSnapshot.cs`.
- Angular: `core/models.ts`, `meal-assistant.models.ts`, `api.service.ts`; `shared/carb-provenance.component.ts`; Calculator, Foods, DeliveryMeals, MealAssistant and MealDetails components; `styles.css`.
- Tests: `CarbProvenanceTests.cs`, `AssistantPostgreSqlSafetyTests.cs`, `AuthIntegrationTests.cs`; Angular provenance component, API service, Calculator, MealAssistant and MealDetails specs.

## Validation and remaining scope

Backend: 237 tests passed, none skipped, including isolated PostgreSQL migration, concurrency and Production-startup tests. Coverage includes manual/deterministic/unknown sources, trusted AI-origin fixtures, historical reuse, all three measurement types, override/adjustment preservation, explicit verification, invalidation, insulin independence, ownership, spoof rejection, assistant serialization and quantity updates. PostgreSQL tests use `ASSISTANT_TEST_POSTGRES`, reject remote hosts and create/drop isolated databases.

Angular: 67 browser assertions passed, covering source labels, source versus current verification, deliberate verification UI, API payload, historical handoff and override clearing. The local Karma browser launcher remained alive after reporting its completed result and was stopped after observing the summary. `npm run build` passed; the existing Meal Assistant component CSS budget warning remains (4.80 kB versus 4.10 kB). `git diff --check` passed. All disposable PostgreSQL databases were cleaned up.

Verification is a user's review, not a claim of nutritional accuracy or medical suitability. This PR does not add food-library verification, per-item review history, a provenance audit trail or source-record links that survive deletion. A suitable next PR is intentional verified Food reference management with explicit portion scope and traceable edits, rather than automatically promoting historical values into personal food memory.
