# Personal Food Memory acceptance — PR #7

## Initial coverage audit

| Feature | Existing tests | Missing test / risk | Proposed coverage |
|---|---|---|---|
| A: Known Food round trip | UnifiedMealMatchingTests, MealServiceTests, Calculator specs | Persisted decimals and edited current references across API handoffs | Authenticated PostgreSQL search → draft → calculate → save → history → repeat |
| B: historical override/adjustment | MealServiceTests, CarbProvenanceTests | Full handoff may lose source IDs, overrides or adjustment | PostgreSQL multi-item repeat and browser historical navigation |
| C: structured Ask Past Me | StructuredDeliveryMealTests, structured-orders.component.spec | Mixed-source draft and persisted order references across layers | Three-unit order → search/select → quantity edit → mixed save |
| D: legacy orders | DeliveryMealTests, structured migration/reuse tests | Whole-order semantics through Assistant preparation | Legacy description/total retained, no fabricated children |
| E: ambiguity | UnifiedMealMatchingTests, structured matching tests | Cross-source choices and explicit ID resolution | Same-name Foods and restaurant items; no arbitrary selection |
| F: provenance | CarbProvenanceTests, verification API/component specs | Source verification versus current verification on repeat | HTTP verify/repeat/edit and rendered verification controls |
| G: concurrency/retry | AssistantPostgreSqlSafetyTests | Structured selection combined with pruning/replay | Two-instance race, changed source, pruned transcript, durable original response |
| H: isolation | AuthIntegrationTests, matching/structured tests | Entire second-user access/reuse path | Authenticated B attempts A's reads, verification and source selection |
| I: AI boundary | UnifiedMealMatchingTests, fake-HTTP OpenAiLlmClientTests | Call count across journeys; untrusted model IDs | Counting fake interpreter; deterministic/direct operations and unknown fallback |
| Database precision | Pure math, InMemory and relational tests | Inputs may exceed numeric column precision and drift on repeat | Real PostgreSQL quantity/reference round trips and rejection checks |
| Browser navigation/errors | Login navigation and isolated components | Joined real routes/AuthService/API services | Karma + RouterTestingHarness + real browser history + mocked HTTP |

Baseline is the inspected current branch: the recent UI report records 258 backend and 99 Angular cases. The worktree was clean. No standalone Playwright/Cypress setup exists. Use existing Karma and Angular routing tools for browser integration; do not describe mocked-HTTP browser journeys as live full-stack E2E. PostgreSQL tests use disposable localhost databases and a fake interpreter only. Execution results follow after validation.

## Executive summary

The requested A–I journeys are covered through composed PostgreSQL/API acceptance and browser routing tests, supplemented by the existing focused suites. One precision defect was reproduced before fixing it. Core current workflows pass; a live deployed full-stack/browser/provider acceptance session remains unverified. No Reusable Corrections, new provider, formula change, UI redesign, mobile packaging, schema change, commit or deployment was introduced.

## Executed scenarios

| Scenario | New executed acceptance test | Observed result |
|---|---|---|
| A | `Acceptance_KnownFood_SearchSaveHistoryAndRepeat_PreservePersistedReferenceAndRounding` | Authenticated Food at 23.37 g/100 g → deterministic 150 g selection → prepared/calculated 35.06 g → saved history. Editing the current Food to 99 does not change the historical repeat's 23.37 reference or 35.06 total. Source remains Manual plus Calculated/HistoricalReuse. |
| B | `Acceptance_HistoricalRepeat_OverrideAdjustmentVerificationAndMedicalValuesRemainDistinct` | Rice calculated 30 g with absolute override 25 g, bread 30 g, zero-carb chicken, adjustment −3 g: total 52 g. Unchanged repeat preserves 52 g. Rice at 200 g calculates 40 g but retains explicit override 25 g; explicitly clearing it yields 67 g. Original quantities/snapshots remain intact. |
| C | `Acceptance_StructuredAndLegacyOrders_SearchScaleMixPrepareAndSaveWithoutInventingData` | Three-unit order (Grams/Portion/Piece), lookup, item selection, quantity edit and saved Food combination yield a 60 g mixed saved meal with source identity/provenance. Original order stays 165 g. New meal uses explicit current glucose and no confirmed historical dose. |
| D | Same C/D test | Legacy 135 g order remains one whole-order row through search/preparation; free text and empty child collection remain intact. No individual values invented. |
| E | `Acceptance_AmbiguityIsolationAndUntrustedIds_RequireAuthorizedChoiceAndDoNotInventValues` | Two same-name Foods and two restaurant items produce unresolved issues/explicit candidates. Choosing a stable owned ID adds only the intended restaurant item. |
| F | B/F test and existing CarbProvenanceTests | Save/insulin confirmation does not verify carbs. Explicit verification persists; repeat retains source-meal verification lineage but is unverified itself. Editing a relevant original item invalidates its verification. PostgreSQL timestamps are compared after persisted reads at their native microsecond precision. |
| G | `Acceptance_StructuredConcurrentEditsAndPrunedReplay_CommitOnceAndReturnOriginalReceipt` | Two structured additions against one version commit once. After transcript pruning and a legitimate saved-order source edit, retry returns the original durable response without recalculating or creating messages. Same request ID/different payload is rejected. Existing concurrent same-ID and database uniqueness tests also passed. |
| H | E/H/I test and existing ownership tests | Authenticated B cannot read A's meal/order/draft, verify A's carbs, prepare A's items or submit A's IDs. B's Food/history collections stay empty; rejected responses do not expose A's restaurant data. |
| I | Counting fake interpreter in A/B/C/E cases | Known/direct/recent operations make zero calls. Natural-language quantity edit makes one; ambiguous planning and unknown fallback make one each. Foreign IDs supplied by model output are discarded and unknown foods produce issues instead of invented numbers. |

