# Assistant draft concurrency and retry safety

## Root causes and approach

The previous implementation treated an assistant message's `ResponseJson` as the
current draft and its deterministic message ID as the retry receipt. Pruning could
remove that receipt; replay did not compare payloads. Two application instances
could interpret the same draft, calculate separate edits, then persist both
without validating which draft the user had seen. The in-process limiter only
serialized requests on one server.

The conversation now owns `DraftJson`, `DraftVersion`, and `Revision`. The latest
draft response is still stored in the transcript for existing UI/history behavior;
the authoritative current draft no longer depends on transcript retention.

- `DraftVersion` advances for draft mutations, including clear and clarification
  state changes. Lookup/history/recent/show do not advance it or rewrite DraftJson.
- `Revision` advances for every persisted conversation turn and is an EF
  concurrency token, alongside DraftVersion. It protects transcript/pruning and
  deletion races without making read-only queries change the public draft version.
- A mutation must carry a request ID. An edit to an existing conversation must
  carry the expected draft version. New conversations start from version zero.
- EF's conditional UPDATE/DELETE checks the original tokens. A competing commit
  causes a database concurrency failure, not an overwritten draft.
- One relational SaveChanges transaction commits the snapshot, version, messages,
  pruning and completed-request receipt together. A losing save rolls back all
  its effects. The scoped context discards its failed tracked changes before
  looking up a winning receipt or returning a conflict.

No transaction or process lock spans the external interpretation call. Separate
service instances/DbContexts exercise the same PostgreSQL guarantees as separate
API instances. Unrelated carbohydrate/insulin calculations are unchanged.

## Durable receipts

`AssistantRequestReceipts` has a composite primary key `(UserId, RequestId)`, a
User foreign key, conversation ID, SHA-256 payload fingerprint, committed response
JSON and creation timestamp. Receipts are not pruned with transcript messages.

The fingerprint includes conversation ID (including an original null), trimmed
message, expected version, and optional selection ID/quantity/unit. Decimal scale
is normalized, so numeric 2 and 2.0 have the same fingerprint. It does not include
server-derived carbohydrate values. Once a request successfully commits, its ID
cannot be reused with a different fingerprint.

An identical retry returns the original business result and version, even if the
current draft is newer, with `replayed: true`. The lookup happens before provider
execution. If two requests race past that initial lookup, PostgreSQL uniqueness
or the conversation token rejects the loser; the loser reads and returns the
winner's receipt when its fingerprint matches. Different payloads receive 409.
This also prevents duplicate initial conversations after a lost first response.

Receipts deliberately have no conversation FK: after explicit deletion a small
tombstone must still prevent recreation through a stale retry. Deletion clears
receipt response content while removing the conversation/messages atomically;
the remaining hash/IDs identify only the completed request. Replay then returns
404. User deletion cascades receipt deletion. The conversation revision token
also protects deletion against a concurrent lookup adding a new receipt.

Failed interpretations, validations, cancellations before commit, and version
conflicts have no completed receipt. The first successful commit establishes
the immutable payload for a request ID; Angular uses a new ID after explicit
conflict recovery. No pending-request reservation subsystem was introduced.

## API contracts

Existing routes and fields remain. The messages request adds:

```json
{
  "conversationId": "EXISTING_CONVERSATION_GUID",
  "message": "Add another portion",
  "requestId": "NEW_REQUEST_GUID",
  "expectedDraftVersion": 3
}
```

The response adds `draftVersion`, `requestId`, and `replayed` (false for an original
successful response). The draft version belongs to the accompanying/current
conversation draft; replay retains the original response's version.

New authenticated endpoint:

`GET /api/meal-assistant/conversations/{id}/draft-state`

returns `{ draftVersion, draft }`, scoped to the current user and independent of
the transcript. Calculator preparation also reads this authoritative snapshot.
Conversation reads keep old transcript responses readable and expose the current
draft/version without changing stored historical responses or receipts.

Conflicts use existing ProblemDetails plus extensions:

| HTTP / code | Meaning |
| --- | --- |
| 409 / `draft_version_required` | Existing conversation mutation lacks expected version |
| 409 / `draft_version_conflict` | Expected version is stale, or another conversation write won |
| 409 / `assistant_request_reused` | Completed request ID has a different payload |
| 409 / `assistant_legacy_retry` | A pre-receipt message ID exists, but its original full payload cannot be verified |

Version conflicts include `currentDraftVersion` when available. Errors retain
`status`, `title`, `detail` and `traceId`. Mutations without a request ID receive
400. Other business endpoints and JWT enforcement are unchanged.

## Angular recovery

The client snapshots conversation ID, text, source selection, request ID and
expected version when sending. A manual network/90-second timeout retry resends
that exact envelope. It never silently changes the version under the same ID.
Changed selections create a new ID. Duplicate submits while pending are blocked.
There is no automatic mutation retry.

The composer remains editable while a request runs. Success only clears the text
if it is still the submitted question. A failed request can be retried separately
without losing a newer unsent message. Reopening the same conversation retains
the failed envelope, including its original version and original null conversation
ID when a completed first request is identified in the transcript.

On 409, the message remains visible and sending is paused. **Refresh latest
draft** fetches transcript then authoritative state sequentially (the existing
per-user limiter permits one API operation at a time). The user reviews the
latest draft and explicitly sends again, with a new request ID and current
version. Refresh failure preserves both input and the conflict state.

Responses are deduplicated visually by request ID. A replay does not overwrite a
newer live draft; it asks for an authoritative refresh before further editing.
If a new-conversation request ID was reused with different content and its
conversation is unknown, the user is directed to open it from conversation history.

## Migration and rollout

New migration: `20261120223407_AddAssistantDraftConcurrency`.

