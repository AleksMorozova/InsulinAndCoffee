# Safe meal deduplication — 2026-11-25

## Findings

This extends the existing [meal-operation investigation](meal-operation-reliability.md).
No production conversation trace was supplied; the exact original California/Sushi
click sequence remains unverified. The following causes are reproduced from code:

- California: separate successful add operations append separate entries, which is
  intentional and must remain supported. Old name-only quantity matching could not
  distinguish these from accidental copies. Source-aware explicit final-quantity
  reconciliation from the previous change remains in place.
- Add-button double clicks: the in-flight guard alone was insufficient when the
  first response completed before the second click. That second click could create
  a new RequestId and another legitimate backend add. All add/whole-order buttons
  now ignore the second click in a browser double-click gesture. Independent single
  clicks and keyboard activation remain supported, without a delay or cooldown.
- Sushi: a provider could extract one mentioned food twice, for example `СУШИ` and
  `sushi`. `BuildAsync.Reject` previously generated a new pending ID for each query,
  producing two clarification forms for the same logical request. This is now
  corrected before food matching when there is evidence of exactly one request.
- Ambiguity labels: `DescribeMatchesAsync` accessed a delivery navigation inside
  `ToDictionaryAsync`'s client-side selector without loading it. A real PostgreSQL
  test reproduced a null-reference failure. The query now projects restaurant and
  order labels in SQL, and no longer displays source/entry UUIDs to users.

Inspection of all insertion paths:

| Path | Result |
| --- | --- |
| Natural-language add | One operation adds its requested entries once; a distinct explicit add is preserved. Extra servings and remove-one commands now have deterministic grammar even without an explicit unit word. |
| Manual Add item / whole historical order | In-flight and fast-response double-click regressions send one envelope per gesture. Whole-order child items are never collapsed by name. |
| AI structured extraction | Only repeated equivalent extraction for one explicit mention is corrected; see the conservative limits below. |
| Pending resolution | Stable operation/request-slot identity; repeated matching updates the same pending entry. Resolution moves it out of pending and keeps its ID on the resulting entry. |
| Retry | Existing `(UserId, RequestId)` durable receipt returns the stored result. No second mutation. |
| Reload / hydration | Reads replace client state; there is no optimistic item append. Detection adds hints but neither merges nor removes food records on load. |
| Concurrent mutation | Existing expected version/revision tokens and transaction/receipt uniqueness remain authoritative. New PostgreSQL concurrent-combine test commits once. |

## Three distinct cases

**Technical duplicates:** same-operation retries and double-click gestures are
prevented by the existing envelope/receipt guards. Repeated AI extraction of a
single requested item is filtered before creating items or pending forms. Pending
matching retains the same lifecycle identity instead of generating another form.

**Intentional repetitions:** separate adds, multiple explicit mentions and explicit
quantity/repeat cues remain intact. “Добавь ещё одну Калифорнию” increases a compatible
entry by one stored unit. “Убери одну Калифорнию” subtracts one compatible unit,
including across equivalent separate entries. Neither means deleting all servings.
Gram-based foods require a unit rather than interpreting “one” as one gram.
“Хочу две порции шаурмы” preserves two servings.

**Potential accidental duplicates:** same-source groups are detected but retained.
A compact, collapsed source disclosure offers Keep separate and, only when safe,
Combine quantities. No action is selected by default. Ordinary meals without a
group get no deduplication controls. Existing exact-row remove controls remain
available. Keep separate is persisted in the draft; a later newly added entry can
require a new decision. Different sources remain distinct even when names match.

## Conservative extraction correction

`AssistantIntentText.RemoveRepeatedExtraction` applies only to model-generated
create/add results. It is not a food-record deduplication algorithm.

It requires one explicit alias-family mention in the original message and equivalent
normalized query family, restaurant constraint, quantity, unit and source fields.
It skips explicit lists, repetition words, numeric quantities and multiple mentions.
Deterministic user lists, manual selections and separate operations are not filtered.
If intent or equivalence is uncertain, entries are retained rather than guessed away.
No nutrient values or historical records are changed by this correction.

## Stable pending identity

