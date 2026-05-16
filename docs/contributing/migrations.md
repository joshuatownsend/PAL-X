---
title: Adding a migration
description: EF Core migration workflow — create, review, test, ship.
---

# Adding a migration

When you change a `Pal.Persistence` entity in a way that affects the database schema, EF Core needs a migration. This page walks the workflow.

For schema history, see **[Architecture — Persistence](../architecture/persistence.md)**. For operational migration concerns (out-of-band, privilege separation), see **[Operations — Postgres setup](../operations/postgres-setup.md)**.

## When you need a migration

A migration is required when you:

- Add or remove a property on an entity.
- Change a property's type, nullability, or default.
- Add or change a constraint (FK, unique index, check).
- Rename an entity or property.
- Reshape a relationship (e.g., add a new join table).

You **don't** need a migration when you:

- Change a property's `[JsonPropertyName]` attribute (JSON only; not DB).
- Change repository code that doesn't touch the schema.
- Edit application-layer DTOs (`Pal.Application/Persistence/Dtos.cs`).
- Change global query filters (filter logic changes don't generate migrations — they re-execute at query time).

Rule of thumb: if EF Core generates an empty migration, you didn't need one. If it generates something non-empty, ship it.

## Install dotnet-ef

```bash
dotnet tool install --global dotnet-ef --version 8.*
```

On Windows, the tool installs to `%USERPROFILE%\.dotnet\tools\dotnet-ef.exe` but isn't reliably on PATH. From PowerShell:

```powershell
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" --version
```

This is the supported invocation pattern from `CLAUDE.md`. Use the full path on Windows.

## Create a migration

From the repo root:

```bash
# Linux / macOS / shell where dotnet-ef is on PATH
dotnet ef migrations add <DescriptiveName> \
    --project dotnet/src/Pal.Persistence \
    --startup-project dotnet/src/Pal.Api

# Windows PowerShell
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" migrations add <DescriptiveName> `
    --project dotnet/src/Pal.Persistence `
    --startup-project dotnet/src/Pal.Api
```

`--project` points at the persistence project (where migrations live). `--startup-project` points at the API project (where the DbContext is configured with the connection string).

EF generates three files under `dotnet/src/Pal.Persistence/Migrations/`:

- `<timestamp>_<DescriptiveName>.cs` — the migration's `Up()` and `Down()`.
- `<timestamp>_<DescriptiveName>.Designer.cs` — model snapshot (auto-generated, don't edit).
- `PalDbContextModelSnapshot.cs` — updated incrementally (auto-generated, don't edit).

The descriptive name should describe what the migration does, in PascalCase: `AddDatasetArtifact`, `Phase4AlertSnoozeColumn`, `AddWorkspaceIdConstraints`. Avoid generic names like `Update1`.

## Review the generated SQL

Before committing, read the migration. EF is usually right but watch for:

| Watch for | Why it matters | Mitigation |
|---|---|---|
| `ALTER TABLE … ADD COLUMN … NOT NULL DEFAULT …` on a large table | Locks the table during the alter | Add nullable, backfill, then NOT NULL in a second migration |
| Wrong `ON DELETE` action | Cascade behaviour matters for tenant filters | Verify the action matches the intent; FK violations on test = wrong cascade |
| Drop + recreate of an index | Slow on large tables | Use `MigrationBuilder.Sql(...)` to script a more efficient operation |
| Column rename that EF saw as drop+add | Loses data | Use `MigrationBuilder.RenameColumn(...)` explicitly |
| Default value that's a function call | Some Postgres functions aren't stable | Verify the default is replayable on restore |

Look at the EF-generated `Up()` and ask "does this represent what I want to happen to a production database?" If not, edit by hand.

## Test the migration

```bash
# Apply to a local Postgres (Docker compose preferred)
docker compose up -d postgres
dotnet run --project dotnet/src/Pal.Api    # migrations apply automatically on startup
```

Watch the startup logs for the migration being applied:

```text
[Information] Applying migration '20260515123456_YourMigrationName'.
[Information] Applied migration '20260515123456_YourMigrationName'.
```

Then run the integration tests — they verify the schema works with the application layer:

```bash
dotnet test dotnet/tests/Pal.Api.Tests -c Release
```

The Testcontainers-based fixtures spin up a fresh Postgres, apply all migrations from `InitialCreate` through your new one, and exercise the affected endpoints. If something's wrong, this is where it surfaces.

## Test the rollback

EF generates a `Down()` for every migration. **Production never runs Down**, but a local rollback test is cheap insurance:

```bash
# Apply your migration
dotnet ef database update --project dotnet/src/Pal.Persistence --startup-project dotnet/src/Pal.Api

# Roll back to the previous migration
dotnet ef database update <PreviousMigrationName> --project dotnet/src/Pal.Persistence --startup-project dotnet/src/Pal.Api

# Reapply
dotnet ef database update --project dotnet/src/Pal.Persistence --startup-project dotnet/src/Pal.Api
```

If the rollback succeeds and reapplies cleanly, the migration is well-formed.

Some migrations can't be cleanly rolled back (data backfill, data deletion). That's fine — but document the limitation in the migration's class comments.

## Don't touch existing migrations

Once a migration ships to `main`, it's frozen. Edit-after-merge breaks anyone who already ran it.

If you find a mistake:

1. Generate a **new** migration that fixes the mistake.
2. Add a comment in the new migration referencing the original.
3. Ship both.

The only acceptable edit to a shipped migration is fixing a syntax error that prevents it from running at all — and even then, prefer adding a fix migration if anyone might have applied the broken version.

## Schema snapshot

EF maintains `PalDbContextModelSnapshot.cs` as the current state of the model. Every `migrations add` regenerates this file. **Commit it** alongside your migration — without it, the next migration generation can't compute the diff correctly.

If you see a PR that adds a migration but doesn't update `PalDbContextModelSnapshot.cs`, that's a problem.

## Migrations and tests

Integration tests run all migrations from scratch on every fresh Testcontainers Postgres. This is the most rigorous test of "does my migration apply cleanly?" — if the suite passes against a fresh DB, the migration is at least mechanically correct.

For unit tests, no migrations run. They use the application layer (DTOs, services) against in-memory state or mocks.

## Common patterns

### Adding a NOT NULL column

```csharp
// Don't:
migrationBuilder.AddColumn<string>(
    name: "new_field",
    table: "analysis_jobs",
    type: "text",
    nullable: false,
    defaultValue: "");

// Prefer (two-migration split for a large table):
// Migration 1:
migrationBuilder.AddColumn<string>(
    name: "new_field",
    table: "analysis_jobs",
    type: "text",
    nullable: true);
migrationBuilder.Sql("UPDATE analysis_jobs SET new_field = '...' WHERE new_field IS NULL");

// Migration 2:
migrationBuilder.AlterColumn<string>(
    name: "new_field",
    table: "analysis_jobs",
    type: "text",
    nullable: false);
```

### Adding a workspace-scoped table

Every workspace-scoped entity carries `WorkspaceId` with a FK to `workspaces.id` and `ON DELETE CASCADE`:

```csharp
migrationBuilder.CreateTable(
    name: "your_entity",
    columns: table => new
    {
        Id = table.Column<Guid>(type: "uuid", nullable: false),
        WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
        // …
    },
    constraints: table =>
    {
        table.PrimaryKey("pk_your_entity", x => x.Id);
        table.ForeignKey(
            name: "fk_your_entity_workspaces_workspace_id",
            column: x => x.WorkspaceId,
            principalTable: "workspaces",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
    });

migrationBuilder.CreateIndex(
    name: "ix_your_entity_workspace_id",
    table: "your_entity",
    column: "workspace_id");
```

Then add the corresponding query filter in `PalDbContext.OnModelCreating`.

## Related

- **[Architecture — Persistence](../architecture/persistence.md)** — schema + tenant filter background.
- **[Operations — Postgres setup](../operations/postgres-setup.md)** — running migrations in production.
- **[Testing](testing.md)** — how integration tests catch migration issues.
