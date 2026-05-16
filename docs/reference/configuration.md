---
title: Configuration
description: Every appsettings section the PAL-X API reads, with environment-variable equivalents.
---

# Configuration

The PAL-X API (`dotnet/src/Pal.Api`) reads configuration from the standard ASP.NET Core layering:

1. `appsettings.json` — committed defaults.
2. `appsettings.{Environment}.json` — environment-specific overrides (`Development`, `Production`, etc.).
3. Environment variables — colon-delimited keys map to double-underscore env vars on Linux (`Storage__LocalRoot`) and either form on Windows.
4. Command-line arguments — `--Storage:LocalRoot=…`.

The CLI (`Pal.Cli`) takes no configuration file — every value is passed as a flag. See the [CLI reference](cli/index.md).

## `ConnectionStrings`

| Key | Default | Notes |
|---|---|---|
| `ConnectionStrings:Postgres` | `Host=localhost;Port=5432;Database=pal;Username=pal;Password=paldev` | Postgres connection string. **Override in production.** The default is committed only for local development against `docker compose up postgres`. |

Environment variable: `ConnectionStrings__Postgres`.

## `Storage`

| Key | Default | Notes |
|---|---|---|
| `Storage:LocalRoot` | `data/storage` | Root directory for persisted artifacts: uploads, generated reports, dataset archives, chart SVGs. Resolved relative to the API working directory unless absolute. |

The API creates this directory on startup if it doesn't exist. Subdirectories used:

```text
<LocalRoot>/
├── uploads/<workspaceId>/<uploadId>/…       # incoming CSV / BLG bytes
├── reports/<jobId>/report.json               # JSON report
├── reports/<jobId>/report.html               # HTML report (if generated)
├── reports/<jobId>/report.md                 # Markdown report (if requested)
├── reports/<jobId>/charts/<chartId>.svg      # ScottPlot SVG outputs
└── datasets/<jobId>/dataset.json.gz          # gzip-compressed dataset (if includeDataset:true)
```

The retention worker (see below) prunes everything under `reports/<jobId>/` and `datasets/<jobId>/` when a job's retention horizon passes.

## `Packs`

| Key | Default | Notes |
|---|---|---|
| `Packs:Directory` | `packs/thresholds` | Filesystem root where PAL-X looks for `pack.yaml` files at startup. Each subdirectory containing `pack.yaml` is a pack. The shipped packs (`windows-core`, `iis-core`, `sql-host-core`) live here. |

Packs uploaded through the API (`POST /api/packs`) live in the database, not on disk — `Packs:Directory` is only the filesystem-loaded source.

## `Retention`

The `RetentionWorker` background service runs hourly and prunes old data.

| Key | Default | Notes |
|---|---|---|
| `Retention:JobRetentionDays` | `0` | Days to keep analysis jobs and their on-disk artifacts. `0` disables retention — jobs are kept indefinitely. |
| `Retention:AuditEventRetentionDays` | `0` | Days to keep audit events. `0` disables. |

Recommended production values: `JobRetentionDays: 90`, `AuditEventRetentionDays: 365`.

## `Schedules`

The `ScheduledIngestionWorker` polls watched directories on a tick interval.

| Key | Default | Notes |
|---|---|---|
| `Schedules:Enabled` | `true` | Set to `false` to disable the worker entirely without removing schedule rows. |
| `Schedules:TickIntervalSeconds` | `30` | How often the worker wakes up and evaluates each schedule. |
| `Schedules:FileStableAgeSeconds` | `30` | Skip a file unless its `LastWriteTime` is at least this old — guards against picking up partially-written uploads. |
| `Schedules:MaxFilesPerTick` | `10` | Maximum files ingested per worker tick per schedule. Files beyond this limit are picked up on the next tick. |

These values trade off freshness against load. Lowering `TickIntervalSeconds` increases polling load on the storage; lowering `FileStableAgeSeconds` risks ingesting partial files.

## `Logging`

Standard ASP.NET Core logging. The committed `appsettings.json` defaults are:

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "Microsoft.EntityFrameworkCore": "Warning"
  }
}
```

`appsettings.Development.json` lowers `Default` to `Debug` and turns on EF Core command logging.

`appsettings.Production.json` raises `Default` to `Warning` and keeps `Microsoft.Hosting.Lifetime` and `Pal.Api.Worker` at `Information` so startup messages and worker output remain visible.

Set per-namespace levels via env vars: `Logging__LogLevel__Pal_Api_Worker=Debug`.

## Authentication and tokens

API-key auth has no `appsettings` knobs of its own — tokens are minted via `POST /api/tokens`, hashed with SHA-256, and stored in Postgres. The committed seed-admin token is created by the `IdentitySeeder` only on first boot against an empty database; rotate it before exposing the API. See [Authentication](../getting-started/first-analysis-remote.md) for the bootstrap flow.

## Choosing the environment

`ASPNETCORE_ENVIRONMENT` selects which `appsettings.{Environment}.json` overlay loads:

```bash
# Linux / macOS
ASPNETCORE_ENVIRONMENT=Production dotnet Pal.Api.dll

# Windows PowerShell
$env:ASPNETCORE_ENVIRONMENT = "Production"; dotnet Pal.Api.dll
```

The default when unset is `Production` for a published binary; `Development` is set automatically by `dotnet run` against the API project.

## Worked example — production override

A `appsettings.Production.json` for a real deployment might look like:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=db.internal;Database=pal;Username=pal_api;Password=…;SSL Mode=Require"
  },
  "Storage": {
    "LocalRoot": "/var/lib/pal/storage"
  },
  "Packs": {
    "Directory": "/etc/pal/packs"
  },
  "Retention": {
    "JobRetentionDays": 90,
    "AuditEventRetentionDays": 365
  },
  "Schedules": {
    "Enabled": true,
    "TickIntervalSeconds": 60,
    "FileStableAgeSeconds": 60,
    "MaxFilesPerTick": 20
  }
}
```

Or, equivalently, as environment variables in a container manifest:

```text
ConnectionStrings__Postgres=Host=db.internal;Database=pal;Username=pal_api;Password=…;SSL Mode=Require
Storage__LocalRoot=/var/lib/pal/storage
Packs__Directory=/etc/pal/packs
Retention__JobRetentionDays=90
Retention__AuditEventRetentionDays=365
Schedules__TickIntervalSeconds=60
```

## Related

- **[`pal remote *`](cli/pal-remote.md)** — the CLI side that talks to a running API.
- **[Exit codes](exit-codes.md)** — CLI and API failure modes.
- **[Pack schema v1](pack-schema-v1.md)** — what lives under `Packs:Directory`.
- **[Report schema](report-schema.md)** — what lives under `Storage:LocalRoot/reports/`.
