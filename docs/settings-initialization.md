# Missing user settings initialization

## Findings and strategy

Current password registration and new Google account creation already use `AuthService.AddNewUser`, attach `DiabetesSettings.CreateDefaults` through the User relationship and persist the user/settings graph in one SaveChanges transaction. Login does not create a second settings record. No change to registration was needed; API regression tests confirm the current behavior. This inspection cannot establish which older application version or import originally omitted an affected user's settings.

The reproducible gap was read-time handling: SettingsService and MealCalculationService threw NotFound for a missing settings row. Only updating settings could create one, and that path did not recover from simultaneous inserts. Existing users/imports with missing settings therefore remained broken after logging in.

SettingsService now centralizes GetOrCreateUserSettingsAsync. Normal reads use one no-tracking query by authenticated UserId. If missing, it checks that the user exists, creates the existing domain defaults with injected TimeProvider and saves. Settings updates request a tracked entity from that same method. Calculator uses the same initializer, including when accessed before the Settings page. No other user's settings are queried as a fallback.

The existing `IX_DiabetesSettings_UserId` unique index prevents duplicate records. Infrastructure translates only PostgreSQL 23505 for that exact constraint into SettingsAlreadyExistException. The initializer detaches its failed Added entity and reads the winner; it never replaces the winner's customized values or clears unrelated tracked work. Other database exceptions, connection errors and cancellations propagate to existing error handling. No broad exception-to-default fallback was introduced.

## Defaults

The existing DiabetesSettings factory remains the source of truth: TargetGlucose 6.5, CarbRatio 10, CorrectionFactor 3, InsulinDurationHours 4. No values were newly selected or changed. Calculator current glucose defaults to 5.5 mmol/L as before; current glucose and target glucose are distinct. No insulin formulas, glucose persistence, supplies, meal history or authentication contracts changed.

Angular Settings now loads the response directly into its form, displays loading and genuine API errors, and prevents saving before loading or during a save. It no longer describes every authenticated user as the default user Aleksandra. Registration/login navigation remains unchanged.

## Changed files

- `backend/src/InsulinAndCoffee.Application/Services/SettingsService.cs`
- `backend/src/InsulinAndCoffee.Application/Services/MealCalculationService.cs`
- `backend/src/InsulinAndCoffee.Application/Abstractions/SettingsAlreadyExistException.cs`
- `backend/src/InsulinAndCoffee.Infrastructure/Persistence/AppDbContext.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/SettingsServiceTests.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AuthIntegrationTests.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AssistantPostgreSqlSafetyTests.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/MealServiceTests.cs`
- `frontend/src/app/pages/settings/settings.component.ts`
- `frontend/src/app/pages/settings/settings.component.spec.ts`
- This document.

## Validation

- `dotnet test InsulinAndCoffee.sln --configuration Release --no-restore --verbosity minimal`: **241 passed, 0 failed/skipped**, with ASSISTANT_TEST_POSTGRES pointing to localhost. PostgreSQL tests create/drop isolated temporary databases, never the application database.
- Coverage includes password and Google registration defaults, first authenticated settings access, repair of a missing row through HTTP, repeated requests, customized values, user isolation, Calculator-first repair, unexpected save failures, and simultaneous first-time initialization against real PostgreSQL.
- The previous test asserting missing-settings NotFound was narrowed to the actual remaining invalid case: the user itself does not exist.
- Angular `npm test -- --watch=false`: **69 passed**. New tests verify first-response form hydration without refresh and visible real load failures with saves blocked. The local Karma process was stopped after its completed success summary because it remained alive.
- `npm run build`: passed; existing unrelated Meal Assistant CSS budget warning remains (4.80 kB versus 4.10 kB).
- `git diff --check`: passed.

## Deployment

No schema change or new migration is required: the unique UserId index exists in InitialCreate and current EF mappings. Deploy the updated backend and frontend normally with the existing application migration process. Missing records repair lazily on the next settings or Calculator request; no production connection, bulk repair, manual SQL, or database intervention was performed or is required by this change. No commit or push was created.
