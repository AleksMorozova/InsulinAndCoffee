# Assistant reliability and AI monitoring

## What changed

The existing matching expressions now cover California's Russian/Ukrainian cases,
`Le Gril`/`Le Grill`, and buckwheat/гречка/гречневая каша forms. Explicit lookup and
add phrases use a conservative deterministic interpreter before the existing LLM.
Questions about what was counted return saved references without changing the
draft. Follow-ups and grammar outside these fast paths retain the existing model
interpreter. Ask Past Me and Foods filtering reuse the shared matching expressions.

The Assistant keeps its existing independently scrolling transcript and persistent
Current Meal area. The first meal opens its summary; the user can subsequently
collapse it. Quantity and remove controls submit explicit indexed edits with the
current draft version and a durable request ID. Retries reuse the identical edit,
and stale edits return the existing conflict response. Order-item quantity fields
start with the saved quantity. Whole orders have one action that adds every item
to Current Meal; individual items retain separate identities and source references.
Restaurant items never create Foods. Conversation titles can be renamed.

Responses now include optional `resultStatus`: `Completed`, `PartiallyCompleted`,
`NeedsClarification`, or `NoMatchesFound`. Transport/provider failures continue to
use the existing HTTP ProblemDetails error contract. Known items remain in partial
drafts and unresolved names are shown explicitly. Russian/Ukrainian lookup replies,
matching issues, draft feedback and send errors are localized; English remains
available. The interface uses the latest conversational language for short numeric
follow-ups. Unrecognized food names are not invented or automatically estimated.

Calculator handoff, glucose provenance, insulin settings and calculations are
unchanged. Settings currently define target glucose but do not define a default
current measurement. Existing Calculator behavior therefore retains its 5.5 mmol/L
unconfirmed fallback and preserves explicitly entered current glucose. Target
glucose is never substituted for a current reading. Default glucose remains
unconfirmed and cannot silently become a saved measurement.

## Usage and cost architecture

`OpenAiLlmClient` is the sole implemented AI provider adapter. Each actual outgoing
call creates one `AiUsageRecord`, including unsuccessful calls, timeouts, network
failures, invalid responses and cancellation. Automatic provider retries are not
introduced. Each explicitly retried provider call has a separate record ID and
shares the original operation ID. Deterministic search, source selection and
durable response replay do not create fictional provider calls or tokens.

Records include owner, operation/conversation/correlation IDs, provider request ID
when returned, requested model (or response-reported exact model when available),
timestamps, monotonic latency, status, safe error code, provider-reported token
components and immutable pricing/cost snapshots. Prompts, response bodies, food
names, history, glucose values, insulin and API credentials are not stored in these
tables. Request details expose operational metadata only. Existing private
conversation content is not added to the monitoring API.

The singleton usage writer opens an independent scoped `IAppDbContext` and uses a
three-second timeout, independent of a disconnected caller. Recording failures log
an error with failure type and record/operation IDs, without raw provider or database
error text. They do not fail or contaminate the business request. There is no queue
or outbox: an unavailable monitoring database can lose that usage event; structured
`AI usage persistence failed` / `AI tracking failed` logs must be monitored.

`AiModelPrices` is an administrator-configured, append-only catalog. No unverified
rates are seeded. Enter exact provider/model names and verified USD-per-million
rates, effective from a future UTC instant. The most recent applicable version at
request start wins; optional expiry is exclusive. Earlier versions remain visible.
There is no update/delete pricing endpoint. A unique provider/model/effective-time
index prevents duplicate version keys. Recorded costs and applied rates never
recompute when new versions are added. Old log-only `MealAssistant:Pricing:*`
settings are no longer the accounting source; enter their verified equivalents as
pricing versions in Admin instead.

Costs use decimal arithmetic and PostgreSQL `numeric(28,14)` persistence:

```text
nonCachedInput = inputTokens - cachedInputTokens
estimatedUSD = (nonCachedInput * inputRate + cachedInputTokens * cachedRate
                + outputTokens * outputRate) / 1,000,000
```

Reasoning tokens are a breakdown of completion tokens, not an additional billable
quantity. Cached input is a breakdown of prompt tokens, not additional input. See
the [OpenAI Chat Completions usage contract](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create).
Missing usage remains null. Unknown pricing, an unavailable required cached rate,
or missing cache usage with differing input tariffs leaves cost unavailable.
Provider charges, service tiers, fees and discounts can differ: all local costs
are labeled **estimated**, never actual billing. This implements the text-only
Chat Completions path already used by the app, not pricing rules for unimplemented
audio/image/batch providers.

