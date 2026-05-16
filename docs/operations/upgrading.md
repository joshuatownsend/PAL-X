---
title: Upgrading
description: Version pinning, migration discipline, rollback procedure.
---

# Upgrading

PAL-X is versioned via Git tags. The runtime carries its assembly version, surfaced at `GET /health`. Upgrades are forward-only at the schema level — every published migration must apply cleanly from any previous version's schema.

## Rule one — back up first

Before any upgrade, take a fresh snapshot of:

- Postgres (`pg_dump` or platform snapshot).
- `Storage:LocalRoot` (rsync or volume snapshot).

See **[Backup and restore](backup-and-restore.md)** for the procedure. The upgrade procedure assumes you have these.

If something goes wrong during the upgrade, restore is your safety net. **Don't skip backup. Don't run upgrades on Friday afternoon.**

## Upgrade sequence

### 1. Read the changelog

Check `CHANGELOG.md` (when shipped) and the diff between your current version and the target. Look for:

- Migration additions (will run on startup).
- Schema-breaking changes (rare in v1.x; would be a major bump).
- Configuration changes (new required env vars).
- Pack schema changes (e.g., v1 → v1.1).

If the upgrade introduces a config setting you must set, set it **before** restarting.

### 2. Verify migration list

The current schema is the sum of all migrations in `dotnet/src/Pal.Persistence/Migrations/`. List them sorted by name (which is timestamp-prefixed):

```bash
ls dotnet/src/Pal.Persistence/Migrations/*.cs | grep -v Designer | grep -v Snapshot
```

Compare against what's applied in your database:

```sql
SELECT migration_id FROM __ef_migrations_history ORDER BY 1;
```

The new version's migration list should be a strict superset of yours. If a migration is missing from the new version that's in your DB, you're on a divergent branch — figure out why before continuing.

### 3. Deploy the new binary

Replace the binary or container image:

```bash
# Source deploy
systemctl stop pal-api
cp -r /opt/pal-api /opt/pal-api.previous   # keep a copy for rollback
rsync -av build-output/ /opt/pal-api/
systemctl start pal-api

# Docker compose
docker compose pull api
docker compose up -d api
```

### 4. Migrations run automatically

On startup, `db.Database.MigrateAsync()` in `Program.cs` applies any pending migrations. Watch the startup logs:

```text
[Information] Applying migration '20260428033625_Phase4AlertSnoozeColumn'.
[Information] Applied migration '20260428033625_Phase4AlertSnoozeColumn'.
```

A successful migration shows the new version's last migration as the final "Applied" line.

### 5. Verify

```bash
curl http://localhost:8080/health
# Confirms new version field
```

Then a real workspace call to confirm Postgres + auth still work end-to-end:

```bash
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:8080/api/workspaces/$WS/analysis
```

## What can go wrong

### Migration fails

```text
[Error] An error occurred while applying migration '...'
```

The API exits. The database is left in whatever state the failed migration partially achieved. **Restore from backup** — don't try to hand-fix the schema. Identify the root cause (out-of-band schema edits, custom constraints, version mismatch) before retrying.

### Binary won't start

```text
[Error] Could not load file or assembly '...'
```

Likely a runtime mismatch (e.g., new binary needs .NET 9 but the host has .NET 8). Roll back to the previous binary and resolve the runtime version separately.

### Old API tries to use new schema

If a deploy has multiple replicas and one is upgraded first, the old replicas might encounter columns or tables they don't know about. EF Core is generally permissive (it ignores extra columns), but some operations might fail. **Roll the upgrade so all replicas land within minutes of each other.**

For a single-host compose deploy, this isn't an issue.

### New API tries to use old schema

If migrations didn't run (e.g., the API user doesn't have schema-modification permissions and you didn't run them out-of-band), the new binary will throw at first DB access. The fix:

```bash
# Run migrations as a privileged user
ConnectionStrings__Postgres="Host=…;Username=pal_admin;Password=…" \
  dotnet ef database update \
    --project dotnet/src/Pal.Persistence \
    --startup-project dotnet/src/Pal.Api
```

Then restart the API.

## Rollback procedure

### Option A — Restore from backup

The safest path. Restore both Postgres and storage from the pre-upgrade snapshot. Restart the previous binary.

This is the only fully-correct rollback because migrations are forward-only — there are no "down" migrations published.

### Option B — Run a single down migration

EF Core's `dotnet ef migrations script <from> <to>` produces a SQL script that can reverse a specific migration. **Don't do this on production without testing in stage first.** Some migrations involve data transformations that can't be cleanly reversed (e.g., adding a NOT NULL column with a default — the rollback recreates the column as NULL).

If you've already merged un-rollbackable changes (data backfill, data deletion), Option B is dangerous. Stick with Option A.

## Skipping versions

PAL-X migrations are tested cumulatively — you can upgrade from any prior version to any later version in one shot. The migrations apply in order automatically.

That said, **test the multi-version jump in a non-production environment first**. The "every migration applies cleanly from previous schema" property is tested for adjacent versions on every commit; the multi-step pathway is tested less rigorously.

## Pack schema upgrades

When the pack schema bumps (e.g., v1 → v1.1), existing v1 packs continue to work — the engine accepts both `schema_version` values. You don't need to re-author packs on an upgrade.

To start using v1.1 features (rolling windows), bump `schema_version` on the pack and re-publish. The validator will accept the new features only with `schema_version: "pal.pack/v1.1"`.

## Upgrade frequency

There's no fixed cadence. Suggestions:

- **Security patches**: deploy immediately. Out-of-band releases.
- **Bug fixes (point releases)**: weekly or biweekly, after a smoke test in stage.
- **Minor features**: monthly. Review changelog, plan around scheduled downtime if anything's risky.
- **Major versions**: rare. Full backup + plan window + rehearsed restore.

## Related

- **[Backup and restore](backup-and-restore.md)** — the prerequisite.
- **[Postgres setup](postgres-setup.md)** — migration tooling.
- **[Monitoring](monitoring.md)** — confirming the upgrade is healthy.
- **[Troubleshooting](troubleshooting.md)** — when it goes wrong.
