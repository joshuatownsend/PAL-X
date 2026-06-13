# Phase 2 Web Consumer — Design

> **Status**: Draft (2026-06-13)
> **Author**: Generated from codebase investigation by Claude Code executor (plan 006)
> **Follow-ups**: If accepted, the framing likely becomes an ADR; `CLAUDE.md`'s
> Phase-1 JS/TS prohibition should be explicitly revisited then.

---

## 1. What "web consumer" could mean

Three shapes are viable. They are not mutually exclusive; the question is which
one to deliver first and in what order.

### (a) Extend the existing Blazor Server app (no new stack; stays .NET)

The Blazor Server app (`dotnet/src/Pal.Api/Components/`) already exists and is
co-hosted with the REST API in a single ASP.NET Core process. It renders
server-side Razor pages for Jobs, JobDetail, Submit, Compare, Baselines, Trends,
Correlations, Alerts, Schedules, and Webhooks — essentially a full operations UI.
Navigation links for all 9 sections appear in `MainLayout.razor`. Users and API
Tokens management is exposed under `/account/users` and `/account/tokens`.

Extending this app means adding Blazor pages and components within
`dotnet/src/Pal.Api/Components/Pages/`. No new runtime, no new build toolchain,
no JS/TS. It delivers a richer UI (file-drag-and-drop, better data tables,
charts rendered inline, filtering, pagination controls) while inheriting the
existing cookie auth, antiforgery, and multitenancy plumbing.

**Trade-offs:**
- PRO: Zero additional infrastructure; single deployment artifact.
- PRO: No CORS, no JWT, no token exchange UI for the SPA itself.
- PRO: `CLAUDE.md`'s Phase-1 JS/TS rule is respected; Phase 2 remains .NET-only.
- CON: Blazor Server requires a persistent SignalR connection per user — not
  optimal for high fan-out scenarios (ops dashboards with many concurrent users).
- CON: No public API story for third-party consumers (CI pipelines, external
  tools) — that requires option (c).

### (b) A separate SPA (e.g. React/TypeScript) consuming the REST API cross-origin

A standalone frontend project under `apps/web/` (or similar) would call the
existing REST endpoints. This is the model `CLAUDE.md` explicitly defers to
Phase 2 ("JS/TS/Node land with Phase 2 when a real web consumer exists").

**Trade-offs:**
- PRO: Clean separation of concerns; frontend deployable to a CDN.
- PRO: Opens the API surface to proper testing by a realistic cross-origin caller.
- CON: Requires CORS configuration (absent today — confirmed by
  `git grep -n -i cors dotnet/src/Pal.Api` returning no matches).
- CON: Requires a token-based auth strategy for browser clients (cookie auth
  is same-origin only; see Section 3).
- CON: Introduces `package.json`, `pnpm-workspace.yaml`, and JS/TS tooling —
  the first departure from the Phase-1 .NET-only rule in `CLAUDE.md`.
- CON: Significant prerequisite gap-closure work before the SPA is viable
  (Section 4).

### (c) Harden the public API so any consumer can build on it

Regardless of (a) or (b), treating the REST API as a versioned, documented,
stable contract benefits any consumer including the CLI, external integrations,
and future SPAs. This is an enabling investment, not a consumer in itself.

**How they relate:**
- (a) and (b) are mutually exclusive as the "first Phase 2 consumer" choice;
  (c) is a prerequisite for (b) and good practice for (a).
- (a) → (c) is the lower-risk ordering: ship the Blazor UI now as a complete
  Phase 2 consumer, then harden the API for future SPA or third-party use.
- (b) → (c) simultaneously is higher effort but creates the strongest long-term
  architecture.

---

## 2. API readiness assessment

All REST endpoints are registered in `dotnet/src/Pal.Api/Program.cs`. The
workspace-scoped group lives under `/api/workspaces/{workspaceId:guid}` and is
filtered by `TenantResolutionEndpointFilter` (lines 188-199 of `Program.cs`).

