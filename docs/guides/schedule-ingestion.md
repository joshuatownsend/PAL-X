---
title: Schedule ingestion
description: Configure a recurring directory poller that submits new captures as analysis jobs.
---

# Schedule ingestion

Goal: point PAL-X at a directory of perfmon captures that gets new files periodically (a Windows agent dropping CSVs, a SMB share, a cron-driven export). The `ScheduledIngestionWorker` picks them up, submits each as an analysis job, and notifies via the configured alerts pipeline.

For the API shape, see **[HTTP API — Schedules](../reference/http-api/schedules.md)**. For the worker's tunables, see **[Reference — Configuration: Schedules](../reference/configuration.md#schedules)**.

## Create a schedule

`Admin` role required.

```bash
pal remote schedules create \
  --name hourly-windows \
  --interval-minutes 60 \
  --source '{"directory":"/var/captures","glob":"*.csv"}' \
  --pack windows-core \
  --enabled true
```

Field meanings:

| Flag | Notes |
|---|---|
| `--name` | Unique within the workspace. Used in logs and the UI. |
| `--interval-minutes` | How often to check. The worker also has a global tick interval (default 30s); the schedule's own interval is when *this* schedule re-checks its source. |
| `--source` | JSON describing where to look. Today's only source type is a directory + glob. |
| `--pack` | Pack to apply to each submitted job. Repeatable; matches `pal analyze --pack`. |
| `--enabled` | `false` to create paused; you can enable later via `set-enabled`. |

The full source-config JSON shape today:

```json
{
  "directory": "/var/captures",
  "glob": "*.csv"
}
```

Future source types (SMB, S3, HTTP) may extend the JSON shape; today only `directory` + `glob` are supported.

## How the worker decides what to ingest

The `ScheduledIngestionWorker` runs on a tick interval (`Schedules:TickIntervalSeconds`, default 30s). On each tick:

1. List the directory matching `glob`.
2. For each candidate, check `LastWriteTime` is at least `Schedules:FileStableAgeSeconds` ago (default 30s). This guards against picking up a partially-written file.
3. Up to `Schedules:MaxFilesPerTick` (default 10) files are submitted per tick per schedule.
4. Each submission becomes its own analysis job; the existing dedup-by-SHA-256 in `/uploads` means re-running the same file doesn't duplicate storage.

If you have 100 new files in a directory at the moment the worker ticks, the first 10 ingest immediately; the remaining 90 across the next 9 ticks. This is intentional flow control.

## List, get, and update

```bash
pal remote schedules list

pal remote schedules get <id>

pal remote schedules update <id> \
  --name hourly-windows \
  --interval-minutes 30 \
  --source '{"directory":"/var/captures","glob":"*.csv"}' \
  --pack windows-core \
  --pack iis-core \
  --enabled true
```

`update` is a full-record replacement (PUT semantics) — pass all fields, even unchanged ones.

## Enable / disable without re-sending the whole record

```bash
pal remote schedules disable <id>

pal remote schedules enable <id>
```

This flips just the `enabled` flag (PATCH). A disabled schedule is a config record that doesn't run — useful during maintenance.

## Delete

```bash
pal remote schedules delete <id>
```

Returns `204`. The schedule is gone; in-flight jobs created by it continue to completion.

## Permissions on the watched directory

The API process needs read access to the directory. Common pitfalls:

- **systemd unit** — set `User=` to an account that can read the directory.
- **Docker** — mount the directory as a volume; verify it's not root-only inside the container.
- **SMB share via fstab** — mount with credentials the unit user can use.

There's no built-in path traversal protection — if you point a schedule at `/etc`, the worker tries to read `/etc`. The API doesn't restrict source paths; that's an operator responsibility.

## What happens to old files

Once a file is ingested, it's not deleted. The worker tracks a per-schedule "last ingested timestamp" so it doesn't re-ingest the same files (and dedup by SHA-256 catches it anyway if the timestamp tracking lapses). To clean up old captures, run a separate retention job — PAL-X's retention worker handles its own storage but not the source directory.

## When the source is unreachable

If the directory is missing or the glob matches nothing on a tick:

- The worker logs at `Information` level and moves on.
- No alert fires today (this is a Phase 4 v1 gap).
- The schedule's `lastRunAt` updates; `nextRunAt` advances.

To monitor schedule health, query `pal remote schedules list` and watch for stale `lastRunAt`. A Phase 5 future improvement is firing alerts on schedule failures.

## End-to-end deployment shape

A typical recurring-capture deployment:

```text
Windows agent → typeperf → C:\PerfLogs\*.csv
              │
              ▼
        (file copy / SMB / scp)
              │
              ▼
[ Linux host running pal API ]
        /var/captures/*.csv
              │
              ▼
   Schedule (interval 60m, source above)
              │
              ▼
       AnalysisWorker → jobs
              │
              ▼
    PolicyEvaluator → alerts (if conditions met)
              │
              ▼
   NotificationService → webhooks
```

The Windows side captures; the Linux side analyses. The schedule is the bridge that automates the handoff.

## Related

- **[Concepts — Alerting and notification](../concepts/alerting-and-notification.md)** — the full automation loop.
- **[Reference — Configuration: Schedules](../reference/configuration.md#schedules)** — worker tunables.
- **[HTTP API — Schedules](../reference/http-api/schedules.md)** — request shapes and status codes.
- **[CLI — `pal remote schedules`](../reference/cli/pal-remote-schedules.md)** — flag reference.
