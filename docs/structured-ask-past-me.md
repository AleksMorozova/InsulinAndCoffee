# PR #6 — Structured Ask Past Me items

## Existing architecture and implementation

Ask Past Me is the existing DeliveryMeal aggregate, separate from actual Meal History. Previously it stored restaurant/order descriptions, a whole-order carb total, optional recorded insulin/glucose, favorites and usage counters. Discovery could find orders, but AssistantDraftBuilder represented an order as one fixed portion; neither it nor Calculator could safely derive individual values from free text.

This change extends that aggregate. It reuses MealCalculationService and the existing FoodCarbCalculator for all gram, portion and piece math. Controllers only delegate workflows. Application persistence remains behind IAppDbContext; EF configuration and migration remain in Infrastructure. No formula, auth workflow, LLM provider, or additional LLM call was introduced.

## Data model and migration

`DeliveryMealItem` has a stable UUID, parent DeliveryMealId, optional FoodItemId, Name, Quantity, MeasurementType, CarbsPer100gSnapshot/CarbsPerUnitSnapshot, CalculatedCarbs, CarbOverride, CarbReferenceSource, CarbTransformation, SourceMealVerifiedAt, and DisplayOrder. Effective carbs are the override when present, otherwise calculated carbs; they are returned in DTOs rather than redundantly persisted. Ownership is inherited through the parent.

`20261121154646_AddStructuredDeliveryMealItems` adds the child table and DeliveryMeals.Revision (bigint, default 0, optimistic concurrency token). Children cascade when their order is deleted. Deleting an optional linked Food sets FoodItemId to null and leaves frozen reference values intact. Parent/order-position and optional Food indexes support access. Quantity uses numeric(8,2), gram basis numeric(7,2), and per-unit basis/calculated carbs/override numeric(8,2). Input quantities and carb references must fit two decimal places; missing references are validation errors, not zeros. Zero references and zero-total structured orders are supported.

No legacy description is parsed, split, translated, backfilled, or verified. Existing order totals, insulin, glucose, counters, descriptions and historical meals are untouched by UP. The generated empty seed UpdateData operations were removed from this new migration; the remaining UP is additive. The migration and snapshot were validated against real PostgreSQL, including migration from the previous schema.

## Totals and edits

Structured total = rounded sum of existing engine-calculated effective item carbs. There is no new order adjustment or whole-order override field; existing whole-order Carbs is kept for legacy records. A request whose Carbs differs from its structured item sum is rejected with HTTP 400, including the conflicting values. Updating/converting an existing order to a different total also requires `acceptCalculatedTotal: true`. The UI shows the saved total, item total and difference; clicking “Use calculated item total” is explicit acceptance. Differences are never distributed across items.

Named rows can be added/removed, reordered through array order, edited and rescaled. Existing child UUIDs must belong to the current order and cannot repeat; new rows receive server IDs and explicit EF Added state. Missing old UUIDs remove those child rows only. An existing structured order must retain at least one item. Omitting `items` preserves a structured collection and cannot change its total. Saving is transactional through the existing EF SaveChanges workflow.

Structured updates require the returned revision. Stale requests or competing tracked edits return HTTP 409 ProblemDetails, `code: saved_order_changed`. A concurrent losing save rolls back child and parent changes together. Legacy clients updating legacy records can continue omitting items/revision. Normal legacy creation and whole-order reuse remain supported.

## API changes

Existing routes remain in place. Additive fields:

- DeliveryMealDto: `items`, `revision`.
- UpsertDeliveryMealRequest: optional `items`, `expectedRevision`, `acceptCalculatedTotal`.
- Item input: optional `id` and `foodItemId`; `name`, `quantity`, `measurementType`, `carbsPer100g`/`carbsPerUnit`, optional `carbOverride`.
- UseDeliveryMealDto: optional prepared `items`; null for a legacy order.
- Calculator input/output: `sourceDeliveryMealItemId` identifies a trusted saved child snapshot. It cannot be combined with another source ID on the same input.
- Assistant knowledge: `items` with parent restaurant/order context and reference units. Selection supports `kind: Food` (existing default), `DeliveryMealItem`, or `DeliveryMeal`; its `foodId` identifies the selected record for that kind.

New routes:

- `POST /api/delivery-meals/preview-items`: `{ items, savedTotal? }` → server-calculated rows, total and optional signed difference. It never saves records.
- `POST /api/delivery-meals/{id}/items-draft`: `{ itemIds, expectedRevision }` → prepared rows from that owned order. Empty, duplicate, foreign or unavailable IDs are rejected.

All incoming IDs are ownership-checked. Provenance/timestamps are response metadata; arbitrary incoming item provenance is rejected. Food links must be owned. A manual reference is Manual; a matching linked Food reference retains its stored origin. Overrides remain separate and contribute Manual to the order's effective aggregate origin. New structured records are not automatically verified. Any retained source-meal verification describes past lineage, never verifies a new portion or meal.

## Assistant and Calculator lifecycle

Create order → server preview → accepted save → item/restaurant search → stable-ID selection → existing versioned Assistant draft → prepared Calculator rows → existing meal save and historical snapshots.

Discovery searches child names and existing restaurant aliases. Restaurant-only discovery lists structured children. Resolution uses the existing scored matching; equal matches or a matching saved Food and order item require a choice instead of selecting one silently. Legacy free text is never used to fabricate item references.

Direct item/order buttons bypass interpretation and use the existing request IDs, durable receipt replay, version checks and ownership validation. The default Food selection hash remains compatible with existing receipts. LLM-interpreted IDs are stripped; only explicit structured selections or deterministic owned-record resolution establish source IDs. Natural-language requests continue using the existing interpreter to extract names, quantities and restaurant terms.