### Global endpoints (no workspace scope)

| Endpoint group | File | Consumer readiness |
|---|---|---|
| `GET /health` | `HealthEndpoints.cs` | Ready. Anonymous, returns `{ status, version }`. |
| `POST /account/login` | `AccountEndpoints.cs` | Ready for browser cookie flow. Form-POST, antiforgery disabled on this endpoint. Redirects to `/jobs` on success — not suitable for SPA (needs a JSON response). |
| `GET /account/logout` | `AccountEndpoints.cs` | Ready for browser. Returns redirect — not JSON. |
| `GET /account/me` | `AccountEndpoints.cs` | Ready. Returns `{ id, email, roles }`. |
| `POST /account/users` | `AccountEndpoints.cs` | Admin-only. Ready. |
| `GET /account/users` | `AccountEndpoints.cs` | Admin-only. No pagination — unbounded. |
| `DELETE /account/users/{id}` | `AccountEndpoints.cs` | Admin-only. Ready. |
| `GET /packs` | `PackEndpoints.cs` | Ready. Returns `{ items }`. |
| `GET /packs/{id}/versions` | `PackEndpoints.cs` | Ready. Filters `StoragePath` correctly. |
| `GET /packs/{id}/versions/{version}/validation` | `PackEndpoints.cs` | Ready. |
| `GET /api/orgs/` | `OrgEndpoints.cs` | Admin-only. Returns `{ items }`. No pagination. |
| `POST /api/orgs/` | `OrgEndpoints.cs` | Admin-only. Ready. |
| `GET /api/orgs/{id}/workspaces` | `OrgEndpoints.cs` | Admin-only. No pagination. |
| `GET /api/orgs/{id}/members` | `OrgEndpoints.cs` | Admin-only. No pagination. |
| `PUT /api/orgs/{id}/members/{userId}` | `OrgEndpoints.cs` | Admin-only. Ready. |

### Workspace-scoped endpoints (`/api/workspaces/{workspaceId}`)