There is no application response cache. Real calls are recorded as `Bypass`, and
eligible hit rate shows **Not enabled**. Provider cached input tokens remain a
separate field and cost component. Idempotency receipts are retry protection, not
an eligible application response cache. No savings baseline is invented. The schema
and aggregates support future genuine hit/miss records without adding a cache now.
Only Meal Assistant currently appears as an AI-enabled feature; Ask Past Me/Foods
database queries do not call a provider and are not counted as AI requests.

## Admin API and UI

Every endpoint requires the existing JWT Admin role. Anonymous users receive 401;
authenticated regular users receive 403. Angular uses the existing `adminGuard`
and Account menu link to `/admin/ai-monitoring`.

| Endpoint | Behavior |
| --- | --- |
| `GET /api/admin/ai-monitoring/overview` | KPIs and UTC-day, feature and model buckets |
| `GET /api/admin/ai-monitoring/requests` | Server filtering, sorting and pagination |
| `GET /api/admin/ai-monitoring/requests/{id}` | Metadata and applied pricing details |
| `GET /api/admin/ai-monitoring/pricing` | All pricing versions |
| `POST /api/admin/ai-monitoring/pricing` | Append a validated pricing version |
| `PATCH /api/meal-assistant/conversations/{id}` | Owner-scoped rename, `{ "title": "…" }` |

Existing `POST /api/meal-assistant/messages` accepts optional
`edit: { itemIndex, action: "set" | "remove", quantity? }`. Include `requestId`
and `expectedDraftVersion`. It cannot be combined with source `selection`.
Existing consumers that omit `edit` retain their prior request hash shape.

Monitoring query parameters: `from` inclusive, `to` exclusive (up to 366 days),
`userId`, `model`, `feature`, `status`. History additionally accepts `page`,
`pageSize` (1–100), `sort` (`time`, `cost`, `duration`) and `descending`.
Dates are stored in UTC. UI presets use browser-local calendar boundaries,
converted to UTC for filtering; request timestamps display in browser local time.
Daily charts explicitly show UTC accounting days. Totals include known values
and show unpriced/incomplete-usage counts. Average cost divides by priced requests;
latency and error rate divide by actual provider calls. Cancellations are separate
from provider failures. Unknown costs/tokens are unavailable, not zero.

## Migration and setup

Migration `20261122152340_AddAiUsageMonitoring` adds `AiUsageRecords`,
`AiModelPrices`, their indexes and nullable `AssistantConversations.Title`.
Existing meals, foods, conversations, receipts and settings are preserved.
Normal API startup applies migrations outside Testing, following existing behavior.
Deploy the backend/schema before the updated frontend. No production database was
accessed during development or test execution.

No additional environment variables, API keys, libraries or external services are
required for monitoring. Keep the existing `MealAssistant` configuration:

```powershell
dotnet user-secrets set "MealAssistant:ApiKey" "YOUR_OPENAI_API_KEY" --project backend/src/InsulinAndCoffee.Api
dotnet run --project backend/src/InsulinAndCoffee.Api --launch-profile http
```

Use `MealAssistant__ApiKey` for a hosting secret instead. Never commit a real key.
Create a pricing version from Admin → AI Monitoring → Model pricing. The exact
response model name is visible even before pricing exists. No historical cost
backfill or invented baseline is performed.

## Validation

Verified locally: **295 backend tests passed (0 skipped), 119 browser tests passed**.
The production Angular build passed. Existing CSS budgets still warn for Assistant
(5.34 kB versus 4.10 kB) and Ask Past Me (5.77 kB versus 4.10 kB); neither is a build
failure. The backend also reports the existing nullable warning in
`StructuredDeliveryMealTests.cs`. No real OpenAI call, hosted database change,
deployment, git commit or push was performed.

Backend tests cover provider success/failure/auth/rate-limit, timeout, cancellation,
multiple actual attempts under one operation, missing usage, tracking failures,
metadata privacy, decimal estimates, zero/missing counts, cache/reasoning components,
immutable time-selected pricing, date filtering, pagination and admin authorization.
PostgreSQL tests apply the migration to disposable local databases, verify model
consistency, aggregate SQL translation, monetary precision and pricing uniqueness.
Assistant tests cover Russian California lookup with no draft mutation, buckwheat
aliases, independent saved-order items, partial meals, quantity edits, safe replay,
stale version rejection, source immutability and rename. Existing whole-order,
override, Calculator handoff and ownership regressions remain in the full suite.

