# Sign In investigation — 2026-11-24

## Result and limits

The reported production delay has not been reproduced locally. No Render runtime
logs, instance tier, Neon compute settings, or browser Network capture of a slow
production attempt were available. Cold starts remain a hypothesis, not a finding.
No production account was used and no deployment settings were changed.

Two application defects were confirmed and fixed: duplicate submission was possible
between the login response and navigation completion; mapped HTTP 401 errors were
not recognized by the authentication interceptor. Neither establishes the cause of
the original latency report. There is no measured before/after latency improvement
claim. Password verification and synchronous login auditing remain intact.

## Request trace

1. `LoginComponent.submit` validates the form and guards concurrent submissions.
   `AuthService.login` sends one POST to the configured `apiUrl` plus `/auth/login`.
   The Google identity script loads independently and does not gate password login.
2. Interceptors run timing → authentication → API error mapping. Authentication
   adds an available bearer token; there are no automatic authentication retries.
   Cross-origin JSON requests can also require an OPTIONS preflight.
3. `AuthController.Login` is anonymous and delegates to `AuthService.LoginAsync`.
   The fallback authorization policy remains enabled for business endpoints.
4. `LoginAsync` queries `Users` without tracking by normalized username, which has
   a unique database index. It verifies the existing password hash using ASP.NET
   Identity's unchanged `PasswordHasher`. It inserts a login audit record and awaits
   `SaveChangesAsync` before issuing the JWT. Invalid credentials also create an
   audit record and return 401. Database failures remain errors, not successful logins.
5. `JwtAccessTokenService.Create` signs the token locally. The response already
   includes the user; login does not need an extra `/auth/me` call.
6. `AuthService.storeSession` validates the response and saves the token, expiry and
   user under the existing localStorage keys, then updates the user signal.
7. `LoginComponent.redirect` validates the return destination using
   `loginDestination`, falling back to Calculator, and awaits router navigation.
   `authGuard`/`guestGuard` use local state; neither fetches data. There are no
   application initializers or route resolvers awaiting profile/settings requests.
8. Calculator loads foods after activation and calculates through existing backend
   services. Dashboard loads its data after activation. Settings loads on demand.
   Those requests are not prerequisites for completing sign-in navigation.
9. Registration (including new Google users) saves default DiabetesSettings with
   the user. Existing users missing settings are repaired by the centralized
   `SettingsService.GetOrCreateUserSettingsAsync` on demand, using the unique UserId
   constraint and existing race handling. Login does not create/query settings.
   No glucose defaults, settings values or calculation rules changed.

## Confirmed findings and actions

| Finding / method | Evidence and impact | Action / verification |
| --- | --- | --- |
| `LoginComponent.submit`, `handleGoogle`, `redirect` released `loading` in HTTP finalize | Navigation promise could remain pending after the guard was released, permitting another POST. Additional work/latency under this race is not quantified. | Keep the submission guard until navigation settles. Browser regression keeps navigation pending and asserts exactly one POST both during HTTP and during navigation; failures release the guard. |
| `authInterceptor` checked only `HttpErrorResponse` | Inner `apiErrorInterceptor` converts it to `ApiError`, so protected 401 responses did not clear stale sessions. This is a correctness defect; no measured latency impact. | Read status through the existing mapper. Test the actual interceptor chain: protected 401 clears session and navigates to login; invalid login stays on the form. |
| Login failures were always described as incorrect credentials | Network/5xx failures could prompt misleading retries; frequency/latency impact unknown. | Distinguish invalid credentials, connection failure and temporary server failure. No automatic retries; password is cleared as before. |
| Password hashing | About 35 ms in this local run; dominant warm service stage. | Preserve hashing strength and verification behavior. Successful and rejected logins tested. |
| Database lookups/audit | Exactly two SQL commands per successful password login: indexed user SELECT and audit INSERT. No profile/settings query in login. | No speculative query changes or removal/deferment of the audit write. Real PostgreSQL regression asserts the command count and persisted attempts. |
| First-use overhead | Local first user-lookup stage was much longer than its connection/command durations, while later requests were faster. This is consistent with initial EF/runtime work, but its individual components were not separately profiled. | Measure, rather than add speculative caches/compiled queries. No production-scale query-plan claim. |