| Endpoint group | File | Consumer readiness |
|---|---|---|
| `POST /uploads` | `UploadEndpoints.cs` | Ready. Multipart form-data, SHA-256 dedup, 512 MB limit. Antiforgery disabled on this route. |
| `POST /analysis` | `AnalysisEndpoints.cs` | Ready. Returns `202 Accepted`. |
| `GET /analysis` | `AnalysisEndpoints.cs` | **Gap: no pagination.** Fetches all jobs (plan 002 targets this). |
| `GET /analysis/{id}` | `AnalysisEndpoints.cs` | Ready. |
| `GET /analysis/{id}/results` | `AnalysisEndpoints.cs` | Ready. Returns raw JSON findings; `409` when not completed. |
| `GET /analysis/{id}/report` | `AnalysisEndpoints.cs` | Ready. Supports `?format=html\|json\|markdown`. Streams file. |
| `GET /analysis/{id}/dataset` | `AnalysisEndpoints.cs` | Ready. Streams gzip artifact. |
| `GET /analysis/{id}/diagnostics` | `AnalysisEndpoints.cs` | Ready. Returns `{ items }`. |
| `PATCH /analysis/{id}/baseline` | `CompareEndpoints.cs` | Ready. |
| `GET /analysis/baselines` | `CompareEndpoints.cs` | **Gap: no pagination.** |
| `GET /analysis/baselines/versions` | `CompareEndpoints.cs` | Ready (query params required). |
| `POST /compare` | `CompareEndpoints.cs` | Ready. |
| `GET /compare/list` | `CompareEndpoints.cs` | **Gap: no pagination.** |
| `GET /compare/{id}` | `CompareEndpoints.cs` | Ready. |
| `GET /trends/data` | `TrendEndpoints.cs` | Ready. `?last=N` controls window. |
| `GET /correlations/data` | `CorrelationEndpoints.cs` | Ready. `?last=N` controls window. |
| `GET /alerts/data` | `AlertEndpoints.cs` | **Gap: no pagination.** Supports `?status` and `?severity`. |
| `GET /alerts/{id}` | `AlertEndpoints.cs` | Ready. |
| `PATCH /alerts/{id}/acknowledge` | `AlertEndpoints.cs` | Ready. Analyst role. |
| `PATCH /alerts/{id}/resolve` | `AlertEndpoints.cs` | Ready. |
| `PATCH /alerts/{id}/snooze` | `AlertEndpoints.cs` | Ready. 30-day cap enforced. |
| `DELETE /alerts/{id}/snooze` | `AlertEndpoints.cs` | Ready. |
| `GET /webhooks/data` | `WebhookEndpoints.cs` | Ready. Returns `{ items }` with secret redacted (only `HasSecret: bool` exposed). |
| `POST /webhooks` | `WebhookEndpoints.cs` | Admin-only. Ready. |
| `PUT /webhooks/{id}` | `WebhookEndpoints.cs` | Admin-only. Ready. |
| `DELETE /webhooks/{id}` | `WebhookEndpoints.cs` | Admin-only. Ready. |
| `POST /webhooks/{id}/test` | `WebhookEndpoints.cs` | Admin-only. Returns `502` on delivery failure. |
| `GET /schedules/data` | `ScheduleEndpoints.cs` | Ready. Returns `{ items }`. |
| `POST /schedules` | `ScheduleEndpoints.cs` | Admin-only. Ready. |
| `PUT /schedules/{id}` | `ScheduleEndpoints.cs` | Admin-only. Ready. |
| `PATCH /schedules/{id}/enabled` | `ScheduleEndpoints.cs` | Admin-only. Ready. |
| `DELETE /schedules/{id}` | `ScheduleEndpoints.cs` | Admin-only. Ready. |
| `GET /tokens` | `TokenEndpoints.cs` | Ready. Returns metadata (no raw token). Full route: `/api/workspaces/{workspaceId}/tokens`. |
| `POST /tokens` | `TokenEndpoints.cs` | Ready. Raw token returned once. Full route: `/api/workspaces/{workspaceId}/tokens`. |
| `DELETE /tokens/{id}` | `TokenEndpoints.cs` | Ready. Full route: `/api/workspaces/{workspaceId}/tokens/{id}`. |

**Summary:** The API surface is broad and functionally complete for all
operations the Blazor UI performs. The primary readiness gaps are: (1) no
pagination on list endpoints (plan 002 targets `/analysis` and `/baselines`;
`/alerts/data`, `/compare/list`, and `/account/users` also lack it); (2) no
CORS; (3) no versioning; (4) login/logout responses are browser-redirect-only,
not SPA-friendly JSON.

---

## 3. Auth for a browser consumer

### Cookie auth (Blazor Server / same-origin browser)

Blazor Server uses the `IdentityConstants.ApplicationScheme` cookie (set by
`ConfigureApplicationCookie` at `Program.cs:60-66`, 7-day sliding expiration).
`POST /account/login` is a form-POST that sets `Set-Cookie` and redirects to
`/jobs` on success. This works perfectly for the Blazor UI because the browser
navigates to the server for every page render and SignalR traffic is
same-origin.

The `app.UseAntiforgery()` call at `Program.cs:179` installs the antiforgery
middleware, but in ASP.NET Core minimal APIs antiforgery validation only runs
for endpoints that carry antiforgery metadata — it is not applied blanket to
all routes. The login endpoint explicitly opts out with `.DisableAntiforgery()`;
the upload endpoint does the same. Plain JSON endpoints (for example
`POST /api/workspaces/{workspaceId}/tokens`, `/compare`, and the alert PATCH
routes) are not automatically antiforgery-validated because they bind JSON
bodies, not form data, and carry no antiforgery metadata. The Blazor Server
pages themselves do carry antiforgery tokens (Razor generates them server-side),
so the Blazor UI flow is protected; cross-origin API callers are not covered by
this mechanism.