Frontend tests cover quantity prefill, one whole-order action, versioned edits and
retry envelopes, rename, Russian timeout handling, monitoring null/zero distinctions,
filters/pagination, pricing and real errors. Existing responsive workspace tests
exercise desktop, tablet and phone widths.

```powershell
# Local PostgreSQL only: suite creates/drops isolated iac_assistant_pr3_* databases.
$env:ASSISTANT_TEST_POSTGRES='Host=127.0.0.1;Port=5433;Database=postgres;Username=postgres;Password=postgres'
dotnet test InsulinAndCoffee.sln --configuration Release
cd frontend
npm test -- --watch=false --browsers=EdgeHeadlessNoSandbox
npm run build
```

For a manual smoke test, save your own structured restaurant order (without Foods),
then try `сколько брала на калифорнию`, `добавь Калифорнию и 120 г гречки`,
and a request including an unavailable product. Verify saved quantities, partial
status, direct edits and source preservation. Open Calculator and verify the draft
and unconfirmed-glucose notice. Rename/reopen a conversation. As a regular user,
verify monitoring is forbidden; as an admin, inspect a genuine model-interpreted
request and check usage and pricing. Simple deterministic queries correctly create
no provider usage row. A live OpenAI smoke test requires a valid key and can incur
charges; automated tests use synthetic provider responses and make no paid calls.

## File inventory

Modified existing files:

- `backend/src/InsulinAndCoffee.Api/Controllers/MealAssistantController.cs`
- `backend/src/InsulinAndCoffee.Application/Abstractions/IAppDbContext.cs`
- `backend/src/InsulinAndCoffee.Application/DependencyInjection.cs`
- `backend/src/InsulinAndCoffee.Application/Dtos/MealAssistantDtos.cs`
- `backend/src/InsulinAndCoffee.Application/Services/AssistantDraftBuilder.cs`
- `backend/src/InsulinAndCoffee.Application/Services/AssistantFoodMatching.cs`
- `backend/src/InsulinAndCoffee.Application/Services/DeliveryMealService.cs`
- `backend/src/InsulinAndCoffee.Application/Services/FoodService.cs`
- `backend/src/InsulinAndCoffee.Application/Services/MealAssistantService.cs`
- `backend/src/InsulinAndCoffee.Domain/Entities/AssistantConversation.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/DependencyInjection.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/OpenAiLlmClient.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Persistence/AppDbContext.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Persistence/Configurations/AssistantConversationConfiguration.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AssistantPostgreSqlSafetyTests.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AuthIntegrationTests.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/MealAssistantTests.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/PersonalFoodMemoryAcceptanceTests.cs`
- `frontend/src/app/app.component.ts`
- `frontend/src/app/app.routes.ts`
- `frontend/src/app/core/api.service.ts`
- `frontend/src/app/core/meal-assistant.models.ts`
- `frontend/src/app/pages/meal-assistant.component.ts`
- `frontend/src/app/pages/meal-assistant.component.spec.ts`

Added files:

- `backend/src/InsulinAndCoffee.Api/Controllers/AdminAiMonitoringController.cs`
- `backend/src/InsulinAndCoffee.Application/Abstractions/IAiUsageWriter.cs`
- `backend/src/InsulinAndCoffee.Application/Dtos/AiMonitoringDtos.cs`
- `backend/src/InsulinAndCoffee.Application/Services/AiMonitoringService.cs`
- `backend/src/InsulinAndCoffee.Application/Services/AssistantIntentText.cs`
- `backend/src/InsulinAndCoffee.Application/Services/AssistantLanguage.cs`
- `backend/src/InsulinAndCoffee.Domain/AiCostCalculation.cs`
- `backend/src/InsulinAndCoffee.Domain/Entities/AiUsageRecord.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/AiUsageWriter.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Persistence/Configurations/AiUsageConfiguration.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Migrations/20261122152340_AddAiUsageMonitoring.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Migrations/20261122152340_AddAiUsageMonitoring.Designer.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AiMonitoringPostgresTests.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AiMonitoringTests.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AssistantUxReliabilityTests.cs`
- `frontend/src/app/core/ai-monitoring.models.ts`
- `frontend/src/app/pages/admin/ai-monitoring.component.ts`
- `frontend/src/app/pages/admin/ai-monitoring.component.spec.ts`
- `docs/ai-monitoring-and-assistant-ux.md`
