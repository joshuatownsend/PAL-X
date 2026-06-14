---
title: Interpret the HTML report
description: Walk through every section of a PAL-X HTML report — what to read first, what to read next, what to ignore.
---

# Interpret the HTML report

Goal: open a PAL-X HTML report and know how to read it. This guide assumes you've already run an analysis — if not, see **[Analyze a CSV](analyze-csv.md)**.

The HTML report is one self-contained file (~150KB, no external assets, no JavaScript). It's a derived rendering of the canonical JSON — for the field-level contract see **[Reference — Report schema](../reference/report-schema.md)**.

## Top of the page — the verdict

The first thing you see is the status banner:

```text
[ CRITICAL ]   3 critical · 7 warning · 12 informational
```

The label is the tri-state `summary.overall_status`. Precedence is "any critical → critical; else any warning → warning; else healthy." Counts are split by severity. **Three critical findings is not the same as 30 informational ones** — there is no aggregate score. See **[ADR 0001](../architecture/adr/0001-deviations-from-seed-docs.md)** for the rationale.

Below the banner: per-category statuses. Each category (`cpu`, `memory`, `disk`, …) gets its own tri-state pill so you can see where pressure is concentrated. A `healthy` pill on a category means *no rules fired against it* — not that no metrics were captured.

## Engine / input strip

Below the banner, a one-line strip identifies:

- **Engine version** — which `Pal.Engine` produced the report.
- **Input path** — what was analysed.
- **Generated at** — UTC timestamp.
- **Duration** — wall-clock time the analysis took.

Useful for diffing two reports — if engine versions differ, finding sets may legitimately differ too.

## Findings — sorted by severity

The findings table is the main payload. Sort order, deterministic per run:

1. `severity` desc — `critical` then `warning` then `informational`.
2. `category` asc — alphabetical within severity.
3. `rule_id` asc — alphabetical within category.
4. `finding_id` asc — content-hash tiebreaker.

The same input + same packs + same `--now` produces byte-identical HTML. If the file changes, something upstream changed.

Each row:

| Column | Meaning |
|---|---|
| Severity pill | `critical` (red), `warning` (amber), `informational` (grey). |
| Category | `cpu`, `memory`, `disk`, `network`, `process`, `iis`, `sql`, `dotnet`, `ad`, `system`, `collection`, `pack-validation`. |
| Title | One-line title from the rule. |
| Summary | Why this fired — short. |

Click a row to expand its evidence.

## Inside a finding — evidence first

The expanded finding shows three blocks:

### 1. Explanation

The rule's `explanation` field — a paragraph or two telling you what this signal means and what to investigate. This is the educational content that turns "CPU is high" into "here's why CPU being high right now matters and what to look at next."

### 2. Evidence — metrics and triggers

A table of every series that contributed to the finding:

| Column | Meaning |
|---|---|
| Metric | The canonical metric ID (e.g., `processor.percent_processor_time`). |
| Statistics | `avg / max / p95` from the matched samples. |
| Trigger expression | The condition in human-readable form: `avg(processor.percent_processor_time) > 80 for >= 20% of samples`. |

The trigger expression is the "show your work" — you can read exactly which comparison fired. If you don't understand why a finding fired, this row tells you.

For rolling-window rules (schema v1.1), each finding also carries a `time_window` — the specific window that triggered. Useful for correlating with logs or traces from that period.

### 3. Recommendations

Inlined from the pack's `recommendations:` map. Each one has a priority (`high` / `medium` / `low`), a body, an optional rationale, and zero or more links. These are the *next actions* — what to do about the finding, not just what it means.

The order is the order the rule listed them in. A `high` priority recommendation says "do this first."

## Charts (when `--include-charts` was passed)

If you ran with `--include-charts`, each finding may carry one or more chart SVGs rendered by ScottPlot. The charts are static SVGs — no JS, no zooming — and show the raw series the finding triggered on, with the triggering threshold drawn as a horizontal reference line.

Charts live under `out/charts/<report-name>-<chart-id>.svg`. The HTML embeds them inline.

## Warnings section

Below findings, a `warnings` panel surfaces non-fatal issues the analyzer hit:

- `host_context.unknown` — a rule was skipped because RAM or CPU count wasn't provided. Pass `--host-memory-mb` / `--host-cpu-count` to re-enable it.
- `metric.unmapped` — a series's counter path didn't match any alias, so it landed as `unknown.*`. Add a `metric_aliases:` map if you care.
- `dataset.gap_detected` — capture had timing gaps. Investigate the source.
- `pack-validation.warning` — a pack loaded successfully but had soft issues (used with `--strict` to escalate).

Warnings don't change the exit code — they're informational.

## Packs section

The bottom of the report lists every pack that was loaded, with `pack_id`, `version`, and `resolution_mode` (`explicit` if you passed `--pack` / `--pack-dir`, `auto` if it loaded by applicability).

If you're surprised by a finding, check this section first — the pack might not be what you expect.

## What's not in the HTML

- **Raw samples.** The HTML embeds aggregates, not the full timeseries. The JSON report's `series_index` carries summary statistics; the actual sample arrays live in the dataset artifact (only if you submitted with `--include-dataset` on `pal remote submit`).
- **Pack source.** The rule body isn't inlined. Look at the pack's `pack.yaml` (or query the pack registry) if you want to see the rule that fired.

## Diffing two reports

For two HTML reports of related runs:

- **Same input, different packs** — finding sets diverge; everything else (dataset, engine, statistics) is identical.
- **Different input, same packs** — findings, statistics, and dataset all differ; engine and pack versions identical.
- **Same input, same packs, different `--now`** — only `generated_at_utc` differs.

If you want structured diffs use the JSON output (not the HTML). The **[compare endpoint](../reference/http-api/compare.md)** does this server-side.

## Related

- **[Reference — Report schema](../reference/report-schema.md)** — the canonical JSON the HTML renders.
- **[ADR 0001 — Deviations from seed docs](../architecture/adr/0001-deviations-from-seed-docs.md)** — why tri-state (not a 0–100 score), content-hash IDs, snake_case canonical metrics.
- **[Compare jobs](../reference/http-api/compare.md)** — structured diff of two analyses.