Whole structured orders expand to their individual rows. A whole legacy order stays one fixed portion. A draft can combine Foods, items from multiple orders and legacy complete orders. Gram weights, fractional portions and integer pieces are recalculated by the existing engine. An item override remains an **absolute** carb override when quantity changes, consistent with existing Calculator behavior; clearing it explicitly restores calculated carbs. No AI calculation is involved. A source whose basis/name/inherited override changed cannot silently replace a persisted draft during a quantity edit; preparation also rejects changed numeric snapshots.

“Use only the second item from my previous order” is handled without an LLM call when one order is unambiguously identified from saved conversation results, or when the account has only one order. First/second/third and supported Russian ordinal phrases are recognized. Multiple possible orders, absent positions and legacy-only orders require clarification; no latest-order date is guessed. Broader ordinal phrasing remains subject to the existing interpreter and may require explicit selection.

Calculator receives complete item snapshots and source IDs, allows edits and existing saved Food additions, and saves new MealItem snapshots through existing workflows. Later order/Food edits do not rewrite those saved historical values. Ask Past Me whole-order counters/favorites and existing recorded medical values remain available. Recorded insulin/glucose is never used as a new dose/current measurement. The 5.5 mmol/L unconfirmed-current-glucose behavior and explicit-save safeguards remain unchanged.

The existing “save from historical meal” operation remains its legacy whole-order workflow; it does not automatically turn an adjusted historical total or free text into structured items.

## UI and examples

The existing Ask Past Me modal now has optional item rows with name, unit, quantity, carb basis, override, backend-calculated/effective carbs, add/remove controls, total/difference review and save errors that preserve input. Legacy whole-order entry stays available. Existing descriptions stay separate from item composition.

Cards show item quantities and effective carbs with provenance, individual reuse buttons and multi-selection. Whole-order reuse prepares editable rows for structured orders. Assistant result cards expose item-specific choices and whole-order addition. Calculator receives all selected rows without historical insulin/glucose. The editor grid wraps using auto-fit/minmax for narrow layouts; no separate page or new visual design was introduced.

Before: Le Grill, “California + five sushi”, 135 g could only be reused as one order.

After: California, 1 portion × 60 g = 60 g; sushi, 5 pieces × 15 g = 75 g; order total 135 g. Selecting three sushi pieces gives 45 g. Combining those with 150 g rice at 20 g/100 g gives 75 g. Editing the saved whole order from five to three sushi pieces shows 135 → 105 g and difference −30 g before acceptance. These are synthetic test examples, not nutritional or insulin recommendations.

## Validation

- Full backend suite with isolated localhost PostgreSQL: **255 passed, 0 failed/skipped**.
- Focused structured-order suite: **13 passed**; the full suite was rerun after final refinements.
- Coverage includes all three units, fractional portions, invalid fractional pieces, zero/missing/negative carb data, overrides, explicit legacy-total conversion, stable identities, historical snapshot preservation, provenance, multiple/mixed sources, ambiguous matches, ordinal selection, ownership, forged metadata, stale revisions, durable duplicate-request replay and no automatic historical insulin/glucose reuse.
- PostgreSQL coverage includes migration from the previous schema, untouched legacy records, legacy-to-structured conversion, child insertion, concurrent updates, optional Food unlinking, cascade deletion and model/migration alignment. Test databases were disposable; the application database and hosted databases were not migrated.
- Angular suite: **88 passed**. Added coverage includes item creation/editing, changed quantities and total acceptance, row add/remove, failed-save input preservation, single/multiple selection payloads, whole structured order and legacy navigation, Calculator hydration, structured-selection retry, and no editor overflow at 340 px content width.
- Production `npm run build`: passed. Existing Meal Assistant component CSS warning remains (4.80 kB vs 4.10 kB).
- `git diff --check`: passed. System-temp headless launcher initially failed; final Angular tests used a workspace-local temporary directory and reported success. Karma was stopped after its final completed summary.

## Changed files and deployment

- Domain: DeliveryMeal.cs; new DeliveryMealItem.cs.
- Application: IAppDbContext.cs; new DeliveryMealConflictException.cs; CommonDtos.cs, MealAssistantDtos.cs; DeliveryMealService.cs, MealCalculationService.cs, AssistantDraftBuilder.cs, FoodKnowledgeSearchService.cs, MealAssistantService.cs.
- API: DeliveryMealsController.cs, GlobalExceptionHandler.cs.
- Infrastructure: AppDbContext.cs, DeliveryMealConfiguration.cs; new DeliveryMealItemConfiguration.cs; new migration/designer and AppDbContextModelSnapshot.cs.
- Angular: core models, assistant models, API service/spec; Ask Past Me component and new structured-order spec; Calculator component/spec; Meal Assistant component/spec.
- Backend tests: new StructuredDeliveryMealTests.cs; AssistantPostgreSqlSafetyTests.cs; AuthIntegrationTests.cs.
- This document.

Deploy the backend migration before the new frontend. The existing application startup migration process is unchanged; no deployment was performed. No commit or push was created.

Remaining validation limits: no external OpenAI provider calls, signed-in production/browser acceptance session, or physical-phone viewport/keyboard test was performed. Unit/component tests validate UI behavior and responsive CSS was added, but device-level layout should be checked before release. Food linking is supported by the API; this modal provides manual item references rather than a new Food-library linking picker. Per-order/food verification is not introduced; the existing explicit meal-snapshot verification remains the verification workflow.
