# Meal operation correctness investigation

## Evidence and root cause

The repository reproduces the California failure. In the original
`AssistantDraftBuilder.UpdateAsync`, every successful add used
`items.AddRange(addition.Items)`. Two separate operations adding the same saved
item therefore produced two entries. That is also a legitimate way to request an
additional serving, so globally blocking or hiding these entries would be wrong.
The defect was that entries had no stable draft identity, and subsequent natural
language set/remove operations matched display names and rejected any match count
above one, without checking whether entries represented the same trusted source.
That directly produced “Калифорния, Калифорния?” rather than applying an explicit
final quantity to the logical item.

No original production conversation ID, operation receipts, request trace or
database snapshot was supplied. Consequently, the exact originating clicks or
messages for the reported production duplicates cannot be established. The
reproduced append path and downstream ambiguity are confirmed; a broken network
retry is **not** claimed as the original cause.

| Suspected path | Inspection / verification |
| --- | --- |
| Repeated replies or HTTP retry apply the operation again | Existing durable request receipts already protect the same RequestId. New tests verify replay and concurrent identical operations against PostgreSQL. |
| Opening a conversation adds food | `openChat` reads turns; it does not send a mutation. Reload tests assert no extra mutation. |
| Add item applies twice within one request | One selection produces one builder invocation. The UI guards an in-flight request. A later, separate add operation intentionally appends another entry. |
| Hydration / optimistic updates append twice | Angular replaces `currentDraft` with the server draft; it does not append individual items. A late older response could nevertheless become the newest turn when the draft came from history; it is now prevented from rewinding active state. |
| Pending resolution duplicates on retry | Resolution removes the pending ID and writes its resolved entries in the same SaveChanges as the operation receipt. Retry tests verify one resulting entry. |
| Existing duplicate sources cause indistinguishable choices | Confirmed: name-only matching did not compare source identity, units, references, overrides or provenance. |
| Compound remove + set becomes replacement | The deterministic interpreter previously had no such grammar and delegated to a single-operation LLM interpretation. It now parses supported clauses explicitly, without an AI call. |

Removal had additional confirmed failure paths: natural removal considered only
resolved items, not the pending list; manual removal identified an index and
called `PrepareAsync` for **all** items before removing one. Edited/deleted source
data could therefore block removal. Pending replacement was also blocked by
unrelated unresolved entries. These transitions are fixed.

## Identity, persistence and concurrency

- `DraftItemId` identifies one active draft entry. Fresh additions get new IDs;
  quantity changes preserve IDs; replacement creates a new entry. Exact manual
  edit/removal prefers this ID over any positional hint.
- `SourceItemId` is derived from the saved delivery item, historical meal item,
  whole delivery, or Food reference. It is not the draft entry identity. A manual
  entry without a saved source has no SourceItemId and is not automatically merged
  with another manually entered item merely because names match.
- The existing public `RequestId` is the **OperationId**. Its durable receipt key
  is `(UserId, RequestId)` with a payload fingerprint. No duplicate alias field or
  second idempotency mechanism was introduced. Different content under an already
  used ID remains a conflict.
- Existing draft version and conversation revision concurrency tokens, receipt
  uniqueness and EF SaveChanges transaction behavior remain authoritative. A
  compound command calculates its result before one commit; a later ambiguous
  clause returns the original meal rather than committing the earlier removal.
- Old JSON gets deterministic IDs on read without changing carb values. Duplicate
  cloned IDs are repaired deterministically. IDs persist with the next mutation.
  The schema is unchanged: **no EF migration is required**.
- Legacy index-based clients remain compatible under the existing expected-version
  check. Their original receipt fingerprint is preserved and regression tested.
  Updated Angular sends DraftItemId and tracks rendered rows by it.

## Command rules

1. A retry with the same operation ID returns the saved response, including the
   original entry IDs and version. It does not mutate again. A distinct explicit
   add operation can add another entry/serving.
2. “Калифорнию оставь одну порцию” sets the final quantity. Multiple entries are
   consolidated only when SourceItemId, FoodItemId, unit, saved carb references,
   override and provenance agree. Overrides additionally require the same original
   quantity before reconciliation. The explicit requested quantity is used, not
   the sum of duplicate quantities. Different sources/overrides require clarification.
3. Clarification includes numbered choices, quantities, units, carbs and source;
   restaurant/order labels are looked up by owned delivery-item IDs when applicable.
   Exact entry IDs stay in the API/state and are not shown as user-facing labels.
   Removal does not search the global Foods catalog.
