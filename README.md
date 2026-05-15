# PAL-X

Deterministic, explainable performance analysis for Windows, IIS, and SQL Server environments.
PAL-X ingests Windows Performance Monitor captures, runs declarative rule packs against the data, and produces structured JSON and HTML reports with evidence-linked findings and recommendations.

```
pal analyze --input server01.csv --output reports/ --auto-resolve-packs --host-memory-mb 16384
```

```
[windows-core] high-cpu-sustained       ⚠  warning   avg 89.4% > 80% for 38% of window
[windows-core] high-paging-activity     ⚠  warning   Pages/sec avg 1 240 > 1 000
[windows-core] low-available-memory     ✗  critical  available 312 MB < 5% of 16 384 MB

Overall status: CRITICAL  (3 findings — 1 critical, 2 warnings)
→ reports/server01.pal-report.json
→ reports/server01.pal-report.html
```

---

## Status

| Phase | Description | State |
|-------|-------------|-------|
| 1 — Engine | CLI analysis tool, rule packs, JSON + HTML reports | **Shipped** |
| 2 — Platform | REST API, async job queue, upload storage, multitenancy | **Shipped** |
| 3 — Intelligence | Baselines, run comparison, trends, cross-signal correlation | **Shipped** |
| 4 — Operations | Scheduled ingestion, alerting, webhook notifications, retention | **Shipped** |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for the API and integration tests)

---

## Quick start — CLI

```bash
# Clone (submodule is legacy reference; --recurse-submodules is optional)
git clone https://github.com/your-org/pal-x
cd pal-x

# Build
dotnet build dotnet/Pal.sln -c Release

# Analyze a CSV capture
dotnet run --project dotnet/src/Pal.Cli -c Release -- \
  analyze \
  --input path/to/server.csv \
  --output out/ \
  --auto-resolve-packs \
  --host-memory-mb 16384 \
  --host-cpu-count 8

# Open the report
start out/server.pal-report.html
```

The CLI auto-resolves `windows-core` on every run. It also loads `iis-core` if IIS counters are present, and `sql-host-core` if SQL Server counters are present.

---

## Quick start — API (Docker)

```bash
# Copy and edit environment values — both placeholders MUST be replaced
cp .env.example .env
# Edit .env:
#   POSTGRES_PASSWORD=<your-postgres-password>     (no semicolons; inlined into Npgsql conn string)
#   PAL_BOOTSTRAP_ADMIN_PASSWORD=<10+ chars>       (required to seed admin@pal.local)

# Start PostgreSQL and the API
docker compose up
```

On first run the seeder creates an `admin@pal.local` account using `PAL_BOOTSTRAP_ADMIN_PASSWORD`. Sign in at `http://localhost:8080/account/login`. The API is also documented at `http://localhost:8080/swagger`.

To get an API token for CLI use:

```bash
curl -X POST http://localhost:8080/api/tokens \
  -u admin@pal.local:<password> \
  -H "Content-Type: application/json" \
  -d '{"name": "my-token"}'
```

> **Bootstrap is one-shot**: if `admin@pal.local` already exists, the seeder skips silently — changing `PAL_BOOTSTRAP_ADMIN_PASSWORD` afterwards has no effect. Rotate via the `/account/users` admin UI or reset directly in the DB.

---

## Local development (without Docker)

`docker compose` reads `.env` automatically; `dotnet run` does not — you have to set environment variables in your shell. Postgres still runs in a container so the API has somewhere to talk to.

```powershell
# 1. Start ONLY the Postgres container (the api service is built from source for local dev)
docker compose up -d postgres

# 2. Set the bootstrap admin password in the current PowerShell session
$env:PAL_BOOTSTRAP_ADMIN_PASSWORD = "ChangeMeLocally1"  # 10+ chars; no special-char requirement

# 3. Run the API
dotnet run --project dotnet/src/Pal.Api
```

Watch the startup log for `Bootstrap admin account created: admin@pal.local` — that confirms the seeder ran. Sign in at `http://localhost:5000/account/login` (Kestrel default port for `dotnet run`).

**Persisting credentials between sessions** (alternative to the env var dance):

```powershell
dotnet user-secrets init --project dotnet/src/Pal.Api
dotnet user-secrets set "Auth:BootstrapAdminPassword" "ChangeMeLocally1" --project dotnet/src/Pal.Api
```

User-secrets are loaded in the `Development` environment automatically and are stored outside the repo.

### Port 5432 collision

If you have a native PostgreSQL service installed on Windows (`postgresql-x64-XX`), it owns port 5432 and Docker's port forward will appear to bind but actually only catch IPv6 traffic — the API will silently authenticate against the *wrong* server and fail.