Each new pending entry records `OperationId` and `RequestedItemId`. The public
mutation OperationId remains the existing `RequestId`; it is not a second alias or
idempotency mechanism. RequestedItemId derives from the operation and requested
item slot, and is also the pending ID. Thus two separately requested Sushi items
have different identities, including when their names differ only by spelling or
transliteration. Two intentionally repeated items within one operation have
different slots. Retrying matching retains the original identity and origin.

The UI distinguishes multiple pending requests with simple numbered item labels
and accessible groups. It never displays operation IDs or raw pending UUIDs.
Removing a pending entry persists its removal and does not resurrect it on reload.

## Safe reconciliation rules

The backend checks source kind and all saved source IDs, Food ID, measurement unit,
reference values, provenance and overrides. An override must have a compatible
original quantity. Stored calculated/effective values must agree with their saved
reference and override; unexplained differences are preserved, not normalized away.

- **Set final quantity:** an explicit “Калифорнию оставь одну порцию” can reconcile
  equivalent entries to exactly one serving. It never sums old quantities for SET.
- **Combine:** only an explicit combine operation sums quantities. The existing
  calculation pipeline must reproduce the original calculated and effective carb
  totals exactly; rounding changes block combination. Override totals are scaled
  through the existing decimal helper only as part of the explicit action.
- **Keep separate:** preserves every item and value and acknowledges the group in
  persisted draft state. No source record or food is updated.
- **Remove all:** “Убери все Калифорнии” removes the compatible logical scope.
  Different sources, incompatible units or conflicting user values require
  clarification rather than assuming a broader scope. Unrelated items remain.
  A provider cannot invent remove-all intent for an unrelated user message.
- **Different carb values / uncertain portions:** no automatic combination or
  overwriting. Numbered source/quantity/unit/carb/provenance alternatives and exact
  row controls let the user distinguish entries without technical UUID labels.

There is no per-item CarbAdjustment field in the assistant draft model. Calculator
meal-level adjustment behavior is unchanged. Mismatching references, effective
values or overrides prevent reconciliation; a new adjustment representation was
not invented or folded into item carbs.

## Persistence and deployment

The added metadata is stored in the existing draft JSON. There is no schema change,
bulk cleanup or EF migration. Existing drafts keep their items on reads. Backend
and frontend contract additions are synchronized. Deploy both normally.

New optional draft fields: `duplicateGroups` (`draftItemIds`, `canCombine`) and
`separateItemIds`. New structured edit actions: `combine` and `keepSeparate`, using
the existing DraftItemId, RequestId and expected-version safeguards. Pending items
add optional `operationId` and `requestedItemId`. Existing clients and legacy
receipt fingerprints remain supported.

## Validation

Tests cover all requested categories: network retries; state hydration; in-flight
double-clicks; intentional adds; explicit final quantity; remove-one/all; differing
source IDs/types, overrides and units; safe combine; stable pending matching and
separate pending requests; resolution/removal; reload; concurrency; provenance;
historical preservation; and Calculator transfer.

Key fixtures:

- The previous California reconciliation still produces 78 g and survives HTTP
  reload/retry/Calculator preparation.
- Le Gril Sushi (5 pieces, 75 g) and another order's Sushi (1 serving, 60 g) remain
  distinct at 135 g. Ambiguity names both restaurants. Saved order/item values remain
  unchanged, and Calculator receives both entries and 135 g.
- Concurrent identical combine operations produce one combined row and one receipt.
- Same-source conflicting overrides or units cannot combine; rounding or unexplained
  effective-carb differences also block combination.
- One repeated Sushi extraction gives one pending form, while two independent Sushi
  requests retain both forms with distinct identities.

All database tests use guarded, isolated localhost PostgreSQL databases which the
test fixtures remove. No production/staging database or real user's meal was changed.

Validation commands (PowerShell):

```powershell
$env:ASSISTANT_TEST_POSTGRES = 'Host=127.0.0.1;Port=5433;Database=postgres;Username=postgres;Password=postgres'
dotnet test InsulinAndCoffee.sln --configuration Release --no-restore
Set-Location frontend
npm test -- --watch=false --browsers=EdgeHeadlessNoSandbox
npm run build
```

Backend: **361 tests passed, zero skipped**, including isolated PostgreSQL tests.
Frontend: **141 tests passed**; production build passed with the existing CSS budget
warnings.
No git commit was created.