### API-key auth (CLI / programmatic)

`Authorization: Bearer pal_<token>` routes to `ApiKeyAuthenticationHandler`
(see `Auth/ApiKeyAuthenticationHandler.cs`). The `CookieOrApiKey` policy scheme
in `Program.cs:71-80` forwards any request whose `Authorization` header starts
with `Bearer ` to the API-key handler; all others go to the Identity cookie
scheme.

### For a cross-origin SPA

A cross-origin SPA cannot use cookie auth without `SameSite=None; Secure`
cookies and explicit CORS allowlists — which the server does not currently
configure. The clean path for a cross-origin SPA is:

1. User logs in via a Blazor or server-rendered login page (same-origin) to
   obtain a cookie, selects a workspace, and then mints an API token via
   `POST /api/workspaces/{workspaceId}/tokens` (the token endpoints are
   workspace-scoped in `Program.cs`).
2. The SPA uses `Authorization: Bearer <token>` for all subsequent API calls,
   scoping requests to the chosen workspace.
3. CORS is added to permit the SPA's origin, but only for token-authenticated
   routes — cookie-bearing requests are kept same-origin.

This avoids retrofitting CSRF protection onto a cross-origin cookie flow and
keeps the auth model simple (tokens for programmatic clients, cookies for the
server-rendered UI).

**Login / logout for a SPA**: The current `POST /account/login` returns a
redirect, not JSON. A SPA would need a JSON response endpoint (e.g.
`POST /api/auth/login` returning `{ ok, userId, roles }` alongside the cookie)
or must rely on the cookie-based flow and redirect. This is a concrete endpoint
gap for option (b).

**Antiforgery**: A cross-origin SPA using token auth bypasses antiforgery
naturally (the handler checks the `Authorization` header, not a form token).
No antiforgery changes are needed for the token path. If the SPA ever sends
cookie-bearing requests cross-origin, antiforgery would need to be reconfigured
for CORS preflight.

**Recommended path:** For option (a) (Blazor extension), no auth changes are
needed — cookie + antiforgery already work. For option (b) (SPA), add a CORS
policy and require API-key auth for all cross-origin callers; document the
"login via Blazor, mint a token, use the token in the SPA" flow as the intended
bootstrap sequence.

---

## 4. Concrete gaps to close before Phase 2

The following gaps are listed in priority order for a browser consumer. Each
is grounded in a specific file or endpoint.

### Gap 1: No CORS policy (HIGH — blocks option b entirely)

**Evidence:** `git grep -n -i cors dotnet/src/Pal.Api` returns zero matches.
Neither `services.AddCors()` nor `app.UseCors()` appears anywhere in
`Program.cs` or any endpoint file.

A cross-origin SPA request will receive no `Access-Control-Allow-Origin` header
and the browser will block it before any response body is read.

**Fix required:** Add a named CORS policy in `Program.cs` that allows the SPA
origin(s), with `AllowCredentials()` only if cookie auth is used cross-origin
(not recommended — prefer token auth for cross-origin). Wire in `app.UseCors()`
before `app.UseAuthentication()`.

### Gap 2: List endpoints have no pagination (HIGH — correctness / DoS risk)

**Evidence:**
- `GET /analysis` — `AnalysisEndpoints.cs:43-49`, calls
  `analysis.ListJobsAsync(status)` with no limit/offset.
- `GET /analysis/baselines` — `CompareEndpoints.cs:39-45`, calls
  `analysis.ListBaselinesAsync(type)` with no limit/offset.
- `GET /alerts/data` — `AlertEndpoints.cs:9-15`, calls
  `alerts.ListAsync(status, severity)` with no limit/offset.
- `GET /compare/list` — `CompareEndpoints.cs:98-103`, calls
  `compare.ListAsync()` with no limit/offset.
- `GET /account/users` — `AccountEndpoints.cs:82-88`, calls
  `userManager.Users.ToListAsync()` with no limit.

