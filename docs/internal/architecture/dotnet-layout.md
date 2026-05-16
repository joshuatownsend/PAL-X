# .NET Solution Layout

## Project Boundaries

```
dotnet/
  Pal.sln
  Directory.Build.props        ← nullable, implicit usings, deterministic build
  schemas/
    pal.pack.v1.json            ← authoritative pack schema
    pal.report.v1.json          ← authoritative report schema
  src/
    Pal.Engine/                 ← pure domain logic, no external I/O
    Pal.Ingestion/              ← collectors: CSV (full), BLG (stub)
    Pal.Packs/                  ← pack loading, validation, resolution
    Pal.Reporting/              ← JSON writer, HTML writer, chart renderer
    Pal.Cli/                    ← Spectre.Console.Cli entry point
  tests/
    Pal.Engine.Tests/
    Pal.Ingestion.Tests/
    Pal.Packs.Tests/
    Pal.Reporting.Tests/
    Pal.Cli.Tests/              ← end-to-end golden fixture tests
```

## Dependency Rules

```
Pal.Engine  ←  Pal.Ingestion
Pal.Engine  ←  Pal.Packs
Pal.Engine  ←  Pal.Reporting
Pal.Engine + Pal.Ingestion + Pal.Packs + Pal.Reporting  ←  Pal.Cli
```

`Pal.Engine` has **no external package dependencies** — only BCL. This keeps the domain model
portable and testable without loading NuGet packages.

## Key Types per Project

### Pal.Engine

| Namespace | Key Types |
|-----------|-----------|
| `Model` | `Dataset`, `TimeSeries`, `Sample`, `SeriesStatistics`, `HostContext`, `Finding`, `EvidenceMetric`, `Pack`, `Rule`, `Condition`, `ThresholdValue`, `ReportStatus` |
| `Normalization` | `MetricAliasRegistry`, `CanonicalMetricId` |
| `Statistics` | `SeriesStatisticsCalculator` |
| `Rules` | `RuleEvaluator`, `HostContextResolver`, `RuleEngine` |
| `Scoring` | `StatusClassifier` |

### Pal.Ingestion

| Type | Purpose |
|------|---------|
| `Csv.CsvCollector` | Parses PDH-CSV 4.0 format into `Dataset` |
| `Blg.BlgCollectorStub` | Throws `NotSupportedException` with `relog` command |
| `HostContext.HostContextReader` | Reads host context from CLI flags or sidecar JSON |

### Pal.Packs

| Type | Purpose |
|------|---------|
| `PackLoader` | Deserializes `pack.yaml` via YamlDotNet |
| `PackValidator` | Semantic validation: duplicate IDs, unresolved refs, enum checks |
| `PackResolver` | Search path traversal, explicit vs auto-resolution |

### Pal.Reporting

| Type | Purpose |
|------|---------|
| `Json.JsonReportWriter` | Emits `pal.report/v1` JSON (UTF-8 without BOM) |
| `Html.HtmlReportWriter` | Emits static HTML report |
| `Charts.ScottPlotRenderer` | (Phase 1.5) SVG charts via ScottPlot |

### Pal.Cli

| Type | Purpose |
|------|---------|
| `Program.cs` | Spectre.Console.Cli app registration |
| `Commands.AnalyzeCommand` | `pal analyze` — main entry point |
| `Commands.ValidatePackCommand` | `pal validate-pack` |
| `Commands.InspectDatasetCommand` | `pal inspect-dataset` |
| `Commands.ListPacksCommand` | `pal list-packs` |
| `ExitCodes` | 0/1/2/3/4/5/6 constants per CLI contract |

## Invariants

- **UTF-8 without BOM:** All JSON and HTML artifacts use `new UTF8Encoding(false)`. Never `Encoding.UTF8`.
- **Invariant culture:** All number parsing/formatting uses `CultureInfo.InvariantCulture`.
- **Finding sort:** severity desc → category asc → rule_id asc → finding_id asc. Enforced in `RuleEngine.Run()`.
- **Content-hash IDs:** `finding_id` and `report_id` are SHA-256-based; same input → same IDs across runs.
- **`host_context` unknown → skip + warning:** Rules needing unavailable context emit an informational warning and skip.
