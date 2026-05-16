---
title: Retention
description: How RetentionWorker decides what to keep and what to delete, and the env-vars that drive it.
---

# Retention

`RetentionWorker` is a background service that runs once a day and prunes old data — both from Postgres and from the on-disk storage. By default it's **disabled**; opt in by setting non-zero retention horizons.

For the settings reference, see **[Configuration — Retention](../reference/configuration.md#retention)**.

## What gets retained

Two horizons, independent:

| Setting | What's retained |
|---|---|
| `Retention:JobRetentionDays` | Analysis jobs, their `AnalysisResult` rows, their `CompareResult` rows, and on-disk artifacts (reports, datasets, uploads not referenced by remaining jobs). |
| `Retention:AuditEventRetentionDays` | `audit_events` rows. |

Set to `0` (the default) to disable retention for that horizon — data is kept forever. **Important: with both at `0`, the worker logs "retention disabled" at startup and exits immediately.** It does not even start the daily loop.

Recommended production values:

- `Retention:JobRetentionDays`: **90** for active investigation use cases; **30** for high-volume continuous monitoring; **365** for compliance/audit retention.
- `Retention:AuditEventRetentionDays`: **365** (compliance baseline).

## When does it run?

- **Startup delay**: 5 minutes after API start. Gives migrations and pack sync time to finish before touching the database.
- **Daily cadence**: every 24 hours thereafter.
- **No persistent schedule**: if the API restarts, the worker's "next run" timer resets. With normal uptime this isn't significant; with a flaky restart loop the worker might never actually fire.

## What gets deleted

### Jobs

For each job older than `JobRetentionDays`:

1. The `analysis_jobs` row is deleted.
2. Its `analysis_results` row cascades.
3. Any `compare_results` where this job was baseline or candidate are deleted.
4. The on-disk report directory (`<Storage:LocalRoot>/reports/<jobId>/`) is removed.
5. The on-disk dataset directory (`<Storage:LocalRoot>/datasets/<jobId>/`) is removed.

### Uploads

An upload is deleted only when **every job that referenced it** has been deleted. The retention repository tracks this — `DeletedUploadSha256s` is computed from "uploads no longer referenced by any job after this purge." The on-disk upload directory (`<Storage:LocalRoot>/uploads/<sha256>/`) is removed for each.

This means a long-running baseline keeps its upload alive even after newer jobs that used the same SHA-256 have been pruned.

### Audit events

Simple row-by-row deletion from `audit_events` older than `AuditEventRetentionDays`.

## Order of operations (DB vs disk)

The worker commits the database changes **first**, then deletes on-disk files. Rationale: orphaned files are cheaper to deal with than orphaned database rows. If the worker dies between the two steps, you end up with files for jobs that no longer exist — which is easy to clean up with a separate script. The opposite (DB rows pointing at deleted files) is harder.

This means a successful purge run might still leave some `*.gz` and `*.html` files behind if the storage deletion failed mid-run. They're logged at `Warning` level (`failed to delete report dir for job <id>`); you can sweep them with:

```bash
# Find storage for jobs no longer in the database
find data/storage/reports -mindepth 1 -maxdepth 1 -type d | while read dir; do
  jobid=$(basename "$dir")
  exists=$(psql -tA -c "SELECT 1 FROM analysis_jobs WHERE id = '$jobid'")
  [ -z "$exists" ] && rm -rf "$dir"
done
```

## What's NOT retained

The retention worker only touches the items above. It does **not** delete:

- **Users, orgs, workspaces, memberships** — identity data is preserved indefinitely.
- **Packs and pack versions** — keep all versions you've ever published.
- **Tokens** — managed manually via the token endpoints.
- **Webhooks, schedules, alerts** — managed manually.

If you want to prune any of those, use the corresponding API endpoint or do it directly in psql.

## Tuning the schedule

The 24-hour cadence is hard-coded. If you need a different cadence (e.g., every 6 hours for tighter SLO on storage), patch `Pal.Api/Worker/RetentionWorker.cs` (the `Task.Delay(TimeSpan.FromHours(24))` line) and rebuild.

The 5-minute startup delay is also hard-coded for the same file. For a deployment with very fast startup, you could reduce this — but the delay gives migrations and pack sync space to finish, so keep it ≥ 1 minute.

## Logging

Each run logs:

```text
RetentionWorker: starting run (job_retention_days=90, audit_retention_days=365)
RetentionWorker: purged 12 job(s), 4 compare result(s), 9 upload(s)
RetentionWorker: purged 38 audit event(s)
```

If retention is disabled:

```text
RetentionWorker: retention disabled (both settings are 0); exiting
```

A failed run logs the exception at `Error` level and the loop continues — the next 24-hour iteration tries again.

## Disabling retention temporarily

To pause retention without restarting the API, you have no in-API toggle. The options:

1. Set both env vars to `0` and restart. The worker exits and stops touching anything.
2. Stop the API entirely during the maintenance window; retention can't run if nothing's running.

For a longer disable (a release window where you want to preserve all jobs), bump the horizons to very high values and restart. The worker won't delete anything for the duration.

## Related

- **[Configuration — Retention](../reference/configuration.md#retention)** — settings.
- **[Storage layout](storage-layout.md)** — what's on disk and structured how.
- **[Backup and restore](backup-and-restore.md)** — alternative to retention is just back up what you'd otherwise delete.
- **[Monitoring](monitoring.md)** — watch retention runs in logs.