The compose `ports:` line reads from `POSTGRES_PORT_HOST` with a default of 5432. To use a different host port (5433 here), set both env vars together — the first tells compose where to publish, the second tells the API where to connect:

```powershell
# In .env (consumed by docker compose):
POSTGRES_PORT_HOST=5433

# In your PowerShell session for `dotnet run` (consumed by the API):
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5433;Database=pal;Username=pal;Password=$env:POSTGRES_PASSWORD"

docker compose up -d --force-recreate postgres
dotnet run --project dotnet/src/Pal.Api
```

Or, if you'd rather not run two Postgres servers at all, stop the native service for the duration of dev work: `Stop-Service postgresql-x64-XX`.

---

## CLI reference

```
pal analyze            Analyze a CSV or BLG capture and emit report artifacts
pal validate-pack      Validate a pack directory against the pal.pack/v1 schema
pal inspect-dataset    Parse and summarize a capture without running rules
pal list-packs         List packs found on the search path
pal packs sign         Sign a pack directory, producing pack.yaml.sig
pal remote             Interact with a running PAL API server
```

`pal remote` exposes a working set against a running API: `submit`, `status`, `results`, `report`, `dataset`, `compare`, `diagnostics`, `trends`, `correlations`, `packs`, `validate-pack`, and the `baselines`, `alerts`, and `schedules` sub-branches. Run `pal remote --help` for the full surface.

### `pal analyze` key options

| Option | Description |
|--------|-------------|
| `--input <path>` | Path to CSV or BLG file |
| `--output <dir>` | Output directory for report artifacts |
| `--format <fmt>` | Input format: `auto` (default), `csv`, or `blg` |
| `--auto-resolve-packs` | Auto-load applicable packs based on dataset counters |
| `--pack <id>` | Explicit pack ID to load (repeatable) |
| `--pack-dir <path>` | Additional search path for packs (repeatable) |
| `--host-memory-mb <n>` | Total physical memory — required for RAM-relative rules |
| `--host-cpu-count <n>` | Logical processor count — required for CPU-count-relative rules |
| `--markdown` | Also emit a Markdown report alongside HTML/JSON |
| `--include-charts` | Emit SVG chart files alongside the report |
| `--chart-limit <n>` | Maximum charts to generate (default: 20) |
| `--json-only` / `--html-only` | Emit only one format |
| `--fail-on-warning` | Exit 1 if any warning finding is produced |
| `--now <iso>` | Override `generated_at_utc` for deterministic test output |

> **BLG files**: native BLG ingestion is supported on Windows (x64) via PDH interop. On non-Windows platforms, convert first with:
> `relog -f CSV server.blg -o server.csv`

### Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success, no findings above threshold |
| 1 | Analysis completed with warning findings (`--fail-on-warning`) |
| 2 | Invalid option combination (e.g. `--html-only --json-only`) |
| 3 | Input file not found or unreadable |
| 4 | Pack validation failed |
| 5 | Analysis engine error |

---

## Report format

Reports are written to `<output>/<stem>.pal-report.json` and `<output>/<stem>.pal-report.html`.

The JSON report conforms to `dotnet/schemas/pal.report.v1.json`. Key sections:

```json
{
  "schema_version": "pal.report/v1",
  "report_id": "rep_0c5830fe0c7f606f75fa",
  "summary": {
    "overall_status": "warning",
    "finding_counts": { "critical": 0, "warning": 1, "informational": 0 },
    "category_statuses": { "cpu": "warning" }
  },
  "findings": [
    {
      "finding_id": "fd_nizsh4fdsffjmruw",
      "rule_id": "high-cpu-sustained",
      "severity": "warning",
      "category": "cpu",
      "title": "Sustained high CPU utilization",
      "evidence": { "metrics": [ { "statistics": { "avg": 89.4 }, "trigger_details": [...] } ] },
      "recommendations": [{ "priority": "high", "text": "..." }]
    }
  ]
}
```

Report IDs and finding IDs are **content-hash-based** — the same input and pack versions always produce the same IDs.

---

## Rule packs

Packs live under `packs/thresholds/` and are validated against `dotnet/schemas/pal.pack.v1.json`.

| Pack | Coverage | Auto-loaded when |
|------|----------|-----------------|
| `windows-core` | CPU, memory, disk, system | Always |
| `iis-core` | App pool failures, request queue, ASP.NET | IIS counters present |
| `sql-host-core` | Buffer pool, page life expectancy, deadlocks | SQL Server counters present |

### Writing a pack

Packs are YAML files using declarative comparators — no expression DSL or custom parser:

