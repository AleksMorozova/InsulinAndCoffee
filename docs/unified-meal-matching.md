# Unified meal matching

## Scope and root causes

Previously, discovery used substrings and one alias list, draft creation used
exact aliases followed by unique word fragments, draft edits used another token
check, and historical matching had additional independent aliases. A unique
fragment could become an authoritative food selection. Exact names and aliases
were pooled together, so a weaker alias could make an exact name ambiguous.
Discovery results exposed food IDs but offered no assistant selection operation.

History filtered broad candidates and took the latest 250 **before** composition
ranking. An older exact composition could therefore be excluded by recent
partial matches. Multi-item drafts retained all issue strings but only the last
pending query; a later clarification/addition could discard unresolved issues.

## Policy

`AssistantFoodMatching` owns the existing food and restaurant alias groups and
the shared comparison normalization. Original names are never rewritten in data
or response display fields. Comparison applies Unicode NFKC, case normalization,
whitespace collapse, selected punctuation-to-space conversion, and curly-to-ASCII
apostrophes. Accents, apostrophes, slashes and plus signs remain significant.
No new arbitrary transliteration aliases are inferred. The existing local
fixture label is ignored for comparison, as the previous draft resolver did.

Automatic source resolution prefers a normalized exact name, then an exact
configured alias. Restaurant context is an additional constraint: prefer its
literal normalized identity before configured aliases, and resolve only an
exact/aliased dish or one unambiguous whole order. Multiple top-scoring records
require explicit selection. A unique substring or qualified category name is
**not sufficient** for automatically creating a new draft item.

Discovery can still show weak/related results, including existing sushi/roll
discovery expansion. A whole-word configured category can rank a historical
composition or identify an already-selected draft target; this does not make it
an authoritative new food source. Exact draft targets outrank weaker contextual
targets; equally plausible targets leave the edit unapplied.

`AssistantPortionText` retains prefix parsing and adds suffix gram/portion/piece
parsing. For example, `California 60 г` and `Калифорния 60 грамм` resolve the
configured California alias with quantity 60 and a gram unit. Gram quantities
cannot rescale a saved whole order. Stable-ID selections bypass parsing of the
stored display name, so a portion food named `California 60 g` is not silently
reinterpreted as a gram-based food.

## Explicit food selection and ownership

`POST /api/meal-assistant/messages` retains its existing fields and adds optional:

```json
{
  "selection": {
    "foodId": "AUTHORIZED_FOOD_GUID",
    "quantity": 2,
    "unit": "Portion"
  }
}
```

The backend loads that ID under the authenticated user, uses saved measurement
and carbohydrate values, and calls the existing meal calculation service.
Client carbohydrate values are not accepted as authoritative. Deleted and
unauthorized IDs share the same explicit validation diagnostic, without revealing
another user's food. Quantity may inherit the single pending request; unit and
piece-integrality validation remain in the existing workflow. Selection retries
use the existing idempotent request ID. Changing a failed selection's quantity
creates a new request rather than replaying the old payload.

Saved-food result cards now provide a quantity input and selection button, also
available under alternatives for partially matched drafts. Ambiguous duplicate
names are distinguishable by their saved basis/values and selected by ID.

Ask Past Me still uses its existing ID-based **Reuse whole order** action and
ownership validation. This change does not introduce order items, split an order,
or reuse its historical insulin/glucose. Historical meal reuse still fetches the
original owned meal ID and transfers its snapshots through the existing route.

## Unresolved requests and provider usage

Every unresolved item keeps an issue. With several unresolved items, no single
pending query is selected arbitrarily. Further add/replace/clarify actions ask
the user to resend the complete intended meal instead of partially applying an
unsafe operation or losing the other issues. Failed replacement leaves existing
items intact. Existing matched items may be reviewed; missing foods are not
silently considered resolved. Ordinary food discovery no longer clears an
unresolved empty draft behind the user's back.

Recent-history shortcut and known standalone alias lookup continue to avoid the
provider. Standalone normalized exact owned-food lookups and explicit food-ID
selection also avoid it. Context-dependent instructions, including `add another
30 g`, remain on the existing interpretation path. Pending source IDs are kept
out of the provider's clarification context. OpenAI's tool schema and parser
were not changed. No caching was added.

## Retrieval and performance

History composition priority is now evaluated in the database before the
250-candidate limit: exact complete composition, similar complete composition,
then partial composition, with recency and ID tie breakers. At most 250 meals
are materialized and at most five are returned. Snapshot values and historical
dose fields are preserved. New food/order discovery remains bounded at eight
records per source; resolution reads the best two to detect ambiguity. Queries
remain user-scoped and asynchronous.

