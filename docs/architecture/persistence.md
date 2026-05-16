---
title: Persistence
description: EF Core 8 + PostgreSQL — the schema, the tenant query filter, the DbContextFactory pattern, and why every workspace-scoped entity carries WorkspaceId.
---

# Persistence

PAL-X persists state in PostgreSQL through Entity Framework Core 8 with the Npgsql provider. Migrations are forward-only and applied automatically on API startup. Multi-tenancy is enforced two ways: an EF query filter scopes every read by `WorkspaceId`, and DB-level cascade constraints reject mistakes the query filter might miss.

For provisioning-side concerns (connection strings, migration tooling, backups), see **[Operations — Postgres setup](../operations/postgres-setup.md)**.

## Why Postgres

Three drivers picked Postgres specifically over alternatives:

1. **Native JSON columns.** Several entities store JSON payloads (`AnalysisResult.FindingsJson`, `IngestionSchedule.SourceConfigJson`, `AnalysisJobEntity.BaselineContextJson`). Postgres's `jsonb` is queryable and indexable; SQLite's JSON1 extension is less ergonomic; MS SQL Server JSON has historically been slower.
2. **Cascade semantics.** The workspace boundary uses DB-level `ON DELETE CASCADE` to clean up dependent rows. Postgres handles this with predictable performance.
3. **Mature managed offerings.** RDS/Aurora, Cloud SQL, Azure Database for PostgreSQL — every cloud has a first-class managed Postgres. No need to operate our own.

The codebase doesn't hard-depend on `jsonb` — JSON columns are stored as `text` and serialised with `System.Text.Json`. This keeps the door open to other databases, but no other dialect is tested today.

## Schema layout

The notable tables (snake_case via `UseSnakeCaseNamingConvention`):

```text
identity
├── asp_net_users / asp_net_roles / asp_net_user_roles    (ASP.NET Core Identity)
└── personal_access_tokens                                  (API keys, SHA-256 hashed)

tenancy
├── orgs
├── workspaces
└── org_memberships                                         (user × org × role)

data plane
├── uploads                                                 (sha256-keyed)
├── analysis_jobs
├── analysis_job_packs                                      (M:N)
├── analysis_results
├── analysis_reports                                        (json / html / markdown)
├── analysis_job_dataset_artifact                           (one per job, opt-in)
├── packs / pack_versions                                   (registry)
├── compare_results
└── workspace_audit_events

phase 4
├── alerts                                                  (open/ack/resolved + snooze)
├── webhook_sinks                                           (event subscriptions + secrets)
└── ingestion_schedules                                     (recurring source pollers)

ef bookkeeping
└── __ef_migrations_history                                 (do not edit)
```

14 migrations exist today, from `InitialCreate` (April 2026) through `Phase4AlertSnoozeColumn`. Each adds one logical capability.

## Two DbContext registrations

The DbContext is registered **twice** in `Program.cs`:

```csharp
// For Blazor Server + BackgroundService — factory-per-call avoids cross-request sharing
builder.Services.AddDbContextFactory<PalDbContext>(...);

// For Identity — Identity insists on scoped DbContext, not factory
builder.Services.AddDbContext<PalDbContext>(...);
```

The factory registration is the one nearly every repository consumes. Identity's `UserManager` / `RoleManager` need a scoped context (their internals expect it), so we register both. The two share the same options and connect to the same database.

If you find yourself debugging "DbContext is disposed" errors in a worker or a Blazor component, the answer is usually "use the factory, not the scoped context."

## Tenant query filter

Every workspace-scoped entity carries a `WorkspaceId` column and registers an EF global query filter:

```csharp
modelBuilder.Entity<AnalysisJobEntity>(e =>
{
    e.HasQueryFilter(j => !_tenantContext.WorkspaceId.HasValue
                       || j.WorkspaceId == _tenantContext.WorkspaceId.GetValueOrDefault());
});
```

The pattern is **"pass-through when tenant context is null; filter when set."** This is load-bearing:

- For an HTTP request landing in a workspace-scoped route, `TenantResolutionEndpointFilter` sets the tenant context to the resolved `WorkspaceId`. All queries scope to that workspace automatically.
- For a background worker, the tenant context is `null` (no per-tenant association); queries see all rows. Workers explicitly need to handle multi-tenant fan-out themselves.
- For a global route (e.g., `/api/orgs/*`), tenant context is `null`. Workspace-scoped entities can be queried — though most global routes don't need to be.

The `.GetValueOrDefault()` call is deliberate and **not** `.Value`. EF's parameter extraction sometimes evaluates the expression in a context where `_tenantContext.WorkspaceId` is captured as a constant — `.Value` on a null `Nullable<Guid>` would throw at expression-build time. `.GetValueOrDefault()` returns `Guid.Empty`, which is harmless because the `!HasValue ||` short-circuits first. This is documented in `CLAUDE.md` and is non-negotiable.

### Entities with query filters

| Entity | Why |
|---|---|
| `UploadEntity` | Uploaded files are workspace-scoped (deduped by sha256 within the workspace, not globally). |
| `AnalysisJobEntity` | Jobs belong to a workspace. |
| `CompareResultEntity` | Comparisons are workspace-scoped (both jobs must be in the same workspace). |
| `AlertEntity` | Alerts emit from workspace-scoped jobs. |
| `WebhookSinkEntity` | Sinks subscribe to events from one workspace. |
| `IngestionScheduleEntity` | Schedules poll into one workspace. |
| `PersonalAccessTokenEntity` | Tokens scope which workspace's data the user can act on. |