```yaml
schema_version: "pal.pack/v1"
pack_id: my-pack
pack_name: "My Pack"
version: "1.0.0"

rules:
  - rule_id: high-cpu-sustained
    severity: warning
    category: cpu
    title: "Sustained high CPU utilization"
    condition:
      metric: processor.percent_processor_time
      instance: _Total
      aggregation: avg
      operator: gt
      threshold: 80
      duration_percent: 20
```

Validate with: `pal validate-pack --path packs/thresholds/my-pack`

---

## Project layout

```
pal-x/
├── dotnet/
│   ├── src/
│   │   ├── Pal.Engine/       # Dataset model, rule evaluator, statistics, status classifier
│   │   ├── Pal.Ingestion/    # CSV collector; BLG collector (Windows PDH interop)
│   │   ├── Pal.Packs/        # YAML pack loader, validator, resolver, signature verifier
│   │   ├── Pal.Reporting/    # JSON + HTML + Markdown writers, ScottPlot SVG charts
│   │   ├── Pal.Application/  # Shared DTOs and service interfaces
│   │   ├── Pal.Persistence/  # EF Core 8 + PostgreSQL — entities, migrations, repositories
│   │   ├── Pal.Api/          # ASP.NET Core minimal API + background workers
│   │   └── Pal.Cli/          # Spectre.Console.Cli standalone tool
│   ├── schemas/              # pal.pack.v1.json, pal.report.v1.json (source of truth)
│   └── tests/
│       ├── Pal.Engine.Tests/
│       ├── Pal.Ingestion.Tests/
│       ├── Pal.Packs.Tests/
│       ├── Pal.Reporting.Tests/
│       ├── Pal.Application.Tests/
│       ├── Pal.Cli.Tests/
│       └── Pal.Api.Tests/    # Integration tests — requires Docker Desktop
├── packs/thresholds/         # Shipped rule packs
├── fixtures/                 # Golden-output test fixtures (CSV and BLG)
├── docs/                     # Architecture ADRs and phase specs
└── legacy/                   # PAL v2 PowerShell tool (read-only reference)
```

---

## Development

```bash
# Unit tests (no Docker required) — enumerate projects rather than a solution-level
# filter; newer Microsoft.NET.Test.Sdk treats a zero-match per-DLL run as exit 1.
foreach ($p in 'Pal.Engine.Tests','Pal.Packs.Tests','Pal.Ingestion.Tests',
               'Pal.Reporting.Tests','Pal.Application.Tests','Pal.Cli.Tests') {
  dotnet test "dotnet/tests/$p" -c Release
}

# Integration tests (Docker Desktop must be running)
dotnet test dotnet/tests/Pal.Api.Tests -c Release

# Validate all shipped packs
pal validate-pack --path packs/thresholds/windows-core
pal validate-pack --path packs/thresholds/iis-core
pal validate-pack --path packs/thresholds/sql-host-core

# Add an EF Core migration (dotnet-ef is not on PATH — invoke via full path)
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" migrations add <Name> `
    --project dotnet/src/Pal.Persistence `
    --startup-project dotnet/src/Pal.Api
```

CI runs on every push to `main` and every PR — see `.github/workflows/ci.yml`. The Windows runner enumerates the unit-test projects and excludes `Pal.Api.Tests` (no Docker); a separate Ubuntu job runs them via Testcontainers.

---

## Architecture decisions

Ratified deviations from the original design docs are recorded in `docs/architecture/adr/`. Key choices:

- **Declarative comparators** ([ADR 0002](docs/architecture/adr/0002-declarative-rule-schema.md)) instead of a custom expression DSL — every rule condition is `metric + aggregation + operator + threshold + duration_percent`, trivially validatable and diffable
- **Tri-state status** (critical / warning / healthy) instead of an additive numeric score
- **Content-hash IDs** — `report_id` and `finding_id` are SHA-256-based; the same inputs always produce the same identifiers
- **snake_case canonical metric IDs** — legacy counter paths (e.g. `\Processor(_Total)\% Processor Time`) map to `processor.percent_processor_time` via a pack-level alias table
- **Pack signing** ([ADR 0003](docs/architecture/adr/0003-pack-signing-format.md)) — RSA-PSS-SHA256 signatures stored as `pack.yaml.sig` sidecar files; `pal validate-pack --require-signature` enforces them
- **Rolling-window aggregations** ([ADR 0004](docs/architecture/adr/0004-schema-v1.1-rolling-windows.md)) — `schema_version: "pal.pack/v1.1"` adds an optional `window:` block to rule conditions for time-windowed evaluation (avg, min, max, p90, p95, p99)
- **Multitenancy** — two-level Org → Workspace hierarchy enforced by EF Core global query filters and DB-level cascade constraints; API routes scope under `/api/workspaces/{workspaceId}`

See [ADR 0001](docs/architecture/adr/0001-deviations-from-seed-docs.md) for the full list of 12 ratified deviations.
