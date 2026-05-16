---
title: Monitoring
description: Health endpoint, structured logging, what to watch in production.
---

# Monitoring

PAL-X exposes a minimal monitoring surface — `/health` for liveness, structured logs for observability. There's no built-in metrics endpoint today (no Prometheus, no OpenTelemetry exporter); these are features that haven't landed yet.

## `/health` for liveness

The only built-in monitoring endpoint:

```bash
curl http://localhost:8080/health
# {"status":"ok","version":"2026.2.0"}
```

- Always returns `200 OK` if the process is running.
- Does **not** check Postgres connectivity.
- Does **not** check pack registry sync.
- Does **not** check worker liveness.

It's a liveness probe, not a readiness probe. Wire it into:

- **Docker compose**: `healthcheck.test: ["CMD", "curl", "-sf", "http://localhost:8080/health"]`. The shipped compose file already does this.
- **Kubernetes**: `livenessProbe.httpGet.path: /health` on port `8080`. Mark unhealthy after ~3 failures (30s).
- **Systemd**: doesn't natively probe — use a periodic timer unit or external watcher.

### What about readiness?

There's no dedicated readiness endpoint. A workable substitute: query a workspace-scoped endpoint with a valid token, e.g., `GET /api/workspaces/<defaultWorkspaceId>/analysis`. A `200` or `204` confirms:

- Process is up.
- Postgres is reachable (it's queried as part of the request).
- Auth pipeline works.
- Workspace tenant filter works.

A `401` means auth's broken; a `500` means Postgres can't be reached. Use whichever response pattern fits your platform's readiness model.

## Structured logging

Logging is standard ASP.NET Core. Per-namespace levels are configured in `appsettings.json`:

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "Microsoft.EntityFrameworkCore": "Warning"
  }
}
```

Production overlay in `appsettings.Production.json` raises the default to `Warning` and keeps lifecycle + worker logs at `Information`.

### Useful log namespaces

| Namespace | What it tells you |
|---|---|
| `Microsoft.Hosting.Lifetime` | Startup, shutdown, listen URLs. |
| `Pal.Api.Worker.AnalysisWorker` | Each job's processing (pickup, completion, errors). |
| `Pal.Api.Worker.RetentionWorker` | Daily retention runs and what they deleted. |
| `Pal.Api.Worker.ScheduledIngestionWorker` | Schedule tick decisions and file pickups. |
| `Pal.Persistence.*` | Repository-level operations. |
| `Microsoft.EntityFrameworkCore.Database.Command` | SQL emitted by EF Core (very noisy at `Information`). |

To enable EF SQL logging in production temporarily:

```text
Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Information
```

Don't leave it on — it generates one log per query.

## What to watch

The signals that matter most for a small-to-medium deployment:

### Startup logs

After every restart, look for:

```text
[Information] Now listening on: http://[::]:8080
[Information] PackRegistrySyncService: synced N pack(s)
[Information] RetentionWorker: retention disabled (both settings are 0); exiting
```

If `PackRegistrySyncService` syncs zero packs but you expected some, check `Packs:Directory`.

If `RetentionWorker` says retention is disabled but you intend it to run, set the env vars.

### Worker errors

The three background services log at `Error` when something goes wrong:

- `AnalysisWorker: failed to process job <id>` — most often a malformed input or a pack validation issue. The job is marked `failed` in the DB.
- `RetentionWorker: run failed` — the next 24-hour iteration retries. If it persists, check Postgres health.
- `ScheduledIngestionWorker: failed to ingest from schedule <id>` — usually directory unreachable or permission. Schedule's `lastRunAt` updates but `lastError` would be the field to surface (today logged, not stored).

### Health failures

If your platform reports the container/service unhealthy:

1. `curl http://localhost:8080/health` directly. If it fails, the process isn't running — check journalctl or container logs.
2. If `/health` works but workspace endpoints return 5xx, Postgres is down — check the connection string and the database.

## Adding metrics

PAL-X doesn't expose a `/metrics` endpoint today. Three options:

### Option A — ASP.NET Core diagnostic counters

`dotnet-counters monitor -n pal-api` from a host with the .NET SDK installed surfaces request rates, response codes, and GC stats. Useful for ad-hoc debugging; not great for continuous monitoring.

### Option B — Add OpenTelemetry

Add `OpenTelemetry.Extensions.Hosting` + an exporter (`OpenTelemetry.Exporter.OpenTelemetryProtocol`) to `Pal.Api.csproj` and wire it in `Program.cs`. Standard pattern; out of scope for this page. Currently a feature gap.

### Option C — Log-based metrics

Pipe stdout to a log aggregator (Loki, ELK, Datadog) and build dashboards from parsed log lines — the `RetentionWorker` and `AnalysisWorker` logs give you job counts and retention numbers as structured fields.

This is what most deployments do today.

## Dashboard suggestions

If you're starting from zero, a useful dashboard has:

- **Job throughput** — count of completed jobs per hour (from `AnalysisWorker` logs or `SELECT count(*) FROM analysis_jobs WHERE status='completed'` grouped by hour).
- **Job latency** — wall-clock time from queued to completed.
- **Retention** — number of jobs purged per day (`RetentionWorker` logs).
- **Schedule pickups** — files ingested per hour per schedule.
- **Alert volume** — alerts created per hour.
- **HTTP error rate** — 5xx responses from API access logs.

The Blazor UI's own pages (`/jobs`, `/alerts`, etc.) give you live views without external tooling.

## Alerting on the alerter

If a deployment's purpose is to monitor performance and emit alerts, you need second-order monitoring: who alerts on the API itself going down?

- **External uptime monitor** hitting `/health` from outside the network. Pages on failure.
- **Log shipper alerting** on `Error`-level logs from `*.Worker.*` namespaces.
- **Postgres connection monitor** — if the DB is unreachable, the API's job processing silently stops; an external watch on Postgres health surfaces this faster than waiting for the next `/health` failure (which doesn't check DB).

These are not built in.

## Related

- **[HTTP API — Health](../reference/http-api/health.md)** — the endpoint contract.
- **[Configuration — Logging](../reference/configuration.md#logging)** — per-namespace level controls.
- **[Troubleshooting](troubleshooting.md)** — what to do when monitoring surfaces something bad.