The pure normalization function has an Infrastructure-owned EF translation to
PostgreSQL `normalize(..., NFKC)`, `lower`, `replace`, `regexp_replace`, and
`btrim`. No SQL function is installed, and no schema/migration is needed. It
requires a UTF-8 PostgreSQL database. Existing personal names in Latin/Cyrillic
are covered; unusual locale-specific case folding can still differ between
.NET invariant casing and PostgreSQL's database locale.

Computed matching expressions can scan a user's records; this PR adds no index,
embedding or search infrastructure. Measure actual query plans with large
datasets before considering a normalized indexed column in a separate change.
History contains no restaurant identity field, so it cannot infer restaurant
provenance from an otherwise unqualified food snapshot.

## Examples and regression coverage

| Input | Previous risk | Current result |
| --- | --- | --- |
| `pizza`, with saved Pizza and Four cheese pizza | Exact and weaker matches could compete | Exact Pizza wins |
| `pizza`, with only Four cheese pizza | Unique fragment auto-selected | Candidate displayed; explicit selection required |
| `RED  Lentil—Soup!` | Discovery/draft normalization differed | Same normalized source resolution |
| `Шаурма из Хули Хули` | Restaurant phrase depended on separate interpretation | Known restaurant scope, exact/aliased dish |
| Two Shawarma orders at one restaurant | Different compositions could be conflated | No automatic choice; reuse a complete selected order |
| Two Salad foods | Text could not distinguish duplicates | Backend validates selected ID and applies requested quantity |
| Two unresolved foods | Last pending item overwrote the other | Both issues retained; explicit complete-meal clarification |
| Chicken + Rice older than 300 recent Chicken meals | Exact match excluded before ranking | Old exact composition ranks first |

The existing quantity-edit regression now supplies explicit full food names when
initially adding Four cheese pizza and Fried potatoes. Its subsequent contextual
quantity edits and calculation expectations are unchanged. Separate new tests
assert that the former generic-category source inference is intentionally
replaced with clarification.

Verification commands:

```powershell
dotnet test InsulinAndCoffee.sln --configuration Release --no-restore --verbosity minimal
cd frontend
npm run build
npm test -- --watch=false
```

The backend suite includes matching/alias/Unicode tests, restaurant/source ties,
source ownership and deleted IDs, HTTP selection contracts, client-carb spoofing,
selection replay, gram/portion/piece calculations, ambiguous edits, multi-item
issues, older history, and Npgsql SQL translation. Frontend tests cover typed
selection payloads and failed-request retry identity. The local PostgreSQL 16
container passed a read-only synthetic VALUES check of normalization; no existing
records or remote databases were read or changed for that check.

Remaining limits: several pending choices still require a complete clarification
request; no structured Ask Past Me items or durable correction memory are added.
PR #3 should focus on a small structured pending-choice list and ID-based draft
target selection, followed by representative PostgreSQL query-plan measurements.

## Changed files

- `backend/src/InsulinAndCoffee.Application/Dtos/MealAssistantDtos.cs`
- `backend/src/InsulinAndCoffee.Application/Services/AssistantFoodMatching.cs` (new)
- `backend/src/InsulinAndCoffee.Application/Services/FoodKnowledgeSearchService.cs`
- `backend/src/InsulinAndCoffee.Application/Services/MealHistorySearchService.cs`
- `backend/src/InsulinAndCoffee.Application/Services/AssistantDraftBuilder.cs`
- `backend/src/InsulinAndCoffee.Application/Services/AssistantPortionText.cs`
- `backend/src/InsulinAndCoffee.Application/Services/MealAssistantService.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Persistence/AppDbContext.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/UnifiedMealMatchingTests.cs` (new)
- `backend/tests/InsulinAndCoffee.Application.Tests/MealAssistantTests.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AuthIntegrationTests.cs`
- `frontend/src/app/core/meal-assistant.models.ts`
- `frontend/src/app/core/api.service.ts`
- `frontend/src/app/core/api.service.spec.ts` (new)
- `frontend/src/app/pages/meal-assistant.component.ts`
- `frontend/src/app/pages/meal-assistant.component.spec.ts`
- `docs/unified-meal-matching.md` (new)

Final verification: 203 backend tests passed in Release; Angular production build
passed with the existing Meal Assistant CSS budget warning; headless Edge
reported 55 successful Angular tests. The existing local browser launcher logged
startup errors and remained running after reporting success; it was stopped,
so a clean Angular test-runner exit was not verified. PostgreSQL query generation
and the local normalization SELECT passed; full application queries were not
executed against PostgreSQL, and no remote environment was tested. No migration,
git commit or push was created.
