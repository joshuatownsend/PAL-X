---
title: Data flow
description: End-to-end — from a counter file on disk to a finding in a report — with the types and components at each hop.
---

# Data flow

This is the runtime story: how a `.csv` or `.blg` becomes a finding in a report. Six hops, two modes (CLI synchronous vs API asynchronous), one engine.

For the per-component reference, see the **[Project map](index.md#project-map)** on the architecture index.

## The engine pipeline (same in all modes)

```text
                          ┌─────────────────────────────────────┐
                          │             RAW INPUT               │
                          │   capture.csv  or  capture.blg     │
                          └─────────────┬───────────────────────┘
                                        │
            (1) Collector dispatch by file extension
                                        │
                  ┌──────────────────────┴──────────────────────┐
                  ▼                                             ▼
        ┌──────────────────┐                       ┌─────────────────────┐
        │   CsvCollector   │                       │    BlgCollector     │
        │  (any platform)  │                       │   (Windows / PDH)   │
        └─────────┬────────┘                       └──────────┬──────────┘
                  │                                            │
                  └────────────────────┬───────────────────────┘
                                       │
                              raw counter paths
                                       │
            (2) MetricAliasRegistry normalises paths to canonical IDs
                                       │
                                       ▼
                       ┌─────────────────────────────┐
                       │           Dataset           │
                       │  series[], samples, gaps,   │
                       │       host_context          │
                       └───────────────┬─────────────┘
                                       │
            (3) PackLoader reads YAML; PackValidator gates malformed packs
                                       │
                                       ▼
                       ┌─────────────────────────────┐
                       │       Pack[] in memory      │
                       │     applicability filter    │
                       └───────────────┬─────────────┘
                                       │
            (4) RuleEngine evaluates conditions against series
                                       │
                                       ▼
                       ┌─────────────────────────────┐
                       │         Finding[]           │
                       │   evidence + statistics     │
                       │  sorted: sev/cat/rule/id    │
                       └───────────────┬─────────────┘
                                       │
            (5) Report writers serialise
                                       │
                  ┌────────────────────┼────────────────────┐
                  ▼                    ▼                    ▼
         ┌──────────────┐    ┌──────────────┐     ┌──────────────────┐
         │  JSON report │    │  HTML report │     │ Markdown report  │
         │  (canonical) │    │ (browser UX) │     │   (optional)     │
         └──────────────┘    └──────────────┘     └──────────────────┘
                                       │
            (6) ScottPlot writes SVG charts (optional)
                                       │
                                       ▼
                       ┌─────────────────────────────┐
                       │     charts/*.svg            │
                       └─────────────────────────────┘
```

## Hop 1 — Collector dispatch

`CollectorFactory.For(path)` looks at the file extension:

- `.csv` → `CsvCollector` (any platform).
- `.blg` → `BlgCollector` (Windows-only, throws `PlatformNotSupportedException` elsewhere with a `relog -f CSV` fallback message).

Both collectors emit the same `Dataset` shape — downstream code can't tell them apart.

The CSV path is text — read line by line, parse perfmon's CSV header for counter paths, parse samples by column. The BLG path is binary — open via PDH (`Pdh.dll`), enumerate counters, fetch samples through `PdhCollectQueryData`.

## Hop 2 — Canonical metric IDs

Raw counter paths look like `\\WEB-01\Processor(_Total)\% Processor Time`. Rules don't reference paths — they reference canonical IDs like `processor.percent_processor_time`. `MetricAliasRegistry.Resolve(path)` runs the path against compiled regex patterns and returns the canonical ID, or `null` if nothing matches (which becomes `unknown.<sanitised>`).

The registry's default entries are built into `Pal.Engine.Normalization.MetricAliasRegistry.BuildDefault()` — see **[Reference — Canonical metric IDs](../reference/metric-ids.md)** for the table. Pack-level `metric_aliases:` extends this registry per analysis.

## Hop 3 — Pack loading

`PackLoader.Load(yamlPath, signatureRequirement, trustedKeys)`:

1. Reads the YAML file.
2. Parses into the `Pack` model (DTOs in `Pal.Engine.Model`).
3. Optionally verifies the `pack.yaml.sig` sidecar.
4. Hands the parsed pack to `PackValidator.Validate(pack)`.

`PackValidator` is the source of truth for what constitutes a valid pack — every schema constraint (severity enum, aggregation enum, operator enum, window invariants) is enforced here, not at YAML parse time. Validation errors and warnings are returned to the caller; failures surface as exit code `4` from the CLI or `400/422` from the API.

`PackRegistrySyncService` (API only) drives the loader at startup: it walks `Packs:Directory`, loads each `pack.yaml`, and persists the result into Postgres so the API has a database-backed pack registry alongside the disk source.

## Hop 4 — Rule evaluation

The heart of the engine. `RuleEngine.Evaluate(dataset, packs)`:

```text
for each pack:
  if pack.applicability matches dataset:
    for each rule:
      if rule.applies_when matches:
        for each condition:
          select series (canonical_metric + optional instance filter)
          compute aggregation (avg, p95, ..., trend, or window-bounded)
          compare to threshold (number or host_context-resolved)
          check duration_percent
        if all conditions satisfied:
          emit Finding with evidence
sort findings: severity desc, category asc, rule_id asc, finding_id asc
```

A few important properties:

- **Determinism.** Two runs against the same dataset with the same packs produce identical findings (modulo `generated_at_utc`, overridable with `--now`). The sort order is total, with `finding_id` (a content hash) as the final tiebreaker.
- **`host_context` is informational-fallback.** If a rule references `host_context.total_physical_memory_mb` and the value is unknown, the rule is skipped and an informational warning is emitted. Run still succeeds.
- **Pack-level `applicability` is a fast skip.** If `requires_any` doesn't match the dataset's metric set, the pack's rules are never evaluated. Rule-level `applies_when` is a per-rule equivalent.

`Finding` carries everything needed to render the result: rule metadata, category, severity, the resolved evidence (series + statistics + trigger expression), and inlined recommendations from the pack's `recommendations:` map.

## Hop 5 — Report writing

Three writers, one shared shape:

- `JsonReportWriter` — emits `pal.report/v1` JSON. Canonical; downstream consumers read this.
- `HtmlReportWriter` — emits a self-contained HTML page. Derived view; renders the same data with a human-friendly layout.
- `MarkdownReportWriter` — emits GFM tables. Derived view; only invoked when explicitly requested.

All three call `JsonReportWriter.WriteInput(...)` internally to compose the report model, then serialise to their target format. This is why golden-fixture tests work — the writers are deterministic transforms of a fixed-input model.

UTF-8 without BOM is enforced via `new UTF8Encoding(false)` on every write. This is non-negotiable: golden tests are byte-comparison, and a BOM would break them.

## Hop 6 — Chart SVGs (optional)

If `--include-charts` is set (CLI) or charts are otherwise requested, the engine attaches `ChartRef` entries to findings and writes SVGs via `ScottPlot.Plot.Save`. One SVG per (finding × metric) pair, capped by `--chart-limit` (default 20).

Charts are written to `out/charts/<report-name>-<chart-id>.svg`. The HTML report embeds them inline. The JSON report references them by relative path in each finding's `evidence.charts[]`.

ScottPlot's SVG output is canonicalised by `SvgCanonicalizer` before write — IDs are normalised so two runs produce byte-identical SVGs. Without this step, ScottPlot's gradient IDs include process-local counters that would defeat determinism.

## Two runtime modes share the pipeline

### CLI — synchronous

```text
                       ┌─────────────┐
   user typed args ───►│  pal CLI    │
                       │ (synchronous)│
                       └──────┬──────┘
                              │
                              ▼
                  the 6 hops above, in process
                              │
                              ▼
                       writes to ./out/
                              │
                              ▼
                     exits with status code
```

`AnalyzeCommand.ExecuteAsync` orchestrates collectors, the engine, the writers. Failures map to `ExitCodes.*` per **[Reference — Exit codes](../reference/exit-codes.md)**.

### API — asynchronous

```text
                       ┌──────────────┐                           ┌────────────────┐
   POST /analysis ────►│   HTTP        │──► writes job row ──────►│   Postgres     │
                       │  handler      │                           └────────────────┘
                       │ enqueues Guid │
                       └──────┬───────┘
                              │
                              ▼
                       Channel<Guid> (in-process, single-reader)
                              │
                              ▼
                       ┌──────────────┐
                       │AnalysisWorker│ (BackgroundService)
                       └──────┬───────┘
                              │
                              ▼
                  the 6 hops above, same code
                              │
                              ▼
                       writes JSON/HTML to disk + result row to Postgres
                              │
                              ▼
                       (auto-compare if selectedBaselineId set)
                              │
                              ▼
                       (policy evaluation → alerts → webhook delivery)
```

The engine pipeline is identical. What's different is the orchestration: HTTP enqueues, the worker dequeues, repositories persist, and additional services (`PolicyEvaluator`, `IAutoCompareService`, `NotificationService`) extend the post-analysis flow with alerting and comparisons.

The in-process `Channel<Guid>` keeps the API simple — no external message broker, no Postgres `LISTEN/NOTIFY`. The trade-off: if the API process crashes, queued-but-not-started jobs are lost (the worker channel is in-memory). Jobs that have started but not finished are detected on restart and marked `failed`. This is documented as a Phase 5 improvement candidate.

## Related

- **[Persistence](persistence.md)** — what gets stored after the pipeline completes.
- **[Schema evolution](schema-evolution.md)** — how the input contract evolves.
- **[Reference — Report schema](../reference/report-schema.md)** — output shape.
- **[Reference — Canonical metric IDs](../reference/metric-ids.md)** — the rewrite table for Hop 2.
- **[ADR 0002 — Declarative Rule Schema](adr/0002-declarative-rule-schema.md)** — why Hop 4 doesn't have an expression parser.
