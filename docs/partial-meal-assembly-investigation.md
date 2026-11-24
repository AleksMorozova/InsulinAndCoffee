# Recent Meal Assistant failures: investigation and verification

## Evidence and limits

The supplied conversation examples were traced through the current repository and
reproduced with representative, owner-scoped fixtures. Original production request
IDs, provider tool arguments, Render request logs and a read-only production data
snapshot were not available in this session. No production database or historical
record was accessed or changed. **A reproduced mechanism is not conclusive proof
of the original production execution.**

The actual model separates `DeliveryMeal` (place/order) from `DeliveryMealItem`
(individual position with its own saved quantity, reference, override and origin).
Items inherit ownership through `DeliveryMeal.UserId`; `FoodItemId` is optional.
Current Meal is conversation-specific JSON (`AssistantConversation.DraftJson`),
protected by draft versions, optimistic conversation revisions and durable request
receipts. It is not a global meal and does not create Meals until explicitly saved.

Totals in the tests distinguish a fresh meal from an existing one: a fresh
California-plus-unresolved-potatoes draft has 60 g known carbs. If the 30 g buckwheat
draft from A is already present, a normal addition retains it and the known subtotal
is 90 g. Entering a synthetic 25 g potato total then produces 85 g or 115 g,
respectively. Replacing the earlier meal is never inferred from an ordinary add.

## Cases A–D

| Case | Current path and observed local result | Root-cause assessment and change |
| --- | --- | --- |
| A: `добавь 150 грамм гречки` | `AssistantIntentText.Interpret/Parse` → `AssistantPortionText.Normalize` → `AssistantFoodMatching.Query/Terms/Score` → `AssistantDraftBuilder.BuildAsync` → Foods reference → `MealCalculationService.CalculateItemsAsync`. The synthetic 20 g/100 g reference produces 30 g for 150 g. | Working reference case, not a failure. The old numeric parser did not itself recognize every full gram-unit spelling, but downstream portion normalization recovered this form. Full spellings are now parsed directly; formulas and saved references are unchanged. |
| B: `хочу съесть калифорнию и 120 жареной картошки` | The old deterministic parser did not recognize this meal-building verb phrase, so `MealAssistantService.SendAsync` used the provider. A representative extraction `Name=роллы калифорния, Restaurant=Калифорния` reproduces the exclusion mechanism behind the reported order-clarification text: `FindItems` applies the restaurant filter before name ranking, excluding Le Gril's item; the builder then tries whole orders. Separately, a provider `create` replaced an existing draft, including when nothing was resolved. | Confirmed code-level failure mechanisms, reproduced with fixtures; the original tool arguments remain unconfirmed. Explicit eating requests now parse item-by-item without a provider. Unregistered implicit restaurant hypotheses cannot hide an item; explicit location constraints are retained and never relaxed to another restaurant. Category-qualified item names use item-specific ranking. Generic provider `create` appends to an existing meal; whole-meal reset requires an explicit request. Each unresolved item is retained independently. |
| C: `сколько брала на калифорнию` | In the inspected baseline this already routes deterministically to `lookup`; `FoodKnowledgeSearchService.SearchAsync` searches child items, Foods and orders, alongside the historical-meal search. Representative fixtures return the saved 60 g item without a draft mutation. | The earlier production failure was **not reproduced on the baseline**. A history-only intent would omit the saved-reference search, consistent with the older English no-match message, but that is a hypothesis without the original routing trace. Existing lookup routing is preserved and regression coverage expanded. |
| D: `добавь 120 гречневой каши` | The baseline already extracted quantity 120, matched the existing buckwheat alias family and used the saved food's measurement model. It resolves locally. | The earlier failure was **not reproduced on the baseline**. Missing aliases, an older deployment or different extraction cannot be distinguished from the provided text. New tests cover both forms, explicit and implicit units, and record inferred-unit metadata; no nutrient defaults were introduced. |

The error wording in B identifies an order-resolution branch, but does not establish
why the provider produced its restaurant/name fields. The new tests additionally
cover an arbitrary item named `Лос-Анджелес` requested with a roll-category prefix:
the fix is not restricted to California or buckwheat synonyms. Bare saved item and
order/place references are also discovered from owner-scoped records, rather than
requiring an entry in a hardcoded alias list. A food/restaurant name collision is
not treated as proof of restaurant intent, even if that place exists. Category relaxation
for arbitrary names is limited to saved **order items**, not generic Foods.

## State and resolution changes