- Adds nullable DraftJson, default-zero DraftVersion and Revision.
- Adds the receipt table and its uniqueness/ownership constraints.
- Backfills the latest valid draft object from existing PascalCase/camelCase
  ResponseJson, leaving transcript rows unchanged and version zero for legacy drafts.
- No old migration was changed. The model snapshot/new designer include the schema.

The schema is additive and legacy drafts remain readable. Old clients must refresh
to obtain the new version-aware frontend before editing existing conversations.
Coordinate/drain old API writers during migration/cutover: an old binary does not
participate in the new conditional-write protocol, so mixed old/new writers are
not covered by this guarantee. Deploy all API instances with the new code before
allowing versioned editing. Verify database isolation before any Render preview
startup because startup still applies EF migrations outside Testing.

Pre-upgrade request IDs were irreversibly hashed into message IDs and did not
store full fingerprints. An identifiable legacy retry is rejected safely instead
of guessing its payload. Pruned pre-upgrade requests cannot be reconstructed;
the durable guarantee begins with requests committed to the new receipt table.

## Verification

PostgreSQL integration tests use `ASSISTANT_TEST_POSTGRES`. Only localhost,
127.0.0.1 and ::1 are accepted. Each test creates a randomly named
`iac_assistant_pr3_<guid>` database and deletes only that verified generated
database afterward. Existing application databases and remote hosts are never
used as test targets. The new CI service supplies PostgreSQL 16 so these tests
run in CI; without the variable they are explicitly skipped, not reported passed.

Local commands from the repository root:

```powershell
$env:ASSISTANT_TEST_POSTGRES = 'Host=127.0.0.1;Port=5433;Database=postgres;Username=postgres;Password=postgres'
dotnet test InsulinAndCoffee.sln --configuration Release --no-restore --verbosity minimal
cd frontend
npm run build
npm test -- --watch=false
```

Database-backed cases cover competing versions across separate instances,
concurrent identical edits, concurrent first requests, conflicting payload races,
missing/stale versions, sequential edits, receipt uniqueness and user isolation,
lost responses and replay after real transcript pruning, read-only draft
preservation, deletion tombstones, legacy migration/backfill, model consistency,
and Production-environment startup applying migrations to an empty isolated DB.
HTTP tests verify the machine-readable 409 contract. Angular tests cover timeout
envelopes, duplicate submits/responses, unsent input, refresh failure/recovery and
reopening a conversation after a failed request.

Final local verification: **216 backend tests passed**, including **12
PostgreSQL-backed integration tests**, with no skips. Migration backfill/model
consistency and Production-mode startup passed against isolated local databases;
all generated test databases were removed. Pending clarification context also
survived transcript pruning. Angular production build passed with the existing
Meal Assistant component CSS budget warning. Headless Edge reported **63
successful Angular tests**; its launcher emitted startup errors and the process
did not promptly exit, so that local runner limitation is reported separately
from the passing assertions. No remote database, Render deployment, git commit
or push was changed.

## Remaining windows and next PR

- OpenAI can execute twice when concurrent requests race before either commits,
  or when interpretation succeeds but persistence/cancellation fails. Only the
  committed mutation/result is deduplicated; exactly-once LLM execution is not promised.
- A browser reload/process kill can lose an in-memory unsent request envelope.
  Review the saved conversation before inventing a new request ID; it cannot be
  inferred reliably from arbitrary new text. This PR does not persist sensitive
  unsent content to browser storage.
- Read-only conversation writes can conservatively conflict with an in-flight
  edit through Revision. The public draft version/content remain unchanged by
  those queries, and the edit is not automatically rebased.
- Completed receipts grow independently of transcript pruning. No expiry that
  would silently weaken deduplication was added. Retention/size limits require a
  separately specified retry horizon and privacy policy.
- Production/Render multi-process deployment was not exercised; PostgreSQL
  tests used independent service instances/contexts, not two deployed containers.

Suggested next PR: define receipt retention and an explicit client recovery
policy for browser restarts, then add structured pending-choice/draft-item IDs.

## Changed files

- `.github/workflows/ci.yml`
- `backend/src/InsulinAndCoffee.Domain/Entities/AssistantConversation.cs`
- `backend/src/InsulinAndCoffee.Application/Abstractions/IAppDbContext.cs`
- `backend/src/InsulinAndCoffee.Application/Abstractions/AssistantConflictException.cs` (new)
- `backend/src/InsulinAndCoffee.Application/Dtos/MealAssistantDtos.cs`
- `backend/src/InsulinAndCoffee.Application/Services/MealAssistantService.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Persistence/AppDbContext.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Persistence/Configurations/AssistantConversationConfiguration.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Migrations/20261120223407_AddAssistantDraftConcurrency.cs` (new)
- `backend/src/InsulinAndCoffee.Infrastructure/Migrations/20261120223407_AddAssistantDraftConcurrency.Designer.cs` (new)
- `backend/src/InsulinAndCoffee.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
- `backend/src/InsulinAndCoffee.Api/Controllers/MealAssistantController.cs`
- `backend/src/InsulinAndCoffee.Api/ExceptionHandling/GlobalExceptionHandler.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AssistantPostgreSqlSafetyTests.cs` (new)
- `backend/tests/InsulinAndCoffee.Application.Tests/AuthIntegrationTests.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/MealAssistantTests.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/UnifiedMealMatchingTests.cs`
- `frontend/src/app/core/meal-assistant.models.ts`
- `frontend/src/app/core/api.service.ts`
- `frontend/src/app/core/api.service.spec.ts`
- `frontend/src/app/pages/meal-assistant.component.ts`
- `frontend/src/app/pages/meal-assistant.component.spec.ts`
- `docs/assistant-draft-safety.md` (new)
