---
title: First analysis — remote API
description: Stand up the PAL-X API, mint a token, submit a capture, retrieve the report.
---

# First analysis — remote API

This walkthrough takes you from a clean `docker compose up` to retrieving a finished analysis report via the hosted API. It's the same engine you used in **[First analysis — local CLI](first-analysis-local.md)**, exposed as a service.

When you reach the end, you'll have:

- the API running locally
- an admin user signed in
- an API token in your clipboard
- one completed analysis job and its report on disk

Total time: under fifteen minutes the first time, mostly waiting for Docker to pull images.

## Before you start

- Docker Desktop is installed and running.
- `.NET 8 SDK` is installed (we use `Pal.Cli` to drive the analysis).
- You've copied and edited `.env` per **[Installation](installation.md)**:
  - `POSTGRES_PASSWORD` — anything, no semicolons.
  - `PAL_BOOTSTRAP_ADMIN_PASSWORD` — at least 10 characters; used once to seed `admin@pal.local`.

## Step 1 — start the API

From the repo root:

```bash
docker compose up
```

Watch the log for two markers:

```text
pal-api  | info: Microsoft.EntityFrameworkCore.Migrations[20402]
                Applying migration '...'
pal-api  | info: Pal.Api.Bootstrap.AdminSeeder[0]
                Bootstrap admin account created: admin@pal.local
```

The first line means schema migrations applied. The second means the admin account exists. If you don't see the seed message, `PAL_BOOTSTRAP_ADMIN_PASSWORD` was unset or shorter than 10 characters — or `admin@pal.local` already exists from a previous run (the seeder is idempotent and skips silently if it does).

The API is now at `http://localhost:8080`. The Blazor UI is at `http://localhost:8080/account/login`, and the auto-generated OpenAPI docs (development mode only) at `http://localhost:8080/swagger`.

## Step 2 — sign in and mint a token

Open `http://localhost:8080/account/login` and sign in:

- email: `admin@pal.local`
- password: whatever you set as `PAL_BOOTSTRAP_ADMIN_PASSWORD`

Navigate to `Account → Tokens` (`http://localhost:8080/account/tokens`) and create a new token. Give it a name (`first-analysis` works), submit, and copy the `pal_…` value that appears — **it's shown once**. The database stores a SHA-256 hash of the token, not the token itself; if you lose it, mint a new one.

Save the token in your shell for the rest of this walkthrough:

```bash
# bash / zsh
export PAL_TOKEN="pal_..."

# PowerShell
$env:PAL_TOKEN = "pal_..."
```

## Step 3 — find your workspace

Every analysis job lives inside a workspace, and the API URL embeds the workspace ID. The bootstrap admin account is a member of the **default workspace**, which is pre-created by a database migration with a well-known ID:

```text
Default workspace ID: 00000000-0000-0000-0000-000000000002
Default org ID:       00000000-0000-0000-0000-000000000001
```

For everything below, the `--api` flag we pass to `pal remote` includes this workspace path:

```text
http://localhost:8080/api/workspaces/00000000-0000-0000-0000-000000000002
```

Save it once and reuse it:

```bash
# bash / zsh
export PAL_API="http://localhost:8080/api/workspaces/00000000-0000-0000-0000-000000000002"

# PowerShell
$env:PAL_API = "http://localhost:8080/api/workspaces/00000000-0000-0000-0000-000000000002"
```

If you create additional workspaces later (via the UI at `/account/users` for an admin, or through org management), their IDs appear on the Submit page picker at `http://localhost:8080/submit`.

## Step 4 — submit a capture

```bash
dotnet run --project dotnet/src/Pal.Cli -c Release -- \
  remote submit \
  --api $PAL_API \
  --api-key $PAL_TOKEN \
  --file fixtures/cpu-pressure/input.csv \
  --pack windows-core
```

What the flags do:

| Flag | Purpose |
|---|---|
| `--api` | Full URL ending in the workspace path. The CLI appends relative routes (`uploads`, `analysis`) to this. |
| `--api-key` | The `pal_…` token you minted in Step 2. |
| `-f`, `--file` | Path to the perfmon capture. CSV or BLG; the server infers the type from the extension. |
| `-p`, `--pack` | Pack ID to run. Repeatable. Defaults to `windows-core` if omitted. |

`pal remote submit` does two HTTP requests under the hood: it posts the file to `POST /uploads`, then posts an analysis job to `POST /analysis` referencing the upload ID. It prints the new job ID:

```text
Uploading input.csv…
Upload id: <upload-guid>
Job queued: <job-guid>
Poll status: pal remote status <job-guid> --api http://localhost:8080/api/workspaces/...
```

Copy the job GUID — you'll need it for the remaining steps.

## Step 5 — poll for completion

```bash
dotnet run --project dotnet/src/Pal.Cli -c Release -- \
  remote status --api $PAL_API --api-key $PAL_TOKEN <job-guid>
```

A typical CSV finishes in well under a second; BLG can take longer. Look for `status: completed`. If the job stays at `queued` for more than a few seconds, check the API logs with `docker compose logs -f pal-api` — the background `AnalysisWorker` may be stalled.

## Step 6 — retrieve the report

```bash
# HTML — save to disk
dotnet run --project dotnet/src/Pal.Cli -c Release -- \
  remote report --api $PAL_API --api-key $PAL_TOKEN \
  --format html --output report.html <job-guid>

# JSON — same flag set, different format
dotnet run --project dotnet/src/Pal.Cli -c Release -- \
  remote report --api $PAL_API --api-key $PAL_TOKEN \
  --format json --output report.json <job-guid>
```

Without `--output`, the report is printed to stdout (useful for piping JSON into `jq`).

Open `report.html` — it has the same structure as the local-CLI version: header, findings list, optional charts, inputs.

## Step 7 — see your job in the UI

`http://localhost:8080/jobs` lists every job in the workspace. Click into `/jobs/{guid}` for the rendered findings, the guided-diagnostics block, and (once you've set one) a comparison against a baseline.

## What's next

- **Set a baseline** — designate this completed job as the reference for future comparisons:
  ```bash
  dotnet run --project dotnet/src/Pal.Cli -c Release -- \
    remote baselines set --api $PAL_API --api-key $PAL_TOKEN <job-guid>
  ```
  A full guide on baseline types (`machine` / `role` / `workload` / `release`) and labeling will land in the Guides section.
- **Auto-compare on submit** — when you submit a future job, pass `--baseline <job-guid>` to `pal remote submit`. The API runs the comparison automatically once the job completes.
- **Include the dataset** — pass `--include-dataset` to `pal remote submit` to persist the normalized dataset alongside the report. Retrieve it later with `pal remote dataset <job-guid>`.

For the full HTTP surface, see the **HTTP API Reference** (53 endpoints across tokens, account, orgs, workspaces, packs, analysis, baselines, comparisons, trends, correlations, alerts, schedules, webhooks). *(Reference coming in a later docs section.)*