`AssistantDraft.Unresolved` contains stable per-item IDs, original requested text,
the extracted query/quantity/unit, an inferred-unit flag, a reason enum and typed
candidate IDs/ranks/context. Existing `Issues` and `Pending` remain for compatible
clients. New fields are optional; no existing field or result-status value was
removed or renamed (`NoMatchesFound` retains its existing API spelling).

- Add preserves existing items and other unresolved items.
- Lookup does not mutate the draft or increment its draft version.
- Quantity/remove edits retain existing version checks and snapshot validation.
- Provider `create` cannot erase an existing meal. Explicit whole-meal replacement
  is handled internally as `replaceMeal`; clear/reset requires an explicit request.
- Clarification targets one unresolved ID, leaving the rest of the meal intact.
- Weak matches are offered for explicit selection, never silently promoted to
  trusted nutrient references. Candidate context distinguishes restaurant orders.
- Missing items have no calculated carbohydrate row and contribute no invented
  value. The displayed total is the **known** subtotal, with unresolved items shown
  separately. A unitless large unresolved amount can suggest grams, visibly marked
  inferred and editable; saved-source unit compatibility is still validated.
- A same-name search retains an explicit restaurant constraint. Changing the search
  name or explicitly selecting a source is a separate user action.

The existing `POST /api/meal-assistant/messages` accepts optional `resolution`:

```json
{
  "conversationId": "CURRENT_CONVERSATION_GUID",
  "message": "Уточнить позицию",
  "requestId": "NEW_OPERATION_GUID",
  "expectedDraftVersion": 1,
  "resolution": {
    "unresolvedId": "PENDING_ITEM_GUID",
    "action": "manual",
    "quantity": 120,
    "unit": "Grams",
    "carbs": 25
  }
}
```

Actions are `search`, `select`, `update`, `manual`, `remove`. `select` accepts
`sourceId` and `sourceKind` (`Food`, `DeliveryMealItem`, `DeliveryMeal`); source
ownership is revalidated. `search`/`update` can include `searchName`, quantity and
unit. All mutations require an operation/request ID and expected draft version.
Resolution fingerprints normalize decimal formatting; replay returns the original
response without another addition, and changed content under the same completed
request ID is rejected. Resolution cannot be combined with selection/indexed edit.

Manually entered carbs are an explicit item override, marked user-provided. A
rounded reference is derived from the supplied total and quantity using a pure
Domain helper because the existing snapshot model requires a reference. This is
not an AI or generic nutrition estimate; the explicit total override remains
authoritative even if reference rounding differs. Quantity, units, zero values,
precision and storage bounds are validated. No Food is created. Calculator uses
the same snapshot, quantity and override through existing backend calculation rules.

Legacy string-only pending drafts are projected into structured entries on read
without modifying historical responses. Original queries are recovered from the
retained user turn when possible; IDs are stable. If extraction/text is unavailable,
the UI asks for the missing name/quantity instead of inventing it. Recovery is
persisted only when the owner explicitly edits their draft.

## Diagnostics and privacy

Existing `ILogger` and `AiRequestContext` are reused, sharing correlation,
conversation and operation IDs with AI Monitoring. There is no new log database,
cache or observability service. Deterministic matching/clarification creates no
fictional provider usage record.

Information logs include intent/draft operation, source types queried, candidate
counts/IDs/types/scores, selected ID/type, ambiguity/resolution reason, assembly
status/counts, proposed draft version and durations. They exclude food names,
carb values, complete prompts, notes, glucose, insulin and provider credentials.
Privacy regression tests check the default matching log output.

Scoped Debug logging for
`InsulinAndCoffee.Application.Services.AssistantDraftBuilder` additionally captures
extracted names, quantities, units, normalized terms/aliases and restaurant
hypotheses. Names are bounded; full prompts/records are never serialized. Use this
only for controlled local/anonymized reproduction. Food names and quantities can
be private; do not enable broad production Debug logging as a default. Routine
diagnosis should start with default metadata and owner-supplied request IDs.

For future unexplained production failures collect: deployed build SHA, correlation
ID, request/operation ID, conversation ID, expected/returned draft versions,
intent/operation, resolution metadata and candidate IDs/ranks; then a read-only
inventory of the relevant owner-scoped sources. The original extracted names and
restaurant hypothesis require a controlled trace if not already retained. Cost
monitoring intentionally cannot reconstruct prompts/tool arguments.

## UI scope