All example data is clearly synthetic and exists only in disposable local test databases. These assertions are not nutritional or insulin recommendations.

## Confirmed defect and fix

Application validation allowed quantities and carb references with more precision than the database's numeric(...,2) columns. Calculation happened before PostgreSQL rounded the stored reference/quantity, so an unchanged historical repeat could drift.

The new regression was run before changing code and failed with:

```text
Expected 400 for excess precision: quantity=Created, snapshot=Created, food=Created.
Stored quantity 0.13, saved carbs 2.50, unchanged repeat 2.60.
```

This used 0.125 portions at 20 g per portion. Raw snapshot reference 20.125 and Food reference 23.375 were also incorrectly accepted for two-decimal storage.

The minimal fix validates representability before calculation/persistence:

- MealCalculationService rejects quantities and active snapshot carb references with significant precision beyond two decimal places.
- MealService item updates reuse the same quantity validation.
- FoodService rejects excess-precision active carb references on create/update.
- Failures use the existing HTTP 400 ProblemDetails convention; values are not silently rounded or repaired.

`Acceptance_PrecisionInputs_CannotCreateSnapshotsThatDriftOnUnchangedRepeat` now passes. It covers Food create/update, meal create/calculate, item add/update and raw snapshot inputs, verifies failed requests leave no partial changes, and confirms supported fractional portions (0.25) save/repeat unchanged at 5 g. Trailing zeros such as 1.250 remain representable. Existing rounding formulas, override precedence/rounding, valid zero carbs, insulin rules and schema were not changed.

No additional application defect was confirmed. Legacy numeric data already rounded in prior versions is not automatically repaired: lost digits cannot safely be reconstructed.

## Database and backend results

Command executed with ASSISTANT_TEST_POSTGRES pointing to the existing local instance at 127.0.0.1:5433:

```powershell
dotnet test InsulinAndCoffee.sln --configuration Release --no-restore --verbosity minimal
```

- **264 passed, 0 failed, 0 skipped**: 258 existing + 6 new acceptance cases.
- **23 real PostgreSQL cases** (17 existing + 6 new). Remaining 241 cases cover pure calculations, InMemory application behavior and other API contracts.
- Existing relational tests verify actual migrations/legacy compatibility, structured parent-child relations, optional Food unlinking, cascade deletion, unique UserId/receipt indexes, concurrency, atomic updates, transcript pruning and startup in Production environment.
- New cases verify persisted decimals, snapshots, authenticated ownership, composed reuse, verification and receipt behavior. InMemory results are not used to claim PostgreSQL behavior.
- The local-only fixture rejects remote hosts and creates/drops `iac_assistant_pr3_<guid>` databases. No hosted database or application database was migrated. Cleanup was checked separately.

The test factory can now optionally receive an ILlmClient test double; no production provider registration changed. The existing CI backend job already provisions PostgreSQL 16 and sets ASSISTANT_TEST_POSTGRES, so it picks up these cases automatically.

## Angular and browser results