Plan 002 targets `/analysis` and `/baselines` as the highest-priority items.
A Phase 2 UI rendering job lists will hit this on every page load.

**Fix required:** Add `?page=&pageSize=` (or `?cursor=`) query parameters with
server-side `LIMIT`/`OFFSET` (or keyset). Plan 002 defines the implementation
shape.

### Gap 3: Swagger/OpenAPI exposed only in Development (MEDIUM — developer experience)

**Evidence:** `Program.cs:170-174`:
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

External consumers (including SPA developers) cannot browse the API contract
in staging or production. At minimum, the Swagger JSON should be exposed in all
environments behind the same auth that protects the API (authenticated users only).

**Fix required:** Move `app.UseSwagger()` outside the `IsDevelopment()` guard,
optionally behind `.RequireAuthorization()`. Keep the UI dev-only if desired,
but the JSON spec (`/swagger/v1/swagger.json`) should be universally accessible
to authenticated callers.

### Gap 4: No API versioning strategy (MEDIUM — long-term stability)

**Evidence:** The Swagger document is registered as `"v1"` (`Program.cs:146`)
but there is no versioning middleware (`Asp.Versioning` or equivalent). All
endpoints are unversioned routes. The health endpoint hard-codes
`version = "2026.2.0"` as an application version, not an API version.

A SPA or third-party client needs a stable contract. Without versioning, any
breaking change is a deployment-coupled event for all consumers simultaneously.

**Fix required:** Adopt a versioning strategy before opening the API to external
consumers. The simplest approach is URL-path versioning (`/api/v1/...`); header
versioning is an alternative. The choice of strategy should be recorded as an
ADR.

### Gap 5: Inconsistent error-response shape (LOW — developer experience)

**Evidence:** Error responses across endpoints use several different shapes:
- Plain strings: `Results.BadRequest("At least one pack is required")`
  (`AnalysisEndpoints.cs:21`)
- Anonymous objects with `error` key:
  `Results.BadRequest(new { error = "..." })` (`WebhookEndpoints.cs:28`)
- Anonymous objects with `errors` list:
  `Results.BadRequest(new { errors = ... })` (`AccountEndpoints.cs:73`)
- `Results.Problem(...)` for 409/500 cases (`AnalysisEndpoints.cs:63`)
- `Results.Conflict("Alert is not in 'open' state")` — plain string
  (`AlertEndpoints.cs:34`)

A SPA must handle all of these shapes. A consistent RFC 7807 `ProblemDetails`
structure across all error responses would simplify client-side error handling.

**Fix required:** Standardize on `Results.Problem()` / `TypedResults.Problem()`
or a thin extension wrapper for all error cases. This can be done incrementally
without breaking existing behavior.

### Gap 6: Login/logout are redirect-only — not SPA-friendly (LOW — only for option b)

**Evidence:** `AccountEndpoints.cs:31-35`:
```csharp
if (result.Succeeded)
    return Results.Redirect("/jobs");
...
return Results.Redirect("/account/login?error=invalid");
```

A SPA calling `POST /account/login` will receive a `302` redirect it cannot
meaningfully follow for JSON state management.

**Fix required (only for option b):** Add a JSON-response login variant
(`POST /api/auth/session` or accept `Content-Type: application/json` on the
existing route) that returns `{ ok: true, userId, roles }` on success and
`{ ok: false, reason }` on failure, without redirecting.

---

## 5. Recommended phasing

### Increment 1 (ship now — no new gaps needed): Blazor UI as Phase 2 consumer

The existing Blazor Server application is already a fully functional web
consumer. It covers 100% of the API surface that the CLI covers (submit, jobs,
compare, baselines, trends, correlations, alerts, schedules, webhooks, tokens)
and adds a visual UI on top. Declaring this "Phase 2 shipped" is accurate and
requires no new gap-closure work beyond what is already planned.