Only Current Meal clarification controls and response semantics changed. The compact
chat, sidebar, result cards, admin page and navigation are retained. Resolved items,
unresolved original requests, known total and partial status are distinct. Each
missing row supports search, quantity/unit correction, typed source choice, manual
total and removal. The partial Calculator action is compact and explicitly opens
known items only; no misleading disabled full-width action is shown when no items
can transfer. Existing incomplete-draft validation/handoff behavior is retained.
Manual overrides remain visibly user-provided. No glucose or insulin logic changed.

A phone regression exposed the existing floated summary overlapping the new fields.
Clearing that float before the item body fixes the width without redesigning the
page. Responsive tests exercise 1440, 1024 and 390 px viewports, independent scrolling
and composer visibility.

## Validation and rollout

Final results: **322 backend tests passed, 0 failed, 0 skipped; 123 browser tests
passed, 0 failed.** The production Angular build passed. CSS budget warnings remain
for Assistant (6.06 kB versus 4.10 kB) and Ask Past Me (5.77 kB versus 4.10 kB).
The backend retains the pre-existing nullable warning in `StructuredDeliveryMealTests`.
`git diff --check` passed. No commit or deployment was created.

Checks run from the repository root (frontend commands from `frontend`):

```powershell
$env:ASSISTANT_TEST_POSTGRES='Host=127.0.0.1;Port=5433;Database=postgres;Username=postgres;Password=postgres'
dotnet test InsulinAndCoffee.sln --configuration Release --no-restore --verbosity minimal
cd frontend
npm test -- --watch=false --browsers=EdgeHeadlessNoSandbox
npm run build
```

Backend coverage includes A–D, arbitrary category-prefixed item names, typed
order/item ambiguity, multiple unresolved items, weak alternatives, explicit place
constraints, existing meal preservation, explicit clearing, canonical safe retries,
manual precision/zero/negative validation, source overrides, legacy recovery and
default log privacy. Existing concurrency and owner-isolation regressions remain.

The local PostgreSQL HTTP acceptance test follows the same endpoints as Angular:
save a representative Le Gril → Суши order with independent California and sushi
items; lookup California; assemble California plus unavailable potatoes; reopen the
conversation; resolve the 120 g potato item with a synthetic 25 g manual total;
verify 85 g; replay/tamper-check; prepare Calculator; calculate and explicitly save
a test meal. Foods remains empty and the 135 g source order remains unchanged.
All records and synthetic values exist only in disposable local test databases.
No paid provider calls were made.

No migration or new environment variable is required: the new state uses existing
conversation JSON. Deploy backend before frontend. For safe production verification
use a dedicated test account/new conversation, confirm source IDs and known totals,
then perform per-item clarification and inspect Calculator without saving a real
Meal or confirming insulin. Compare source records before/after; check logs by
correlation ID. Do not replay an old completed operation with a new ID expecting
idempotency, or automatically rewrite old historical results.

Remaining limits: complex unsupported language still uses the existing provider
interpreter or needs explicit clarification. Old requests cannot be replayed as
their original provider interpretation without that trace. Missing original legacy
text/quantities must be supplied rather than reconstructed speculatively. Browser
coverage uses real Angular components/services with mocked HTTP; the separate API
acceptance uses real local PostgreSQL. A live production/browser-to-provider replay
was not performed.

## Files

- Application DTOs: `backend/src/InsulinAndCoffee.Application/Dtos/MealAssistantDtos.cs`
- Assembly/clarification and tracing: `Services/AssistantDraftBuilder.cs`
- Intent, alias, typed search and grounding: `Services/AssistantIntentText.cs`,
  `Services/AssistantFoodMatching.cs`, `Services/FoodKnowledgeSearchService.cs`,
  `Services/MealAssistantService.cs` (all under the Application project)
- Localized reasons: `Services/AssistantLanguage.cs`
- Manual-reference helper: `backend/src/InsulinAndCoffee.Domain/Calculations/FoodCarbCalculator.cs`
- Backend tests: `AssistantUxReliabilityTests.cs`, `UnifiedMealMatchingTests.cs`,
  `PartialMealAssemblyAcceptanceTests.cs` under the Application.Tests project
- Angular contracts/transport: `frontend/src/app/core/meal-assistant.models.ts`,
  `frontend/src/app/core/api.service.ts`
- Current Meal UI/tests: `frontend/src/app/pages/meal-assistant.component.ts`,
  `frontend/src/app/pages/meal-assistant.component.spec.ts`
- Explicit-override label: `frontend/src/app/shared/carb-provenance.component.ts`
- This report: `docs/partial-meal-assembly-investigation.md`
