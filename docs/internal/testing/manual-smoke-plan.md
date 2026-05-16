# Manual Smoke-Test Plan — Phases 1 → 4

This is the master checklist for end-to-end manual verification of every shipped surface. CI tests guarantee unit-level correctness; this plan covers what unit tests *can't* — real DB migrations, real Postgres queries against the EF tenant filter, real worker timing, browser-rendered Blazor pages, and cross-phase integration paths.

**How to use this document:**

- Tick each box as you verify the row in your local environment
- When a row fails: open a follow-up GitHub issue, link it under [Failures discovered](#failures-discovered), then keep going
- Treat the plan as a **living document** — add rows as you find new gaps; squash existing rows when they become redundant with future automated coverage

**Conventions:**

- 🟢 marks the **golden path** (must pass for a phase to be considered shipped)
- 🟡 marks **negative paths** (rejection, validation, error)
- 🔵 marks **integration paths** (cross-phase or cross-surface)
- All `pal …` commands assume the CLI binary is built (`dotnet build dotnet/Pal.sln -c Release`); use `dotnet run --project dotnet/src/Pal.Cli -c Release -- …` if you haven't installed `pal` globally
- All `curl …` examples target a locally-running API on `http://localhost:5000` (Kestrel default); replace with `8080` if running via `docker compose up`

---

## Table of contents

- [Prerequisites & one-time setup](#prerequisites--one-time-setup)
- [Phase 1 — CLI analysis](#phase-1--cli-analysis)
- [Phase 1.5 — BLG, signing, rolling windows](#phase-15--blg-signing-rolling-windows)
- [Phase 2 — Platform (API, auth, multi-tenancy)](#phase-2--platform-api-auth-multi-tenancy)
- [Phase 3 — Intelligence (compare, baselines, trends, diagnostics)](#phase-3--intelligence-compare-baselines-trends-diagnostics)
- [Phase 4 — Operations (schedules, policy, snooze, webhooks)](#phase-4--operations-schedules-policy-snooze-webhooks)
- [Cross-phase integration scenarios](#cross-phase-integration-scenarios)
- [Failures discovered](#failures-discovered)

---

## Prerequisites & one-time setup

Run these once at the start of the test session. All later sections assume this state.

### Setup

- [ ] **S-1** Build clean: `dotnet build dotnet/Pal.sln -c Release` — exits 0, zero warnings
- [ ] **S-2** Run all unit tests: `dotnet test dotnet/Pal.sln -c Release --filter "FullyQualifiedName!~Pal.Api.Tests"` — 146/146 pass
- [ ] **S-3** Postgres container is up: `docker ps --filter "name=pal-x-postgres-1" --format "{{.Names}}: {{.Status}}"` shows `Up (healthy)`
- [ ] **S-4** API starts cleanly: `dotnet run --project dotnet/src/Pal.Api` produces "Application started" and "Bootstrap admin account created" or "user already exists"
- [ ] **S-5** Health endpoint responds: `curl -f http://localhost:5000/health` returns 200
- [ ] **S-6** Swagger UI loads: open `http://localhost:5000/swagger` in browser → endpoints visible
- [ ] **S-7** Sign in to Blazor UI: `http://localhost:5000/account/login` with `admin@pal.local` + bootstrap password → redirects to `/jobs`
- [ ] **S-8** Create an API token: `POST /api/tokens` (admin auth) returns a `pal_…` token. Save it to `$env:PAL_TOKEN` for later sections
- [ ] **S-9** Note the workspace ID: visit `/jobs`, copy the GUID from the URL or query DB. Save to `$env:PAL_WS`

### Test fixtures available

| Fixture | Path | What it exercises |
|---|---|---|
| cpu-pressure | `fixtures/cpu-pressure/input.csv` | High CPU finding (warning) |
| healthy-server | `fixtures/healthy-server/input.csv` | All-green run (no findings) |
| memory-pressure | `fixtures/memory-pressure/input.csv` | RAM-relative rule with `--host-memory-mb` |
| disk-latency | `fixtures/disk-latency/input.csv` | Disk IO finding |
| cpu-pressure-blg | `fixtures/cpu-pressure-blg/input.blg` | BLG ingestion (Windows only) |

---

## Phase 1 — CLI analysis

### CLI: `pal analyze` (CSV input)

- [ ] 🟢 **1.1** Happy path against cpu-pressure: `pal analyze --input fixtures/cpu-pressure/input.csv --output out/run1 --pack-dir packs/thresholds` exits 0, writes `out/run1/input.pal-report.json` and `out/run1/input.pal-report.html`
- [ ] 🟢 **1.2** Healthy-server run: same command on `fixtures/healthy-server/input.csv` exits 0, JSON `summary.overall_status == "healthy"`, no findings
- [ ] 🟢 **1.3** Auto-resolve packs: `pal analyze --input fixtures/cpu-pressure/input.csv --output out/auto --auto-resolve-packs` resolves and loads `windows-core` based on dataset counters
- [ ] 🟢 **1.4** Host context (RAM-relative rule): `pal analyze --input fixtures/memory-pressure/input.csv --output out/mem --host-memory-mb 8192 --host-cpu-count 4 --pack-dir packs/thresholds` produces a critical low-available-memory finding
- [ ] **1.5** `--include-charts`: report directory contains `charts/<id>.svg` files for evidence series
- [ ] **1.6** `--json-only`: only `*.pal-report.json` written; no HTML
- [ ] **1.7** `--html-only`: only `*.pal-report.html` written; no JSON
- [ ] **1.8** `--markdown`: `*.pal-report.md` written alongside others
- [ ] **1.9** `--now <ISO>` produces deterministic `generated_at_utc` — running twice with the same `--now` yields byte-identical output
- [ ] 🟡 **1.10** `--html-only --json-only` exits **2** (option conflict)
- [ ] 🟡 **1.11** `--input` pointing at a non-existent file exits **3**
- [ ] 🟡 **1.12** `--input` pointing at a malformed CSV exits **5** with a readable error
- [ ] 🟡 **1.13** `--fail-on-warning` against cpu-pressure exits **1** (warning findings present)
- [ ] **1.14** `--fail-on-warning` against healthy-server exits **0** (no warnings)

### CLI: `pal validate-pack`

- [ ] 🟢 **1.15** `pal validate-pack --path packs/thresholds/windows-core` exits 0, prints "Valid"
- [ ] 🟢 **1.16** Same for `iis-core` and `sql-host-core`
- [ ] 🟡 **1.17** `pal validate-pack --path fixtures/broken-pack` exits **4** with the validation error
- [ ] 🟡 **1.18** `pal validate-pack --path /nonexistent` exits **3**

### CLI: `pal list-packs` and `pal inspect-dataset`

- [ ] **1.19** `pal list-packs --pack-dir packs/thresholds` lists all 3 shipped packs with their versions
- [ ] **1.20** `pal inspect-dataset --input fixtures/cpu-pressure/input.csv` prints series count, time range, sample counts — no rules evaluated

### Report quality

- [ ] **1.21** Open `out/run1/input.pal-report.html` in a browser — renders without console errors, finding cards display severity badges, recommendations are visible
- [ ] **1.22** JSON report validates against `dotnet/schemas/pal.report.v1.json` (use any JSON Schema validator)
- [ ] **1.23** `finding_id` and `report_id` are SHA-256-based: same input + pack version → same IDs across runs (compare 1.9 outputs)

---

## Phase 1.5 — BLG, signing, rolling windows

### BLG ingestion (Windows only)

- [ ] 🟢 **15.1** `pal analyze --input fixtures/cpu-pressure-blg/input.blg --output out/blg --pack-dir packs/thresholds` exits 0 on Windows, produces equivalent findings to the CSV equivalent
- [ ] 🟡 **15.2** Same command on Linux/macOS: exits with `PlatformNotSupportedException` and a message containing `relog -f CSV`

### Pack signing (RSA-PSS-SHA256)

- [ ] 🟢 **15.3** `pal packs sign --pack packs/thresholds/windows-core --key tools/test-keys/dev.priv.pem` writes `pack.yaml.sig` next to `pack.yaml`
- [ ] **15.4** Sig file contains base64-encoded RSA-PSS-SHA256 signature (verify by decoding, length matches RSA modulus)
- [ ] 🟢 **15.5** `pal validate-pack --path packs/thresholds/windows-core --require-signature --trust-key tools/test-keys/dev.pub.pem` exits 0
- [ ] 🟡 **15.6** Same with `--require-signature` but no `--trust-key`: exits non-zero with "no trusted keys"
- [ ] 🟡 **15.7** Same with `--trust-key` pointing at a different public key: exits non-zero with signature-mismatch error
- [ ] 🟡 **15.8** Tamper test: modify a single byte in `pack.yaml`, re-validate with signature → fails

### Schema v1.1 — rolling windows

- [ ] **15.9** A pack with `schema_version: "pal.pack/v1.1"` and a `window:` block validates without error
- [ ] **15.10** A `schema_version: "pal.pack/v1"` pack containing `window:` is rejected with version-gate error
- [ ] **15.11** Rolling-window aggregations supported: `avg`, `min`, `max`, `p90`, `p95`, `p99` — author a tiny test pack with each aggregator and confirm rule fires

---

## Phase 2 — Platform (API, auth, multi-tenancy)

### Auth

- [ ] 🟢 **2.1** Cookie auth: browser login at `/account/login` → redirects to `/jobs`, subsequent page requests succeed without re-auth
- [ ] 🟢 **2.2** API key auth: `curl -H "Authorization: Bearer $env:PAL_TOKEN" http://localhost:5000/api/workspaces/$env:PAL_WS/analysis` returns 200
- [ ] 🟡 **2.3** Missing auth header: same endpoint returns 401
- [ ] 🟡 **2.4** Invalid token: `Authorization: Bearer pal_invalid` returns 401
- [ ] 🟡 **2.5** Login with the **rememberMe checkbox UNCHECKED** redirects (does NOT 400 — regression check from PR #18 fix)
- [ ] **2.6** Lockout after 10 failed attempts: rapid-fire bad credentials trigger `error=locked` URL parameter on the login page
- [ ] **2.7** Logout: GET `/account/logout` clears cookie, subsequent page requests redirect to login
- [ ] **2.8** Admin-only endpoint with non-admin role: `POST /api/workspaces/{ws}/webhooks` from a Viewer-role token returns 403

### Multi-tenancy

- [ ] 🟢 **2.9** Workspace prefix routing: `GET /api/workspaces/{ws}/uploads` with valid workspace returns 200; with garbage GUID returns 400; with non-existent valid GUID returns 404
- [ ] 🔵 **2.10** Cross-tenant isolation: create an upload in workspace A, attempt to retrieve it via workspace B's URL → 404 (not the upload data). Requires creating a second workspace; can be done via `POST /api/orgs/{orgId}/workspaces` as admin
- [ ] **2.11** EF tenant filter: query the alerts table from EF directly inside a SetWorkspace block — only that workspace's alerts return

### Upload + analysis lifecycle

- [ ] 🟢 **2.12** Upload: `curl -X POST -F "file=@fixtures/cpu-pressure/input.csv" -H "Authorization: Bearer $env:PAL_TOKEN" http://localhost:5000/api/workspaces/$env:PAL_WS/uploads` returns 201 with `uploadId`
- [ ] 🟢 **2.13** Re-upload same file → SHA-256 dedup: returns 200 with the **same** `uploadId`
- [ ] 🟢 **2.14** Submit analysis: `POST /api/workspaces/{ws}/analysis` with `{uploadId, packs: ["windows-core"]}` returns 202 with `analysisId`
- [ ] 🟢 **2.15** Poll status: `GET /api/workspaces/{ws}/analysis/{id}` returns `queued` → `running` → `completed` within ~30 seconds
- [ ] 🟢 **2.16** Get results: `GET /api/workspaces/{ws}/analysis/{id}/results` returns findings JSON matching the CLI's output for the same input
- [ ] 🟢 **2.17** Download HTML report: `GET /api/workspaces/{ws}/analysis/{id}/report?format=html` returns an HTML body that opens in a browser
- [ ] **2.18** Download JSON report: same with `?format=json` → JSON file
- [ ] **2.19** Download Markdown report: same with `?format=markdown` → `.md` file with GFM tables
- [ ] 🟡 **2.20** GET results before completion: returns 409
- [ ] **2.21** Submit with `includeDataset: true` → after completion, `GET /analysis/{id}/dataset` returns a gzip stream

### Background workers

- [ ] **2.22** AnalysisWorker startup: kill the API mid-job, restart → orphaned `running` job is reset to `queued` and re-processed
- [ ] **2.23** RetentionWorker: set `Retention:JobRetentionDays=1` in env, manually backdate a completed job's `CompletedAt` to 2 days ago, restart → next retention cycle deletes the job + its storage. **Note**: the worker runs every 24h; for testing, temporarily reduce the interval in `RetentionWorker.cs` or trigger via DB query

### Pack registry

- [ ] **2.24** `GET /packs` returns all 3 shipped packs
- [ ] **2.25** `GET /packs/windows-core/versions/1.0.0/validation` returns `{isValid: true}`
- [ ] **2.26** Sync on startup: drop a new pack file in `packs/thresholds/`, restart API → pack registry includes the new version

### CLI: `pal remote`

- [ ] 🟢 **2.27** `pal remote submit --file fixtures/cpu-pressure/input.csv --api http://localhost:5000/api/workspaces/$env:PAL_WS --api-key $env:PAL_TOKEN` uploads + queues, prints job ID
- [ ] 🟢 **2.28** `pal remote status <jobId>` reflects status as it advances
- [ ] 🟢 **2.29** `pal remote results <jobId>` prints findings table after completion
- [ ] **2.30** `pal remote report <jobId> --format html --output out/r.html` downloads the report
- [ ] **2.31** `pal remote packs` lists registered packs
- [ ] **2.32** `pal remote validate-pack windows-core 1.0.0` exits 0
- [ ] **2.33** `pal remote dataset <jobId> --output out/d.json.gz` downloads the dataset (only if submitted with `--include-dataset`)

---

## Phase 3 — Intelligence (compare, baselines, trends, diagnostics)

### Compare

- [ ] 🟢 **3.1** Run analysis A and B from different inputs, then `pal remote compare <jobA> <jobB>` shows new/resolved/severity-changed findings
- [ ] **3.2** REST: `POST /api/workspaces/{ws}/compare` with `{baselineJobId, candidateJobId}` returns persisted compare result with summary counts
- [ ] **3.3** UI: `/compare` page lists prior compares; clicking one shows the diff table

### Baselines

- [ ] 🟢 **3.4** `PATCH /api/workspaces/{ws}/analysis/{jobId}/baseline` with `{isBaseline: true, label: "WEB-01", type: "machine", contextJson: "{\"machine\":\"WEB-01\"}"}` succeeds
- [ ] **3.5** `pal remote baselines list` shows the designated baseline
- [ ] **3.6** `pal remote baselines list --type machine` filters correctly
- [ ] **3.7** Versioning: designate a second baseline with the same `(type, contextJson)` → `GET /analysis/baselines/versions?type=machine&contextJson={...}` returns both, ordered by `CreatedAt` desc
- [ ] **3.8** UI: `/baselines` page lists baselines; type filter works; inline version history visible
- [ ] 🔵 **3.9** Auto-compare on completion: submit job with `selectedBaselineId` → on completion, a CompareResult is automatically created (visible in `/compare`)
- [ ] **3.10** Clear baseline designation: `PATCH … {isBaseline: false}` succeeds; removed from list

### Trends

- [ ] 🟢 **3.11** `GET /api/workspaces/{ws}/trends?window=10` returns trend directions for findings across last 10 completed jobs
- [ ] **3.12** `pal remote trends --window 10` prints the trend table with direction badges
- [ ] **3.13** UI: `/trends` page renders. Each row shows direction (worsening/appearing/etc.) with color
- [ ] 🟡 **3.14** Window > 100 clamps to 100 (no error)

### Correlations

- [ ] 🟢 **3.15** `GET /api/workspaces/{ws}/correlations?window=10` returns finding pairs with `co_score`
- [ ] **3.16** `pal remote correlations --window 10` prints the pair table
- [ ] **3.17** UI: `/correlations` page renders pairs

### Guided diagnostics

- [ ] 🟢 **3.18** Complete a job with critical findings → `GET /api/workspaces/{ws}/analysis/{jobId}/diagnostics` returns `DiagnosticInsightDto` items
- [ ] **3.19** Each insight cites `affectedRuleIds` (no black-box inference)
- [ ] **3.20** `pal remote diagnostics <jobId>` prints insights
- [ ] **3.21** UI: `/jobs/{id}` page shows the collapsible "Diagnostics" `<details>` block with insight cards

---

## Phase 4 — Operations (schedules, policy, snooze, webhooks)

### Ingestion schedules

- [ ] 🟢 **4.1** Create a directory and drop 2 CSV files in it (use `fixtures/cpu-pressure/input.csv` copies). Note the absolute path
- [ ] 🟢 **4.2** Create schedule via UI: `/schedules` (admin role) → fill name/interval/path/glob/pack → save → row appears
- [ ] 🟢 **4.3** Wait one tick (≤ 30s + 30s file-stable threshold = 60s). Visit `/jobs` → 2 new analysis jobs from the schedule
- [ ] **4.4** Drop a third file → next tick → third job appears (verifies cursor + newest-first works for new arrivals)
- [ ] **4.5** Re-drop one of the original files (same content) → next tick → SHA-256 dedup; no new job
- [ ] 🟢 **4.6** Disable schedule via UI → "Enabled: No" badge; no further job ingestion
- [ ] **4.7** Re-enable → ingestion resumes
- [ ] **4.8** Delete schedule → row removed; container directory still on disk
- [ ] 🟡 **4.9** Create with invalid path (relative): UI shows validation error from service-layer exception
- [ ] 🟡 **4.10** Create with interval=1 (below 5-min minimum): rejected with `intervalMinutes` message
- [ ] 🟡 **4.11** Create with empty pack list: rejected
- [ ] 🟡 **4.12** Create with duplicate name in same workspace: API returns 409 (check via curl since UI may not surface)
- [ ] 🟡 **4.13** Schedules page from non-admin user: blocked at the page level (regression check on the PR #18 admin-role fix)
- [ ] **4.14** Worker resilience: edit a schedule's `source_config_json` to invalid JSON via DB, observe worker log warning, schedule's `NextRunAt` advances anyway (no tight retry loop — regression check on the PR #18 fix)
- [ ] **4.15** CLI: `pal remote schedules list` shows the same schedules; `pal remote schedules create --name … --interval 15 --path …` creates one; `enable`/`disable`/`delete` work end-to-end

### Alerts (existing — Phase 4 dependency)

- [ ] 🟢 **4.16** Trigger an alert: submit cpu-pressure with critical findings → `GET /api/workspaces/{ws}/alerts/data` returns an `open` alert
- [ ] **4.17** Re-submit same input → existing alert's `LastSeenAt` updates; no new alert created
- [ ] **4.18** Submit input with higher-severity finding for the same rule → alert escalates; severity updates
- [ ] **4.19** UI: `/alerts` page shows the alert with severity badge; ack and resolve buttons work
- [ ] **4.20** `pal remote alerts list` shows the alert; `acknowledge <id>` and `resolve <id> --note "fixed"` work

### Alert policy engine (Phase 4)

- [ ] 🟢 **4.21** Run 3 jobs that each fire `cpu-high` at warning severity (use 3 different cpu-pressure-like inputs that produce the same rule). After the 3rd job: alert severity is escalated to `critical` and `policy_applied = "warning-3of5-critical"`
- [ ] **4.22** UI: alert row shows the policy badge next to severity
- [ ] **4.23** `pal remote alerts list` shows the policy column populated
- [ ] **4.24** Run several jobs where the rule does NOT fire, then a job where it fires as warning again. `policy_applied` is null on that re-fire (the prior-window count dropped below threshold). **Note**: the alert's *severity* does not downgrade — `AlertService` never reduces an alert's severity below its current value. Only the policy citation is re-evaluated
- [ ] 🟡 **4.25** Insufficient history (< 4 prior completed jobs in workspace): no escalation occurs

### Alert snooze

- [ ] 🟢 **4.26** UI: click "Snooze" on an open alert, pick "1h" preset → confirm → alert row shows snoozed-until badge
- [ ] **4.27** Re-trigger the rule with a **higher** severity than the current alert (use a different input that fires the same rule at critical when the alert is currently warning). `LastSeenAt` and severity update in DB; webhook for `alert.escalated` is **NOT** delivered (snooze blocks it). Verify via the webhook receiver — see Webhooks section
- [ ] **4.28** Click "Unsnooze" → badge clears; next escalation **does** deliver webhook
- [ ] **4.29** CLI: `pal remote alerts snooze <id> --duration 30m` succeeds. `unsnooze <id>` clears
- [ ] 🟡 **4.30** `snooze --duration forever` exits with `InvalidArguments`
- [ ] 🟡 **4.31** `snooze --duration 31d` (> 30 days) — service rejects with 400
- [ ] 🟡 **4.32** `snooze --duration 1h --until 2030-01-01T00:00:00Z` — CLI rejects (mutually exclusive)
- [ ] 🟡 **4.33** Snoozing a resolved alert: returns 409
- [ ] 🟡 **4.34** Auto-clear: snooze for 1 minute, wait, re-fire rule → webhook delivers (snooze auto-expired)

### Webhooks

- [ ] **4.35** Set up a webhook receiver. Quickest: `nc -l 9999` or use `https://webhook.site` for a public endpoint
- [ ] 🟢 **4.36** UI: `/webhooks` (admin) → Add webhook with URL pointing at receiver, secret = "test123", events = `alert.created,alert.escalated`
- [ ] **4.37** "Test" button → receiver gets a `webhook.test` payload with `X-PAL-Signature: sha256=…` header
- [ ] **4.38** Verify HMAC: compute `sha256(secret, payload)`, compare to header value — must match
- [ ] 🟢 **4.39** Trigger an `alert.created` event (submit a critical finding for a new rule) → receiver gets the event
- [ ] **4.40** Trigger `alert.escalated` (re-submit with higher severity for existing rule) → receiver gets the event
- [ ] **4.41** Disable webhook → no events delivered
- [ ] **4.42** Delete webhook → row removed; no events

---

## Cross-phase integration scenarios

### End-to-end: schedule → ingestion → analysis → alert → policy → webhook → UI

- [ ] 🔵 **X-1** Setup: a schedule pointing at a directory; a webhook subscribed to `alert.created` and `alert.escalated`; both targets configured.
- [ ] 🔵 **X-2** Drop a CSV with critical findings into the directory.
- [ ] 🔵 **X-3** Within 60 seconds: `/jobs` shows new completed job; `/alerts` shows new open alert; webhook receiver got `alert.created`.
- [ ] 🔵 **X-4** Drop 2 more equivalent CSVs (different filenames, different SHA but same finding pattern) over the next two ticks.
- [ ] 🔵 **X-5** After the 3rd: alert is now `critical` (was `warning`); UI shows policy badge; webhook receiver got `alert.escalated` with `policyApplied: "warning-3of5-critical"`.
- [ ] 🔵 **X-6** Snooze the alert for 30m via UI. Drop a 4th CSV.
- [ ] 🔵 **X-7** Within 60 seconds: alert's `LastSeenAt` updates, but webhook receiver got NO new event.
- [ ] 🔵 **X-8** Delete the schedule. Alerts and webhooks are unaffected.

### Multi-tenant isolation

- [ ] 🔵 **X-9** Create a second workspace via API. Get a token scoped to it.
- [ ] 🔵 **X-10** From workspace A: create an upload, schedule, alert. Confirm via API + UI.
- [ ] 🔵 **X-11** From workspace B's token: query each resource → none of A's data is visible.
- [ ] 🔵 **X-12** From workspace B: try to PATCH workspace A's alert by guessing the GUID → 404 (regression check on the PR #18 `UpdateLatestAsync` IgnoreQueryFilters removal).

### Determinism

- [ ] 🔵 **X-13** Run cpu-pressure twice via CLI with `--now <fixed-iso>` → outputs are byte-identical.
- [ ] 🔵 **X-14** Submit cpu-pressure twice via API → both jobs produce the same `finding_id` and `report_id` (content-hash IDs).
- [ ] 🔵 **X-15** ScottPlot SVG output: render the same series twice → byte-identical.

---

## Failures discovered

Track failures here as you work through the plan. Each row should link to a follow-up issue. Format:

| Date | Test ID | Symptom | Issue |
|---|---|---|---|
| _example_ | 2.22 | Orphaned job not reset on restart | _link_ |
| | | | |

---

## Sign-off

When every checkbox above is either ✅ ticked or has a linked failure issue, the platform is considered manually verified through Phase 4. Add a dated sign-off line below.

- _Phase 4 manual smoke: <date> — \<initials\>_