4. “Убери жареную картошку” removes the matching resolved or pending entry. Exact
   manual removal needs no source revalidation. Repeated removal of an already
   absent exact ID is harmless. Removing an unresolved entry ignores incomplete
   quantity inputs and clears its pending issues.
5. Existing subtract semantics remain: “убери один кусочек пиццы” subtracts one
   piece instead of deleting the entire entry.
6. “Убери жареную картошку и калифорнию оставь одну порцию” runs independent remove
   and set clauses as one versioned operation. It never means replacement.
7. A resolved pending item leaves the pending list; a single resulting entry keeps
   its pending ID as DraftItemId. A second resolution with the same operation ID
   replays; a new operation targeting a no-longer-pending ID is rejected.
8. Unresolved items do not block independent resolved-item removal, quantity edits
   or replacement. Their pending state and incomplete-total indication are retained.
9. User-entered carb overrides scale proportionally with an explicitly changed
   quantity, using the central decimal `FoodCarbCalculator.ScaleTotal` helper and
   existing midpoint rounding. Calculated carbs and the effective override remain
   separate. Provenance is preserved. No historical records or global Foods are
   rewritten by draft operations, and insulin calculation rules are unchanged.

## Angular behavior

The affected row shows a small saving status. No optimistic deletion or local
carbohydrate calculation occurs. Failure keeps the item and preserves the exact
retry envelope. Success replaces the active draft with the server response.

A version conflict or replay fetches authoritative draft state. The original
input stays available after conflict, but the mutation is not automatically
resubmitted. Successful refresh lets the user review the current meal and explicitly
send a new request with the current version. If refresh fails, editing remains
blocked with a visible retry diagnostic. Older responses cannot rewind a newer
draft hydrated from history. Compound success messages are displayed from the
backend instead of being replaced by a generic frontend summary.

## Acceptance result

Synthetic fixture only:

| Before | Quantity | Carbs |
| --- | --- | ---: |
| Калифорния, source A, entry 1 | 1 serving | 60 g |
| Калифорния, source A, entry 2 | 1 serving | 60 g |
| Пюре | 120 g | 18 g |
| Жареная картошка | 120 g | 35 g |

The authenticated HTTP command returns exactly **Калифорния: 1 serving / 60 g**
and **Пюре: 120 g / 18 g**, totaling **78 g carbohydrates**, and:

> Убрала жареную картошку. Калифорнию оставила одну порцию. Всего: 78 г углеводов.

An HTTP conversation reload still contains exactly those two items. Calculator
draft preparation returns the same two items and 78 g. Replaying the original
operation leaves the same version and state. The test then removes California by
DraftItemId with an intentionally wrong index and verifies the persisted 18 g result.
All PostgreSQL work uses new, guarded localhost-only test databases, removed by the
test fixture. No real user/production draft was repaired or inspected.

## Regression coverage

`MealOperationTests`, `MealOperationPostgreSqlTests`, existing assistant regressions,
component specs and the routed Personal Food Memory browser journey cover:

| Requested cases | Coverage |
| --- | --- |
| 1–4: same add ID, intentional adds, lost-response retry, concurrent adds | Durable receipts, distinct entries, PostgreSQL barriers for same/different IDs |
| 5–8: refresh, same names/different sources, accidental California duplicates, final quantity | Component authoritative replacement, source/override distinction, 78 g HTTP fixture |
| 9–12: natural removal, pending removal, already removed, multiple removals | Deterministic removal/pending cleanup, repeated exact remove, compound commands |
| 13–15: set after reconciliation, compound edits, double pending resolution | Stable IDs, original override scaling/provenance, atomic feedback and durable replay |
| 16–20: page refresh, version conflict, provenance, scaling, Calculator transfer | HTTP reload, real PostgreSQL conflicts, UI automatic refresh, decimal rounding/zero/invalid tests, 78 g Calculator draft |

Validation: **346 backend tests passed, zero skipped**, **144 frontend tests
passed**, Angular production build passed. Existing CSS budget warnings remain.
No commit was created.

Commands used:

```powershell
$env:ASSISTANT_TEST_POSTGRES = 'Host=127.0.0.1;Port=5433;Database=postgres;Username=postgres;Password=postgres'
dotnet test InsulinAndCoffee.sln --configuration Release --no-restore
Set-Location frontend
npm test -- --watch=false --browsers=EdgeHeadlessNoSandbox
npm run build
```

Deploy backend and frontend together through the normal workflow. No data migration
or bulk deduplication is required. Existing duplicates remain intact until an
explicit set/removal operation can safely identify their source. To identify the
exact original production event, inspect that conversation's receipt RequestIds,
versions and response snapshots with authorized access; do not infer it from names.