## Measured local breakdown

Release build, .NET 9, Windows, localhost PostgreSQL on port 5433; isolated temporary
database, synthetic account, real hashing/JWT/audit/EF/Npgsql. The API test host uses
Production environment startup and migrations, but **is not the production service**.
Startup and migrations complete before sampling. The connection pool for this test
database is reset before the first request. PostgreSQL itself stays running.

The following is one six-request sample, captured by
`Login_LocalPostgres_RecordsSafeStageTimingsAndRepairsMissingSettingsAfterNavigation`.
The first column is first HTTP/EF login-path use with a fresh connection; it does
not represent a full process/Render cold start or a suspended Neon compute.

| Stage (ms) | First request | Median of next 5 |
| --- | ---: | ---: |
| Buffered HTTP request via in-process TestServer | 249.394 | 41.597 |
| HTTP application pipeline | 221.673 | 41.317 |
| Login application service | 176.844 | 40.707 |
| User lookup, including EF/materialization/connection | 114.213 | 1.449 |
| Password verification | 35.949 | 35.260 |
| Audit write, including SaveChanges overhead | 19.507 | 3.824 |
| JWT creation | 7.103 | 0.124 |
| Connection open durations, summed per request | 6.412 | 0.028 |
| SQL command execution durations, summed per request | 5.706 | 3.895 |

Connection and command durations are **inside** the service stages, not additive
to them. Reader execution timing excludes subsequent row consumption/materialization.
Medians of individual stages need not sum to the median total. TestServer excludes
browser, DNS, TLS, internet routing, proxy queues and Render startup waits.
Warm HTTP samples ranged from 39.773 to 72.639 ms.

Separate, explicitly requested post-login endpoints in the same test:
`/auth/me` 23.303 ms; first `/settings` repairing a missing record 34.256 ms;
repeat `/settings` 3.778 ms. Exactly one settings row remains. These are synthetic
verification calls, not extra requests added to the Angular sign-in workflow.

Browser session-storage and response-to-navigation timings are now recorded at
runtime. Production values are unavailable; browser unit-test durations are not
substitutes for device/network measurements.

## Instrumentation

- `AuthService.LoginAsync`: structured Information log with service total,
  lookup, password verification, audit and JWT durations, outcome, active stage
  and trace ID. If a stage throws, its completed-stage field can be zero; use
  `Outcome=Error` with `ActiveStage`/`ActiveStageMs` to locate partial work.
- `AuthTimingMiddleware`: HTTP method, fixed endpoint label, status, cancellation,
  pipeline duration, trace ID, and existing response correlation ID. Covers
  `/api/auth/login`, `/api/auth/google`, `/api/auth/me`, `/api/settings`; distinguishes
  POST from OPTIONS. Timings end after application processing, before network delivery.
- EF command and connection interceptors: duration, operation/failure and trace ID
  only, while inside the selected authentication/settings request activity. No
  diagnostic SQL, parameters, connection strings or exception messages are emitted.
  They do not open connections or issue additional queries.
- Startup logs: migration duration and process startup-to-ApplicationStarted
  duration, useful when correlating a newly awakened service. Container provisioning
  and time before managed code starts are outside these timers.
- Browser `performance.measure`: `iac.auth.login-http`, `google-http`, `profile-http`,
  `settings-http`, `session-storage`, `response-to-navigation`. Durations use the
  monotonic clock, with fixed outcome labels. Only the latest entry per stage is
  retained. No credentials, tokens, usernames, user IDs, response bodies or URLs are
  stored by these measurements. No new telemetry endpoint is called.