**Value delivered:** Operations engineers can use PAL-X entirely from a browser
without installing the CLI. The REST API is the contract — the Blazor UI is the
consumer, proving the API is complete.

**Effort:** Zero — it already ships. Close plan 002 (pagination) to make the
jobs list scale before calling it production-ready.

### Increment 2 (small effort, high value): Pagination + error-shape cleanup

Close Gaps 2 and 5 to make the API more robust for any consumer. Gap 2
(pagination, HIGH) is the most impactful item here: plan 002 covers
`/analysis` and `/baselines`; extend it to `/alerts/data` and `/compare/list`.
Gap 5 (error-shape consistency, LOW) can be addressed incrementally alongside
pagination. No new infrastructure required for either.

**Effort:** S–M. Plan 002 is already written and awaiting execution.

### Increment 3 (medium effort): API hardening — Swagger, versioning

Expose OpenAPI to authenticated users in all environments. Choose and implement
a versioning strategy. Record the decision as an ADR.

**Effort:** M. Versioning is a schema-level decision that must happen before
external consumers lock on to routes.

### Increment 4 (large effort, introduces JS/TS): SPA consumer

Only after Increments 1–3 are complete: add CORS (Gap 1), add a JSON auth
endpoint (Gap 6), and build the SPA in `apps/web/`. This is the increment that
amends `CLAUDE.md`'s Phase-1 JS/TS prohibition and introduces the first Node.js
tooling in the repo.

**Effort:** L. Requires a framework decision (React/Vue/Svelte), a build
pipeline, and a hosting decision (same-origin vs. split). See Section 6 for
open questions.

---

## 6. Open questions for the maintainer

1. **Hosting model (same-origin vs. split):** Should the SPA (if built) be
   served from the same ASP.NET Core process as the API (via `UseStaticFiles`
   on the built SPA bundle), or deployed separately (e.g. to a CDN/Vercel)?
   Same-origin eliminates CORS and simplifies auth. Split origin simplifies
   independent deployment cadences but requires CORS and token auth.

2. **Target audience (internal ops vs. external):** Is Phase 2 a richer UI for
   the internal operations team only (favors Blazor extension, no CORS needed),
   or is the API intended to be consumed by external tools and third parties
   (favors API hardening and SPA)? This determines whether API versioning and
   CORS are urgent.

3. **Does Phase 2 introduce the first JS/TS in the repo?** `CLAUDE.md` currently
   prohibits all JS/TS ("There is no JavaScript, TypeScript, or Node.js in
   Phase 1"). If the answer is to build a SPA (option b), this rule must be
   explicitly revised. The design document you are reading is the natural place
   to initiate that conversation — but the decision and the `CLAUDE.md` edit
   should be made by the maintainer, not the executor. If the answer is to
   extend Blazor (option a), the Phase-1 rule remains intact and Phase 2 is
   a zero-risk increment.

4. **Authentication UX for the SPA bootstrap flow:** If token auth is the chosen
   path for a cross-origin SPA, what is the intended UX for a first-time user?
   The current flow (log in via Blazor → mint a token → use the token in the
   SPA) is developer-friendly but awkward for end-users who just want to open
   a URL. An embedded login page in the SPA requires a JSON auth endpoint
   (Gap 6) and careful CORS/cookie/antiforgery coordination.

5. **Role and workspace selection in the SPA:** The API is multitenant
   (Org → Workspace). A SPA must let users select their workspace. The current
   Blazor app does not show a workspace picker — it uses a seeded
   `DefaultTenant.WorkspaceId`. A production multi-workspace SPA needs a
   workspace selection step backed by `GET /api/orgs/{id}/workspaces`.

---

*This document is intentionally descriptive and does not make a binding
recommendation between options (a) and (b). The recommended ordering in
Section 5 is based on effort/risk, but the final choice belongs to the
maintainer. If this design is accepted, raise an ADR to record the decision
and update `CLAUDE.md` accordingly.*