Entities **without** query filters are either global (orgs, workspaces, packs, pack_versions, identity tables) or always queried in their parent's scope via FK (analysis_job_packs, analysis_results, analysis_reports — accessed through their `AnalysisJob` row, which carries the filter).

## DB-level constraints

Beyond the query filter, the database itself enforces workspace boundaries via `ON DELETE CASCADE`. If a workspace is deleted, every workspace-scoped row is reliably removed by Postgres without depending on repository code to do the right thing.

This is belt-and-suspenders: the query filter prevents accidental reads across workspaces; the FK cascade prevents orphaned data. If you bypass EF and write raw SQL, the constraint catches you; if you accidentally drop the query filter on a new entity, the cascade still works in delete scenarios.

## Repository pattern

Every persistence concern is exposed as an interface in `Pal.Application/Persistence/I*Repository.cs`. The implementations live in `Pal.Persistence/Repositories/`. The split is:

- Interfaces define the application's needs ("get this job by id", "list completed jobs in the workspace").
- Implementations construct DbContexts via the factory, execute queries, project to DTOs.

This is the single layer the engine and surface layers cross to talk to the database. Direct `DbContext` access from `Pal.Api` endpoints or background workers is avoided — there's a repository for everything.

One exception: workers sometimes do bulk queries through the repository in ways that wouldn't make sense to expose as a "use case" interface. `RetentionRepository.PurgeJobsAsync` is one such — it's a `BackgroundService`-specific operation, not a user-facing one.

## Migrations

EF Core migrations live in `dotnet/src/Pal.Persistence/Migrations/`. Each is named with a timestamp prefix so they sort chronologically:

```text
20260424112255_InitialCreate.cs
20260424124441_UniqueUploadSha256.cs
20260425180839_Phase3aCompareBaselines.cs
20260426023941_Phase4Alerting.cs
20260426033757_Phase4AlertingUniqueIndex.cs
20260426124416_Phase4Webhooks.cs
20260426152258_AddIdentityAndTokens.cs
20260426202142_AddMultitenancy.cs
20260427012734_AddWorkspaceIdConstraints.cs
20260427173608_AddDatasetArtifact.cs
20260427203848_AddBaselineTypeAndContext.cs
20260428021952_Phase4IngestionSchedules.cs
20260428030045_Phase4AlertPolicyColumn.cs
20260428033625_Phase4AlertSnoozeColumn.cs
```

Each carries an `Up()` for the forward migration and a `Down()` for reversal — though down-migrations are never run in production. The contract is **forward-only**.

`Program.cs` runs `db.Database.MigrateAsync()` on every API startup. Idempotent — running against an already-current DB is a no-op.

To author a new migration:

```bash
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" migrations add <DescriptiveName> `
    --project dotnet/src/Pal.Persistence `
    --startup-project dotnet/src/Pal.Api
```

(Use the full path because `dotnet-ef` isn't on `PATH` — see `CLAUDE.md`.)

Then review the generated migration before commit. Auto-generated migrations are usually correct, but watch for:

- **Default value ordering on new columns.** `ALTER TABLE ... ADD COLUMN ... NOT NULL DEFAULT '...'` is fine on small tables; risky on large ones. For large tables, prefer adding nullable, backfilling, then setting NOT NULL.
- **Index changes.** EF will rebuild indexes on rename — slow on big tables.
- **Cascade deletes.** Verify the migration scripts the right `ON DELETE` action for every FK.

## Connection pooling

The Npgsql connection pool is built into the driver. Defaults handle a few thousand req/s on a single API instance. Tuning:

- `Maximum Pool Size` (default 100) — increase if you see "pool exhausted" errors.
- `Connection Lifetime` (default 0 = forever) — managed Postgres providers sometimes prefer ~300s to avoid stale pooled connections after server-side resets.

The DbContext doesn't pool DbContexts themselves; it pools the underlying connections. The factory registration creates a new DbContext per use, which is the right pattern for the workloads here (request-scoped + worker-scoped).

## What's NOT in Postgres

- **Uploaded file content** — lives on disk under `Storage:LocalRoot/uploads/<sha256>/`. The DB carries the metadata + sha256 reference.
- **Report renderings** — JSON/HTML/Markdown live on disk under `reports/<jobId>/`. The DB carries a row in `analysis_reports` pointing to each format's storage path.
- **Dataset artifacts** — gzipped JSON on disk under `datasets/<jobId>/`. The DB tracks existence + path in `analysis_job_dataset_artifact`.
- **Chart SVGs** — on disk under `reports/<jobId>/charts/`.

The split is "Postgres for metadata; filesystem for bytes." This means a backup involves both — see **[Operations — Backup and restore](../operations/backup-and-restore.md)** for the consistent-order procedure.

## Related

- **[Operations — Postgres setup](../operations/postgres-setup.md)** — provisioning side.
- **[Operations — Backup and restore](../operations/backup-and-restore.md)** — backup discipline.
- **[Data flow](data-flow.md)** — what produces the rows being persisted.
- **[Reference — Configuration: ConnectionStrings](../reference/configuration.md#connectionstrings)** — settings.
- **[ADR 0001 — Deviations from seed docs](adr/0001-deviations-from-seed-docs.md)** — content-hash IDs, snake_case fields.
