---
title: Operations
description: Production setup, postgres, auth, storage, retention, backup, monitoring, upgrades, and troubleshooting.
---

# Operations

These pages cover running PAL-X for real — not just `dotnet run` against a local Postgres. They assume you've worked through **[Getting Started](../getting-started/index.md)** and now want to deploy it somewhere durable.

If you only need to run analyses locally without persistence, the CLI (`pal analyze`) is self-contained and doesn't need any of this — it operates against `pack.yaml` files on disk and writes reports next to them. Everything below applies to the **API + Postgres + workers** stack.

## Sections

### Get the stack running

- **[Installation](installation.md)** — production runtime install from source or container.
- **[Docker Compose](docker-compose.md)** — the bundled compose file, environment variables, healthchecks.
- **[Postgres setup](postgres-setup.md)** — schema bootstrap, EF Core migrations, connection strings.
- **[Authentication and tokens](auth-and-tokens.md)** — bootstrap admin, API key minting, rotation.
- **[Orgs and workspaces setup](orgs-and-workspaces-setup.md)** — production multi-tenancy beyond the default tenant.

### Run the stack

- **[Pack distribution](pack-distribution.md)** — where packs live on the server, signing trust setup.
- **[Storage layout](storage-layout.md)** — what's on disk under `data/storage/` and why.
- **[Retention](retention.md)** — `RetentionWorker` behaviour, what gets deleted when.
- **[Monitoring](monitoring.md)** — `/health`, logging, what to watch.

### Keep the stack running

- **[Backup and restore](backup-and-restore.md)** — Postgres dump, storage directory, full disaster recovery.
- **[Upgrading](upgrading.md)** — migration discipline, version pinning, rollback.
- **[Troubleshooting](troubleshooting.md)** — port collisions, BLG on non-Windows, FK errors, broken seed.

## Quick-start reference

If you just need the minimum-viable production config, the path is:

1. Install (source or container).
2. Provision Postgres; set `ConnectionStrings:Postgres`.
3. Set `PAL_BOOTSTRAP_ADMIN_PASSWORD`.
4. Start the API; migrations run automatically.
5. `POST /account/login` with `admin@pal.local` and your bootstrap password.
6. `POST /api/workspaces/.../tokens` to mint an API key.
7. Set sensible `Retention:JobRetentionDays`.
8. Wire `/health` to your liveness probe.

Each step has its own page above.
