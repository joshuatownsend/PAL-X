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
| 4 — Operations | Scheduled ingestion, alerting, webhook notifications, retention | **In progress** |

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

## Quick start — API

```bash
# Copy and fill in environment values
cp .env.example .env

# Start PostgreSQL and the API
docker compose up

# Bootstrap creates admin@pal.local with the password from PAL_BOOTSTRAP_ADMIN_PASSWORD
# Get an API token:
curl -X POST http://localhost:8080/api/tokens \
  -u admin@pal.local:<password> \
  -H "Content-Type: application/json" \
  -d '{"name": "my-token"}'
```

The API is documented at `http://localhost:8080/swagger` when running locally.

---

## CLI reference

```
pal analyze            Analyze a CSV or BLG capture and emit report artifacts
pal validate-pack      Validate a pack directory against the pal.pack/v1 schema
pal inspect-dataset    Parse and summarize a capture without running rules
pal list-packs         List packs found on the search path
pal remote             Interact with a running PAL API server
```

### `pal analyze` key options

| Option | Description |
|--------|-------------|
| `--input <path>` | Path to CSV or BLG file |
| `--output <dir>` | Output directory for report artifacts |
| `--auto-resolve-packs` | Auto-load applicable packs based on dataset counters |
| `--pack <id>` | Explicit pack ID to load (repeatable) |
| `--pack-dir <path>` | Additional search path for packs (repeatable) |
| `--host-memory-mb <n>` | Total physical memory — required for RAM-relative rules |
| `--host-cpu-count <n>` | Logical processor count — required for CPU-count-relative rules |
| `--include-charts` | Emit SVG chart files alongside the report |
| `--json-only` / `--html-only` | Emit only one format |
| `--fail-on-warning` | Exit 1 if any warning finding is produced |

> **BLG files**: BLG import is not supported in Phase 1. Convert first:
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

The JSON report conforms to `schemas/pal.report.v1.json`. Key sections:

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

Packs live under `packs/thresholds/` and are validated against `schemas/pal.pack.v1.json`.

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
│   │   ├── Pal.Ingestion/    # CSV collector; BLG stub (Phase 1.5)
│   │   ├── Pal.Packs/        # YAML pack loader, validator, resolver
│   │   ├── Pal.Reporting/    # JSON + HTML writers, ScottPlot SVG charts
│   │   ├── Pal.Application/  # Shared DTOs and service interfaces
│   │   ├── Pal.Persistence/  # EF Core 8 + PostgreSQL — entities, migrations, repositories
│   │   ├── Pal.Api/          # ASP.NET Core minimal API + background workers
│   │   └── Pal.Cli/          # Spectre.Console.Cli standalone tool
│   └── tests/
│       ├── Pal.Engine.Tests/
│       ├── Pal.Ingestion.Tests/
│       ├── Pal.Packs.Tests/
│       ├── Pal.Reporting.Tests/
│       ├── Pal.Cli.Tests/
│       └── Pal.Api.Tests/    # Integration tests — requires Docker Desktop
├── packs/thresholds/         # Shipped rule packs
├── schemas/                  # pal.report.v1.json, pal.pack.v1.json
├── fixtures/                 # Golden-output test fixtures
├── docs/                     # Architecture ADRs and phase specs
└── legacy/                   # PAL v2 PowerShell tool (read-only reference)
```

---

## Development

```bash
# Unit tests (no Docker required)
dotnet test dotnet/Pal.sln -c Release --filter "FullyQualifiedName!~Pal.Api.Tests"

# Integration tests (Docker Desktop must be running)
dotnet test dotnet/tests/Pal.Api.Tests -c Release

# Validate all shipped packs
pal validate-pack --path packs/thresholds/windows-core
pal validate-pack --path packs/thresholds/iis-core
pal validate-pack --path packs/thresholds/sql-host-core

# Add an EF Core migration
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" migrations add <Name> `
    --project dotnet/src/Pal.Persistence `
    --startup-project dotnet/src/Pal.Api
```

CI runs on every push to `main` and every PR — see `.github/workflows/ci.yml`. The Windows runner excludes `Pal.Api.Tests` (no Docker); a separate Ubuntu job runs them via Testcontainers.

---

## Architecture decisions

Ratified deviations from the original design docs are recorded in `docs/architecture/adr/`. Key choices:

- **Declarative comparators** instead of a custom expression DSL — every rule condition is `metric + aggregation + operator + threshold + duration_percent`, trivially validatable and diffable
- **Tri-state status** (critical / warning / healthy) instead of an additive numeric score
- **Content-hash IDs** — `report_id` and `finding_id` are SHA-256-based; the same inputs always produce the same identifiers
- **snake_case canonical metric IDs** — legacy counter paths (e.g. `\Processor(_Total)\% Processor Time`) map to `processor.percent_processor_time` via a pack-level alias table

See `docs/architecture/adr/0001-deviations-from-seed-docs.md` for the full list.
