---
title: First analysis — remote API
description: Stand up the PAL-X API, mint a token, submit a capture, retrieve the report.
---

# First analysis — remote API

This walkthrough takes you from a clean `docker compose up` to retrieving a finished analysis report via HTTP. It's the same engine you used in **[First analysis — local CLI](first-analysis-local.md)**, exposed as a service.

When you reach the end, you'll have:

- the API running locally
- an admin user
- an API token
- one completed analysis job and its report, JSON or HTML

Total time: under fifteen minutes the first time, mostly waiting for Docker to pull images.

## Before you start

- Docker Desktop is installed and running.
- `.NET 8 SDK` is installed (we use `Pal.Cli` to drive the workflow; you can do the whole thing with `curl` too, both options shown).
- You've copied and edited `.env` per **[Installation](installation.md)**. The two values that matter here:
  - `POSTGRES_PASSWORD` — anything, no semicolons.
  - `PAL_BOOTSTRAP_ADMIN_PASSWORD` — 10+ characters; used once to seed the admin account.

## Step 1 — start the API

From the repo root:

```bash
docker compose up
```

Watch the log for two markers:

```
pal-api  | info: Microsoft.EntityFrameworkCore.Migrations[20402] Applying migration ...
pal-api  | info: Pal.Api.Bootstrap.AdminSeeder[0] Bootstrap admin account created: admin@pal.local
```

The first means schema migrations applied; the second means the admin account exists. If you don't see the seed message, `PAL_BOOTSTRAP_ADMIN_PASSWORD` was unset, too short (<10 chars), or `admin@pal.local` already exists from a previous run.

The API is now at `http://localhost:8080`. The Blazor UI is at `http://localhost:8080/account/login`, and (in development mode) the auto-generated OpenAPI docs are at `http://localhost:8080/swagger`.

## Step 2 — mint an API token

Browser path (easiest): sign in at `http://localhost:8080/account/login` as `admin@pal.local` with the bootstrap password, then go to `Account → Tokens` and click "Create token". Copy the value that appears — it's shown once.

`curl` path:

```bash
curl -X POST http://localhost:8080/api/tokens \
  -u admin@pal.local:<your-bootstrap-password> \
  -H "Content-Type: application/json" \
  -d '{"name":"first-analysis"}'
```

Response:

```json
{
  "id": "...",
  "name": "first-analysis",
  "token": "pal_..."
}
```

The `pal_...` value is your token. Save it; it isn't recoverable from the database (only its SHA-256 hash is stored).

```bash
export PAL_TOKEN="pal_..."  # bash / zsh
$env:PAL_TOKEN = "pal_..."  # PowerShell
```

## Step 3 — figure out your workspace

The API is multi-tenant: every analysis job belongs to a workspace inside an org. The bootstrap seeder creates a default org and workspace for the admin account; the easiest way to find their IDs is via the CLI:

```bash
dotnet run --project dotnet/src/Pal.Cli -c Release -- remote --help
```

Then list orgs and workspaces. Or, in the browser, navigate to the Submit page (`http://localhost:8080/submit`) — it lists your workspaces in a picker.

For everything below, assume you've captured the workspace ID into `$WORKSPACE_ID`.

## Step 4 — submit a capture

Easiest path — `pal remote submit`:

```bash
dotnet run --project dotnet/src/Pal.Cli -c Release -- \
  remote submit \
  --api http://localhost:8080 \
  --api-key $PAL_TOKEN \
  --input fixtures/cpu-pressure/input.csv \
  --auto-resolve-packs
```

`pal remote submit` uploads the file, creates an analysis job, and prints the job ID.

`curl` path (two requests — upload, then submit):

```bash
# 1. Upload the capture
UPLOAD_ID=$(curl -s -X POST \
  "http://localhost:8080/api/workspaces/$WORKSPACE_ID/uploads" \
  -H "Authorization: Bearer $PAL_TOKEN" \
  -F "file=@fixtures/cpu-pressure/input.csv" \
  | jq -r '.id')

# 2. Submit an analysis job referencing the upload
JOB_ID=$(curl -s -X POST \
  "http://localhost:8080/api/workspaces/$WORKSPACE_ID/analysis" \
  -H "Authorization: Bearer $PAL_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"uploadId\":\"$UPLOAD_ID\",\"autoResolvePacks\":true}" \
  | jq -r '.id')

echo "Job: $JOB_ID"
```

## Step 5 — poll for completion

Analysis runs on a background worker. Typical CSV finishes in under a second; BLG can take longer.

```bash
dotnet run --project dotnet/src/Pal.Cli -c Release -- \
  remote status --api http://localhost:8080 --api-key $PAL_TOKEN $JOB_ID
```

Or `curl`:

```bash
curl -s -H "Authorization: Bearer $PAL_TOKEN" \
  "http://localhost:8080/api/workspaces/$WORKSPACE_ID/analysis/$JOB_ID"
```

Look for `"status": "completed"`. If it stays at `"queued"` for more than a few seconds, check the API logs (`docker compose logs -f pal-api`) — the worker may be stuck.

## Step 6 — retrieve the report

JSON:

```bash
dotnet run --project dotnet/src/Pal.Cli -c Release -- \
  remote report --api http://localhost:8080 --api-key $PAL_TOKEN \
  --format json --output report.json $JOB_ID
```

HTML:

```bash
dotnet run --project dotnet/src/Pal.Cli -c Release -- \
  remote report --api http://localhost:8080 --api-key $PAL_TOKEN \
  --format html --output report.html $JOB_ID
```

Or with `curl`:

```bash
curl -s -H "Authorization: Bearer $PAL_TOKEN" \
  "http://localhost:8080/api/workspaces/$WORKSPACE_ID/analysis/$JOB_ID/report?format=html" \
  -o report.html
```

Open `report.html` — same structure as the local-CLI version: header, findings list, optional charts, inputs.

## Step 7 — see your job in the UI

`http://localhost:8080/jobs` lists every job in your workspace. Click in to `/jobs/{id}` for the rendered findings, guided diagnostics, and (once you've set one) a comparison against a baseline.

## What's next

- **Set a baseline** — designate this completed job as the reference for future comparisons. `pal remote baselines set $JOB_ID --type machine --label "WEB-01 baseline"`. Guide coming.
- **Auto-compare on submit** — when you submit a future job, pass `--baseline-id <id>` (CLI) or `selectedBaselineId` (API) and PAL-X runs the comparison automatically once the job completes.
- **Configure an alert** — turn rules into ongoing alerts via `POST /api/workspaces/{id}/alerts/...` or the `/alerts` UI page. Guide coming.
- **Schedule recurring ingestion** — drop captures into a directory and have PAL-X analyze them on a cron schedule. Guide coming.

For the full HTTP surface, see the **HTTP API Reference** (53 endpoints across tokens, account, orgs, workspaces, packs, analysis, baselines, comparisons, trends, correlations, alerts, schedules, webhooks). *(Reference coming in a later docs section.)*
