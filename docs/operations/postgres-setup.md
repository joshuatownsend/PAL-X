---
title: Postgres setup
description: Provision the database, configure the connection string, manage EF Core migrations.
---

# Postgres setup

PAL-X persists everything (jobs, reports, alerts, schedules, identity) in Postgres. EF Core 8 handles schema migrations; the API runs them on every startup.

For the connection-string setting, see **[Configuration — ConnectionStrings](../reference/configuration.md#connectionstrings)**.

## Version

PAL-X is built and tested against **PostgreSQL 16**. Older versions back to 14 should work; 13 and below are unsupported because of dependencies on features in the EF Core 8 Npgsql provider.

The shipped Docker Compose uses `postgres:16-alpine`.

## Create the database and role

Connect to your Postgres as a superuser and provision:

```sql
CREATE ROLE pal WITH LOGIN PASSWORD 'a-real-password';
CREATE DATABASE pal OWNER pal;

-- Optional: tighten privileges
REVOKE ALL ON DATABASE pal FROM public;
GRANT CONNECT, TEMPORARY ON DATABASE pal TO pal;
```

The API user needs:

- `CREATE` on the database (to apply migrations).
- `USAGE`, `CREATE` on the `public` schema.
- DML on all tables under `public` (migrations grant this on creation).

If you want a separate migration-running user, you can hand-run migrations as a privileged user and then run the API as a lower-privilege user with DML-only. See **[Run migrations out-of-band](#run-migrations-out-of-band)** below.

## Connection string

Set `ConnectionStrings:Postgres` (or env var `ConnectionStrings__Postgres`) to a standard Npgsql connection string:

```text
Host=db.internal;Port=5432;Database=pal;Username=pal;Password=…;SSL Mode=Require
```

Common parameters:

| Parameter | Notes |
|---|---|
| `Host`, `Port` | Required. |
| `Database`, `Username`, `Password` | Required. |
| `SSL Mode` | `Require` for production. Set explicitly — Npgsql does not default to TLS. |
| `Trust Server Certificate` | If using a self-signed cert. Prefer mounting the CA properly. |
| `Pooling` | `true` by default — keep it. |
| `Maximum Pool Size` | Default `100`. Rarely needs tuning. |
| `Connection Lifetime` | `0` (forever) by default; some managed Postgres providers prefer `300` to avoid idle disconnects. |
| `Application Name` | Optional but recommended (e.g., `pal-api`); shows up in `pg_stat_activity`. |

The committed `appsettings.json` ships with a development-only string pointing at `localhost:5432` with password `paldev`. **Override in production** via `appsettings.Production.json` or environment variables.

## Migrations run automatically

On every API startup, `Program.cs` calls:

```csharp
await db.Database.MigrateAsync();
```

This applies any pending migrations from `dotnet/src/Pal.Persistence/Migrations/` — there are 14 today, covering the full schema from `InitialCreate` through Phase 4 (alerts, webhooks, schedules, snooze column).

On a fresh database, the first startup creates everything. On an upgraded database, only the new migrations apply. Idempotent — running the API against an already-migrated database is a no-op.

## Run migrations out-of-band

If you don't want the API user to have `CREATE` on the database, run migrations as a privileged user before deploying the new API binary:

```bash
# Install the EF Core tool once (per user)
dotnet tool install --global dotnet-ef --version 8.*

# Run migrations against a target database, using a privileged connection string
ConnectionStrings__Postgres="Host=…;Username=pal_admin;Password=…" \
  dotnet ef database update \
    --project dotnet/src/Pal.Persistence \
    --startup-project dotnet/src/Pal.Api
```

After this, the API binary can run with a lower-privilege user that only has DML.

**Note for Windows + PowerShell:** `dotnet-ef` is not always on PATH. Invoke it as:

```powershell
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" database update `
    --project dotnet/src/Pal.Persistence `
    --startup-project dotnet/src/Pal.Api
```

This matches the pattern in `CLAUDE.md` and is the supported invocation on Windows.

## Verifying the schema

After startup, the database has tables under the `public` schema. The notable ones:

```sql
\dt public.*
```

You'll see (with `snake_case_naming_convention` applied):

- `analysis_jobs`, `analysis_results`, `analysis_job_dataset_artifact`
- `uploads`
- `packs`, `pack_versions`
- `compare_results`
- `alerts`, `webhook_sinks`, `ingestion_schedules`
- `audit_events`
- `tokens`
- `orgs`, `workspaces`, `org_memberships`
- Identity tables: `asp_net_users`, `asp_net_roles`, `asp_net_user_roles`, etc.
- `__ef_migrations_history` — EF Core's bookkeeping; **do not edit**.

Migration history:

```sql
SELECT migration_id, product_version FROM __ef_migrations_history ORDER BY 1;
```

If the API logs a migration failure on startup, that's where to debug — usually a conflict between the model and the database that arose because someone edited tables out-of-band.

## Connection pooling at scale

The Npgsql connection pool is built into the driver. The defaults handle a few thousand req/s comfortably. For higher loads, you might add PgBouncer in front in **transaction-pool** mode — but PAL-X uses some Npgsql features (notifications, async streaming) that don't play well with statement pooling. Test before relying on PgBouncer.

## Backups

Postgres is the source of truth for everything except uploaded files, reports on disk, and dataset artifacts. Back it up with `pg_dump` — see **[Backup and restore](backup-and-restore.md)**.

## Related

- **[Configuration — ConnectionStrings](../reference/configuration.md#connectionstrings)** — settings reference.
- **[Installation](installation.md)** — where the connection string is set.
- **[Backup and restore](backup-and-restore.md)** — `pg_dump` + storage backup.
- **[Upgrading](upgrading.md)** — migration discipline across versions.
