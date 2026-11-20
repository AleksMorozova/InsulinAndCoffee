# PR preview deployment and health verification

## Audit: PR #35, 20 November 2026

Repository: `AleksMorozova/InsulinAndCoffee`; PR `add-meal-assistant` → `stage`.
Audited head: `365bb146a53554f0627b95e29cae9177741abeed`.

GitHub already contains a native Render deployment, **7951773270**, for that exact
head SHA. Its environment is `add-meal-assistant - InsulinAndCoffee PR #35`, marked
transient and non-production, published via the **Render GitHub App**. Its latest
status is `success`, with URL `https://insulinandcoffee-pr-35.onrender.com`.
Render reported `in_progress` then `success` at 20:27:25 UTC. The app metadata
includes deployment write permission; its successful publication establishes
repository access for this deployment. Full installation settings were not accessible.

The missing item is a Render **Check**, not a missing deployment. Open PR #35's
Conversation deployment/environment section and select **View deployment**, or
open the repository's Environments/Deployments view and select the environment
above. GitHub's REST evidence is available at:

- [Deployments for the audited SHA](https://api.github.com/repos/AleksMorozova/InsulinAndCoffee/deployments?sha=365bb146a53554f0627b95e29cae9177741abeed)
- [Render deployment statuses](https://api.github.com/repos/AleksMorozova/InsulinAndCoffee/deployments/7951773270/statuses)
- [PR #35](https://github.com/AleksMorozova/InsulinAndCoffee/pull/35)

Backend and Frontend checks are successful. Vercel also has a successful
deployment for the same SHA (7951780823). No Render health Check was present.
Existing CI has only `contents: read`; this does not restrict the separately
installed Render App. No Actions workflow deploys to Render. There is no
`render.yaml` or repository definition of Render's tracked branch. CI runs on
all PRs and pushes to `main`; these are test/build triggers, not Render deploy
triggers. The Render linked branch and auto-deploy mode must be checked in its
dashboard. A preview of this PR is consistent with `stage` being linked, but
does not independently prove the service's current branch setting.

Live unauthenticated requests at 20:31 UTC returned:

| Request | Result |
| --- | --- |
| Preview `/health` | 200, `Healthy`, Kestrel origin |
| Preview `/api/foods` | 401, `WWW-Authenticate: Bearer` |
| CORS OPTIONS from current Vercel preview | 204, **no Access-Control-Allow-Origin** |
| Current Vercel preview HTML | 302 to deployment protection; assets not inspected |

The root's 401 is expected under the global authenticated fallback policy.
`/health` explicitly allows anonymous access. Its health registry currently has
no database readiness check: `Healthy` proves HTTP liveness, not database query
availability. Startup executes EF migrations before serving traffic outside
Testing, so startup completed for the running instance, but this does not
establish database isolation or ongoing database health. No remote DB queries
or migrations were executed during this audit.

## Repository changes

`.github/workflows/render-preview.yml` adds **Render Preview / Health Check**
for opened, synchronized, reopened and ready-for-review PRs targeting `stage`.
It does not create deployments. It polls native GitHub Render deployment
metadata for the event's **head SHA**, uses the latest matching deployment's
latest status, and only probes its reported URL after native success.

The URL must match the verified service pattern
`https://insulinandcoffee-pr-<number>.onrender.com`; arbitrary hosts, paths and
redirects are rejected. The slug can be changed through the non-secret
repository variable `RENDER_SERVICE_SLUG` after verifying the new service URL.
Health must return **200**, **Healthy**, and **X-Preview-Commit** equal to the
PR head SHA. This prevents an old instance from passing while Render rolls out
a new commit. The backend emits this header only when Render's
`IS_PULL_REQUEST=true`, using `RENDER_GIT_COMMIT`; the existing health writer
and JWT fallback remain intact. Do not set a static commit value manually.

Polling defaults to 1200 seconds, with 15-second intervals and bounded network
timeouts. Repository variable `RENDER_PREVIEW_TIMEOUT_SECONDS` accepts 1–1800.
The job has a 35-minute hard limit. Failures identify deployment failure,
HTTP/commit mismatch, or timeout in the job summary; success includes the URL
and verified SHA. A missing commit header fails until this code is deployed.
Only read permissions for contents, PRs and deployments are granted. No Render
token or JWT is required; the GitHub token goes only to GitHub. Fork PRs are
skipped: they need separate review and isolation before previewing. No
`pull_request_target`, deploy hooks, or privileged execution is used.

## Required dashboard setup

1. **Render:** open the base service → Settings → Build & Deploy. Verify its
   repository and linked branch (`stage` is intended for this PR flow), and
   record the auto-deploy setting. Do not change the production service's
   branch just to obtain previews. Open Previews and verify Automatic mode and
   PR #35's deployed commit. Use its native deploy log if GitHub reports failure.
2. Set the service health-check path to `/health` if it is not already set.
   This is a dashboard verification, not a repository deployment change.
3. **GitHub:** Settings → Applications → Installed GitHub Apps → Render →
   Configure. Confirm `InsulinAndCoffee` is included in repository access and
   approve any pending permission update. The current deployment proves the
   integration works; reinstalling or recreating it is unnecessary. If a future
   SHA has no deployment, compare Render's deployed SHA, app access, preview
   skip labels/title, and linked branch before changing Actions permissions.
4. Commit/push the repository changes. The new workflow runs on the next PR
   event; it cannot retroactively add a check to an old workflow run. Inspect
   Actions and its summary. Optionally select the new check in the `stage`
   branch rules after its first successful run. Leave existing CI intact.

## Vercel → matching Render preview

The production Angular build uses `environment.generated.ts`, generated by
`frontend/scripts/generate-production-environment.mjs`. Existing resolution is
`API_URL`, otherwise `https://insulinandcoffee.onrender.com/api`. `vercel.json`
only rewrites SPA routes; it does not proxy API traffic. The deployed preview's
actual `API_URL` could not be established without Vercel dashboard access or an
authenticated browser: its public URL redirects to deployment protection.

The new routing is opt-in and requires no paid service or deployment API:

1. Vercel project → Settings → Environment Variables: enable access to System
   Environment Variables. Add `RENDER_PREVIEW_SERVICE_SLUG=insulinandcoffee`
   scoped **only to Preview**. Leave Production's `API_URL` unchanged.
2. On GitHub-triggered PR builds, Vercel supplies `VERCEL_ENV=preview` and
   `VERCEL_GIT_PULL_REQUEST_ID`. The build resolves
   `https://<slug>-pr-<PR ID>.onrender.com/api`, taking precedence over an
   inherited `API_URL`. There is no PR #35 special case. Missing PR metadata
   fails clearly rather than silently using production. Branch deployments
   made before opening a PR must be redeployed after the PR exists. For
   non-PR previews, omit the opt-in variable and set an isolated `API_URL`.
3. Redeploy Vercel's PR preview after configuring the variable. Verify the
   browser Network panel sends API requests to that PR's Render URL. Vercel
   does not need to wait for Render during its static build; use the health
   check before testing interactions.
4. On the **Render preview instance**, set indexed `AllowedOrigins` entries
   for the exact Vercel origin(s) used for testing. For the audited deployment:
   `AllowedOrigins__0=https://insulin-and-coffee-demm2dfxy-aleksmorozovas-projects.vercel.app`.
   Preserve any other required preview entries using `__1`, etc. An allowed
   origin includes scheme and host, no path or trailing slash. Redeploy the
   preview after changing its environment. Test OPTIONS again and require
   `Access-Control-Allow-Origin` equal to the requested origin.

Each Vercel commit deployment can have a new origin. A verified stable branch
alias can reduce manual CORS updates; register that exact alias and use it for
testing. Do not allow every `*.vercel.app` origin or copy preview origins into
production. Automatic exact-origin synchronization would require additional
dashboard/API automation; it is deliberately not introduced here. Existing
Google sign-in may also require the preview origin in its OAuth client settings.

## Database isolation: not yet verified

No Render/Neon credentials or runtime environment listing were available.
Whether this preview shares production/stage's Neon database is **unknown**.
Render service previews initially copy the base service's environment, including
database connection information; later base-service edits do not propagate to
existing previews. Do not infer isolation from a separate Render URL.

Before exercising writes or deploying migrations, privately compare the
preview's `ConnectionStrings__DefaultConnection` destination with the base
service and Neon project/branch/database. Do not paste credentials into logs,
PRs, workflow variables or documentation. If shared, stop testing against it
and configure a separate non-production Neon branch/database with synthetic
data and scoped credentials, then deploy the preview against it. A branch
created from production can still copy sensitive data: use a sanitized test
baseline. Verify Neon limits/cost before provisioning. Nothing was provisioned
or changed by this task.

Because startup auto-migrates outside Testing, isolation must be established
**before the preview's next startup**, not after checking health. For future
automatic previews, use a dedicated preview base service whose inherited
database is isolated, or explicitly managed preview environments/Neon branches.
Review costs and migration policy first. This change does not alter production
migration behavior or attempt remote database repair.

## Lifecycle and validation

| Event | Expected behavior |
| --- | --- |
| PR opened/reopened against stage | Render creates/updates preview; health job waits for matching head SHA |
| New PR commit | Previous health job cancelled; new deployment and served SHA required |
| Render deploying / cold start | Pending native status or network timeout is retried within the deadline |
| Render ready | Native success plus exact URL, 200 Healthy, matching served commit passes |
| Render failed | Native failure/error/inactive fails with a diagnostic |
| Wrong URL, old commit, unhealthy API | Rejected or bounded timeout, never successful |
| PR retargeted/closed/merged while polling | Job fails as obsolete on its next PR metadata read |
| PR closed/merged | No new health run; Render natively deletes preview; no cleanup credentials needed |

Local verification commands:

```powershell
node --test scripts/render-preview-health.test.mjs frontend/scripts/resolve-api-url.test.mjs
dotnet test InsulinAndCoffee.sln --configuration Release --no-restore
cd frontend
npm run build
npm test -- --watch=false
```

This change passed 163 backend tests, 10 Node configuration/workflow tests, and
the Angular production build. The build retains the existing Meal Assistant
component CSS budget warning. Angular reported **52 SUCCESS** in headless Edge,
but the local browser launcher logged startup errors and failed to exit after
completion; the lingering test processes were stopped. Treat that as a local
runner limitation, not a verified clean Angular process exit. Workflow YAML
was parsed locally; a GitHub-hosted execution still needs a push.

Tests simulate deploying→ready, deploy failure, absent deployment, bad health,
wrong URL, stale deployed SHA, subsequent commits, closure, and per-PR URL
resolution with production unchanged. Backend integration tests cover anonymous
health, preview-only revision headers and protected APIs. Live results above
are for the pre-change deployment. The new GitHub workflow, revision header,
Vercel routing, fixed CORS, database isolation, and native deletion on actual PR
closure still require validation after pushing/configuring the dashboards.

References: [Render service previews](https://render.com/docs/service-previews),
[Render runtime variables](https://render.com/docs/environment-variables),
[Vercel system variables](https://vercel.com/docs/environment-variables/system-environment-variables).