Keep the authentication timing categories at Information level to collect backend
stages (the repository's existing default already allows this). Console logs use
structured logging templates; a structured log sink can retain their named fields.
Do not enable sensitive EF parameter logging to diagnose this issue.

## Production cold/warm capture still required

1. Deploy these changes normally. Record deployed SHA, Render service region and
   instance type, Neon compute region/state and Scale to zero setting. Do not
   export connection strings, environment variable values or credentials.
2. On an authorized test account, enable browser Network **Preserve log**. After an
   idle period exceeding the configured suspend windows, submit once without first
   opening health/API URLs. Those probes could wake the service and invalidate the
   comparison. Record POST and OPTIONS timing: queued/stalled, DNS/connect/TLS,
   time to first byte, download, status, response `X-Correlation-ID`, and UTC time.
   Avoid sharing raw HAR files: they contain the password body and bearer tokens.
3. Inspect local safe measurements in the browser console:

   ```js
   performance.getEntriesByType('measure')
     .filter(entry => entry.name.startsWith('iac.auth.'))
     .map(entry => ({ stage: entry.name, durationMs: entry.duration, outcome: entry.detail?.outcome }));
   ```

4. Match `X-Correlation-ID` to `Authentication HTTP timing`, then its `TraceId` to
   service/DB logs. Compare browser HTTP elapsed with application pipeline elapsed;
   the difference includes preflight, network, proxy and pre-application wait, so
   it alone does not prove a Render cold start. Correlate Render instance/startup
   events and migration/ready logs. If service elapsed itself is high, identify the
   slow stage; inspect Neon activation/connection events for that UTC interval.
5. Sign out and repeat five times immediately on the same deployment, recording
   the latest browser measurements after each attempt. Compare distributions,
   not just one request. If navigation is slow but HTTP is fast, inspect lazy bundle
   downloads and the Angular response-to-navigation measure. If the destination
   renders but appears unready, separately time its foods/dashboard/calculation
   requests; do not conflate them with login.

Render documents that Free web services sleep after 15 idle minutes and can take
about one minute to wake ([Render](https://render.com/docs/free)). Neon documents
idle compute activation and colder initial queries
([Neon](https://neon.com/docs/introduction/compute-lifecycle)). These are provider
capabilities, **not verified settings or measured delays for this installation**.
Choose hosting changes only after the matched traces identify hosting as the cause.

## Validation and affected files

- Full backend suite: **323 passed, 0 skipped** including local PostgreSQL tests.
- Final focused authentication/correlation suite: **34 passed**.
- Angular browser suite using the production interceptor order: **133 passed**.
- Angular production build passed; existing Meal Assistant and Ask Past Me CSS
  budget warnings remain. No schema migration or deployment configuration change.

Commands (PowerShell, repository root; the PostgreSQL environment variable must
point only at an authorized local test instance):

```powershell
$env:ASSISTANT_TEST_POSTGRES = 'Host=127.0.0.1;Port=5433;Database=postgres;Username=postgres;Password=postgres'
dotnet test InsulinAndCoffee.sln --configuration Release --no-restore
dotnet test InsulinAndCoffee.sln --configuration Release --no-restore --filter FullyQualifiedName~Login_LocalPostgres --logger 'console;verbosity=detailed'
Set-Location frontend
npm test -- --watch=false --browsers=EdgeHeadlessNoSandbox
npm run build
```

Files changed:

- `backend/src/InsulinAndCoffee.Api/Program.cs`
- `backend/src/InsulinAndCoffee.Api/Middlewares/AuthTimingMiddleware.cs`
- `backend/src/InsulinAndCoffee.Application/Services/AuthService.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/DependencyInjection.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/AuthenticationDatabaseTimingInterceptor.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AuthPerformanceTests.cs`
- `frontend/src/main.ts`
- `frontend/src/app/core/auth.service.ts`
- `frontend/src/app/core/auth.interceptor.ts`
- `frontend/src/app/core/auth-performance.ts`
- `frontend/src/app/core/auth-timing.interceptor.ts`
- `frontend/src/app/core/auth-timing.interceptor.spec.ts`
- `frontend/src/app/core/login-navigation.spec.ts`
- `frontend/src/app/pages/auth/login.component.ts`
- `docs/sign-in-performance.md`
