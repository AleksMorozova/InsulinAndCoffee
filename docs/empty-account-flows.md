# New account / empty-data regression checks

## Finding

The supplied production screenshot shows `/error?returnUrl=%2Fhistory`; it contains no original exception or failed HTTP response. In the current source, History subscribed through AsyncPipe without catching API errors. Any failed history request therefore escaped to GlobalErrorHandler, which logs the error and navigates to `/error`. Foods, Ask Past Me and Calculator's initial food load also had unhandled subscription errors. The redirect mechanism is confirmed from source and reproduced in the frontend error-recovery regression scenario; the precise original production failure (status/response/exception) remains unknown without browser Network/console or corresponding backend logs.

An authenticated newly registered account against a disposable local PostgreSQL database in the Production ASP.NET environment successfully received empty collections. An empty database result alone did **not** reproduce a backend failure. No production connection, account creation, data repair, deployment or migration was performed.

## Changes

- History: explicit loading, empty, filtered-empty, and error states; first-meal navigation; local retry. Catch is inside switchMap so retry and filter changes work after a failed request. Failed requests are never converted into a successful empty result.
- Foods: loading/error state and retry; suppress empty-library copy while loading or on failure. Existing first-food creation remains accessible.
- Ask Past Me: loading/error state and retry; suppress empty-order/favorites copy during failure/loading. Existing first-order editor and reuse behavior retained.
- Calculator: initial food-search load errors stay visible locally with retry. An empty food library remains valid and does not invent foods or start a calculation/save.
- Existing global HTTP logging and auth interception remain intact. Unexpected runtime errors still use the global error handler. No global swallow or API contract change was added.

Changed application files: `frontend/src/app/pages/history/history.component.ts`, `foods/foods.component.ts`, `delivery-meals/delivery-meals.component.ts`, `calculator/calculator.component.ts`.

Test files: `frontend/src/app/pages/empty-account.spec.ts`, `meal-assistant.component.spec.ts`, and `backend/tests/InsulinAndCoffee.Application.Tests/AssistantPostgreSqlSafetyTests.cs`.

## Validation

Backend suite: **242 passed, no failures/skips**, with isolated local PostgreSQL enabled. The added scenario verifies registration/login; empty History (including food/type filters), Supplies and assistant conversations; zero Foods; all four empty Ask Past Me sections; zero Dashboard totals; first settings access; Calculator direct entry without saved foods; assistant recent-history, saved-food lookup and draft planning with no matching data. The LLM interpretation is a deterministic test double, not an external provider call. Empty assistant responses do not create Foods, Meals or DeliveryMeals.

The same PostgreSQL scenario then verifies creating the first Food, saving the first Meal with unconfirmed insulin, retrieving one historical meal, creating a first Ask Past Me order without insulin, and reusing it as a draft without creating another meal. It passed again after extending these first-record assertions. All records exist only in the disposable test database.

Angular suite: **78 passed**. New coverage includes empty History and filters, loading state, failed request and retry without redirect, empty Foods and first-food editor, genuine Foods failure, empty Ask Past Me and first-order editor, genuine order failure, empty Dashboard, and first assistant conversation with no history. Existing Calculator and Settings regression tests also pass. Local Karma remained alive after reporting success and was stopped after the completed summary.

`npm run build` passed with the existing Meal Assistant CSS budget warning (4.80 kB vs 4.10 kB). `git diff --check` passed. No calculation rules changed. No schema change or migration is needed; deployment requires the updated frontend assets only.

## If the deployed failure persists

Capture the first failed `/api/meals` request status, ProblemDetails response and traceId, plus the browser console exception if the request succeeds. Correlate traceId with backend logs. Do not assume a 401, 500, transport error or malformed response means the user has no meals. The current screenshot is insufficient to identify which of these occurred. These changes have been verified locally, not deployed or verified on insulincoffee.com.
