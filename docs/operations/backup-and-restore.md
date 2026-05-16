---
title: Backup and restore
description: Postgres dump + storage directory — the two sources of truth and how to recover the stack from each.
---

# Backup and restore

PAL-X has two stateful surfaces: **Postgres** (everything metadata) and **`Storage:LocalRoot`** (uploads, reports, datasets, charts). Both need to be backed up — and crucially, they need to be backed up **in a consistent order** so you can recover into a coherent state.

## What's in each

| Postgres | `Storage:LocalRoot` |
|---|---|
| Identity (users, roles, memberships) | Upload binary content |
| Orgs and workspaces | Report JSON/HTML/Markdown |
| Analysis job metadata (uploadId references, statuses) | Chart SVGs |
| Analysis results (findings, summary) | Dataset gzip artifacts |
| Compare results, baselines, baseline versioning | |
| Packs (YAML stored as text) and pack versions | |
| Webhooks, schedules, alerts, audit events | |
| Tokens (hashed) | |

The relationship is one-way: **DB references storage by id** (`reports/<jobId>/`, `uploads/<sha256>/`). Storage doesn't know about DB. So:

- Backups need both for consistency.
- A DB row pointing to a missing file is the failure mode you want to avoid (the report endpoint returns `404`).
- A file with no DB row is harmless (orphaned, retention sweeps clean it up).

## Backup order — DB first

The retention worker writes in the same order: DB commit, then file deletion. Backups should follow the **same order** so the reverse is true on restore:

1. **Snapshot Postgres** (point-in-time consistent).
2. **Then snapshot storage** (rsync, snapshot, tar).

If Postgres is snapshotted *after* storage, you can get a backup where the DB references jobs whose report files weren't yet on disk at the snapshot time (the job completed and committed to DB, but the file write hadn't flushed). Restoring that backup produces `404`s.

The DB-first order means: at restore time, the DB knows of jobs whose files might be missing. That's the cheaper failure mode — visible, recoverable, and the retention worker would have cleaned them up anyway.

For a fully consistent backup, **stop the API** before snapshotting either. For a hot backup, accept the small inconsistency window of running operations.

## Postgres backup

### `pg_dump` (logical)

Simplest and most portable. Single-database, captures everything:

```bash
pg_dump \
  --host db.internal \
  --username pal \
  --format custom \
  --file pal-$(date +%Y%m%d-%H%M).dump \
  pal
```

`--format custom` writes a compressed, restore-friendly format. To restore:

```bash
pg_restore \
  --host db.internal \
  --username pal \
  --dbname pal \
  --clean --if-exists \
  pal-20260515-1023.dump
```

`--clean --if-exists` drops existing objects before recreating — appropriate for a full recover. For an additive merge (e.g., importing one workspace's data), see `pg_restore --schema` / `--table` for selective restore.

### Snapshot-based (physical)

If your Postgres is managed (RDS, Cloud SQL, Azure Database for PostgreSQL), use the platform's snapshot tooling — it's faster and point-in-time consistent. Restore creates a new instance you can swap in.

### WAL archiving + point-in-time recovery

For a deployment where you need to recover to any second within the last N days, configure WAL archiving on Postgres and base backups via `pg_basebackup`. This is standard Postgres ops; out of scope for this page.

## Storage backup

### rsync

The simplest. After snapshotting Postgres, sync the storage tree to backup storage:

```bash
rsync -av --delete \
  /var/lib/pal/storage/ \
  backup-host:/backups/pal-storage-$(date +%Y%m%d-%H%M)/
```

`--delete` ensures the backup mirrors the source — orphaned files (from prior incomplete runs) get cleaned up in the backup, matching what the retention worker would do.

For incremental backups, use `rsync --link-dest=<previous>` to hard-link unchanged files.

### Tar + compress

For a single-archive snapshot:

```bash
tar -czf pal-storage-$(date +%Y%m%d-%H%M).tar.gz -C /var/lib/pal storage
```

Slower than rsync for repeated runs; appropriate for full-stack archives where you want one file.

### Volume snapshot

For Docker / cloud volumes, use the platform's snapshot tooling. EBS snapshots, ZFS snapshots, etc. — instant, point-in-time, restorable as a new volume.

## Full disaster recovery procedure

Assuming both Postgres and storage are lost and you have a recent pair of backups:

```bash
# 1. Provision new Postgres and storage volume
sudo mkdir -p /var/lib/pal/storage
sudo chown pal:pal /var/lib/pal/storage

# 2. Restore Postgres
psql -h db.internal -U postgres -c "CREATE DATABASE pal OWNER pal;"
pg_restore -h db.internal -U pal -d pal --clean --if-exists pal-snapshot.dump

# 3. Restore storage
rsync -av backup-host:/backups/pal-storage-snapshot/ /var/lib/pal/storage/

# 4. Verify
ls /var/lib/pal/storage/reports | wc -l
psql -h db.internal -U pal -c "SELECT count(*) FROM analysis_jobs;"
# The two should match (modulo jobs added between Postgres and storage backup times)

# 5. Start the API pointing at the new Postgres + storage
ASPNETCORE_ENVIRONMENT=Production \
  ConnectionStrings__Postgres="Host=db.internal;…" \
  Storage__LocalRoot=/var/lib/pal/storage \
  /opt/pal-api/pal-api

# 6. Smoke test
curl http://localhost:8080/health
# Mint a fresh token, hit a known job's report endpoint, confirm it streams.
```

## Tokens after restore

Hashed tokens restore with the rest of the DB and remain valid — the SHA-256 hashes match the raw values held by clients. No need to re-mint.

Cookies (from `account/login`) survive the restore as well, **as long as the data protection key ring is preserved**. The default key ring is held in-memory; in a fresh API process, all cookies are invalidated and users must log in again. For cookie continuity across rebuilds, configure ASP.NET Core data protection to persist its keys to a stable location (`appsettings.json` → `DataProtection:KeysPath`). This is out of scope for first install but worth doing once you have HA replicas.

## Backup frequency

Suggestions:

| Item | Cadence | Retention |
|---|---|---|
| Postgres dump | daily | 14 days |
| Storage rsync | daily | 14 days |
| Postgres WAL archive | continuous | 7 days for PITR |
| Off-site copy | weekly | 6 months |

Tune to your data-loss tolerance. For a single-team deployment used for occasional investigation, daily backup with 7-day retention is plenty. For continuous monitoring with alert tie-ins, hourly Postgres snapshots and tighter rsync are appropriate.

## What you can throw away

If the deployment is purely transient (e.g., a CI runner uses PAL-X to score a build's perf log, then doesn't care after), you can skip backup entirely. Re-run from the original captures whenever you need.

This is the "no retention, no backup" mode — set `JobRetentionDays: 7` and don't worry about it.

## Related

- **[Postgres setup](postgres-setup.md)** — provisioning the DB you'll be backing up.
- **[Storage layout](storage-layout.md)** — what's in the storage directory.
- **[Retention](retention.md)** — the worker that runs the alternative to long-term backup.
- **[Upgrading](upgrading.md)** — backup before upgrade is the rule.