`npm test -- --watch=false --browsers=EdgeHeadlessNoSandbox`: **104 passed**, including 99 existing tests and 5 new routed browser cases.

`personal-food-memory.journeys.spec.ts` uses actual routes, guards, AuthService, auth/error interceptors, ApiService, Assistant, Calculator, History, MealDetails and Ask Past Me components. RouterTestingHarness uses BrowserPlatformLocation to exercise the actual history.state handoffs. HTTP responses are supplied by HttpTestingController; these are browser integration tests, not live full-stack E2E.

Executed new journeys:

1. Login → authenticated search → explicit selection → draft quantity edit → preparation → Calculator's 5.5 default → explicit entered glucose → save → details → intentional carb verification → History.
2. Historical reuse with complete snapshots, source IDs, override and adjustment; quantity edit and explicit override clearing; historical glucose/insulin ignored.
3. Network failure preserves input/request identity; retry uses the same envelope; conflict blocks resubmission until refresh, then uses a new version and request ID.
4. Current compact Ask Past Me editor previews and explicitly accepts a changed total, sends a null optional note, preserves stable child IDs, then transfers only selected items to Calculator.
5. Precision error remains on Calculator, preserves entered quantity, clears stale calculations and recovers after an explicit valid edit.

The existing UI geometry, optional-note, equal-override and responsive-frame tests also passed. No product UI changes were needed for this PR.

`npm run build` passed. Existing Meal Assistant CSS budget warning remains: 4.80 kB versus 4.10 kB. `git diff --check` passed. The local headless launcher emits retries and stays alive after a completed summary; success is based on the connected Edge browser's final `TOTAL: 104 SUCCESS`, after which the process was stopped. Task-local profiles/logs were cleaned up.

The frontend CI job previously only built Angular. It now discovers an installed Chrome/Chromium binary and runs the same Karma suite with ChromeHeadless. No new package or framework migration was added. **The GitHub workflow was not triggered or observed here**; local tests ran on Edge, not Linux Chrome.

## AI invocation checks

| Operation | Asserted interpretation calls |
|---|---:|
| Known Food lookup/direct selection/preparation/save/repeat | 0 |
| Recent history and historical override/verification journey | 0 |
| Structured lookup/direct selection/mixed addition/legacy reuse | 0; quantity-edit natural-language fallback adds exactly 1 |
| Ambiguous planning; unknown fallback | 1 each; explicit choice adds 0 |
| Concurrent structured direct selections and replay | Stub throws if interpretation is attempted; passed without invocation |

Existing mocked-HTTP OpenAiLlmClientTests passed for allowed/malformed output, failures and timeouts. Model source IDs cannot establish ownership or verification. Existing strict provenance/verification DTO tests also passed. No paid OpenAI call was made. These tests validate routing and trust boundaries, not every real-model phrasing.

## Changed files and deployment

- `backend/src/InsulinAndCoffee.Application/Services/FoodService.cs`
- `backend/src/InsulinAndCoffee.Application/Services/MealCalculationService.cs`
- `backend/src/InsulinAndCoffee.Application/Services/MealService.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AssistantPostgreSqlSafetyTests.cs` (partial fixture reuse and optional fake interpreter)
- New `backend/tests/InsulinAndCoffee.Application.Tests/PersonalFoodMemoryAcceptanceTests.cs`
- New `frontend/src/app/personal-food-memory.journeys.spec.ts`
- `.github/workflows/ci.yml` (run the existing frontend browser suite)
- This report.

Deploy the backend validation fix normally. No new migration, manual data repair, commit, push or deployment was performed in this task.

## Remaining risks, readiness and next phase

Core Personal Food Memory journeys are verified across the current layers and ready for controlled review-environment acceptance. Remaining gaps are explicit:

- No single live deployed Angular → backend → PostgreSQL browser E2E environment was exercised. Add a small isolated review-environment smoke suite with synthetic accounts and a deterministic test interpreter as the next testing increment.
- No paid-provider, physical-phone/keyboard, production account or deployed-version acceptance session was performed.
- Previously rounded historical inputs are not repaired automatically; suspected mismatches need explicit review.
- The Linux CI browser step is newly configured but not remotely verified in this task.
- Separate typed browser/backend fixtures do not replace checking that compatible frontend/backend versions are deployed together.

Keep Reusable Corrections postponed until these checks run reliably in CI and the controlled deployment smoke path is established. No correction learning or automatic trust promotion was added.
