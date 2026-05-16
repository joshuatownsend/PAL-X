---
title: Datasets and inputs
description: How CSV and BLG inputs become the dataset the rule engine evaluates, and where host_context fits in.
---

# Datasets and inputs

PAL-X analyses **datasets**. A dataset is the normalised, time-aligned representation of one performance capture — a set of series, each carrying a canonical metric ID, an instance, a unit, and samples. Rules evaluate against the dataset; reports describe the dataset. The collector layer (CSV or BLG) translates from the on-disk representation into that uniform shape.

For the field contract, see **[Reference — Report schema, `dataset`](../reference/report-schema.md#dataset)**. For the canonical ID inventory, see **[Reference — Canonical metric IDs](../reference/metric-ids.md)**.

## Two input formats

### CSV (cross-platform)

The Windows `typeperf` and `relog` tools both emit CSV with this shape:

```text
"(PDH-CSV 4.0) (Pacific Daylight Time)(420)","\\MACHINE\Processor(_Total)\% Processor Time",...
"05/15/2026 10:23:14.123","42.5",...
```

The CSV collector parses this directly on any platform. It's the most portable format and works the same on Windows, Linux, and macOS.

### BLG (Windows-only)

Windows Performance Monitor's binary log format. PAL-X reads BLG natively on Windows via PDH (Performance Data Helper) interop in `BlgCollector`. On non-Windows platforms a `PlatformNotSupportedException` is thrown with a fallback message — see **[Convert BLG on Linux](../guides/convert-blg-on-linux.md)** for the workflow there.

The collector factory dispatches by extension: `.blg` → `BlgCollector`, `.csv` → `CsvCollector`. There's no auto-conversion in either direction.

## From raw counter path to canonical ID

A raw Windows counter path looks like:

```text
\\WEB-01\Processor(_Total)\% Processor Time
```

The collector decomposes this into:

| Component | Example | Where it lands |
|---|---|---|
| Machine | `WEB-01` | `dataset.machine_name` |
| Object | `Processor` | Part of the canonical ID lookup |
| Instance | `_Total` | `series.instance` (filter via `condition.instance`) |
| Counter | `% Processor Time` | Rest of the canonical ID lookup |

The `MetricAliasRegistry` then matches the path against built-in patterns and rewrites it to a snake_case ID:

```text
\\WEB-01\Processor(_Total)\% Processor Time  →  processor.percent_processor_time
```

This rewrite is why packs author rules against `processor.percent_processor_time` — once. They don't carry locale-specific or version-specific path patterns; the engine handles that. If your counter path doesn't match a built-in pattern, the engine emits `unknown.<sanitised>` and the series is ingested but doesn't match any rule. Add a `metric_aliases:` map to your pack to teach it the new mapping.

## Host context

Some rules need information the counter file doesn't carry — total physical RAM (to express "below 10% of installed memory") or logical processor count (to express "queue length > 2 per core"). PAL-X exposes two host context variables:

- `host_context.total_physical_memory_mb`
- `host_context.logical_processor_count`

You provide them in one of three ways:

1. **CLI flags:** `--host-memory-mb 32768 --host-cpu-count 16` on `pal analyze`.
2. **Sidecar file:** a `host_context.json` next to the input file:
   ```json
   { "total_physical_memory_mb": 32768, "logical_processor_count": 16 }
   ```
3. **PerfMon header on BLG:** BLG captures sometimes carry this in the binary header. When present, PAL-X reads it automatically.

If a rule references a host context variable that's unknown, PAL-X emits an informational warning (`host_context.unknown`) and skips the rule. The run still completes successfully — host-context-dependent rules are *informationally optional*, not required.

## Determinism

A dataset is fully deterministic from its inputs. Two analysis runs of the same CSV with the same pack set produce byte-identical reports if `--now` is fixed (override the `generated_at_utc` timestamp). This is what makes golden-fixture testing possible — see `fixtures/cpu-pressure/golden.pal-report.json`.

The implication: if you re-run an analysis and get different results, something changed in the input (counter ordering, sample timing, encoding) or in the pack set. The engine itself adds no entropy.

## Series, samples, and gaps

A **series** is one (counter, instance) pair: e.g., the `_Total` instance of `Processor`. A **sample** is one timestamped value on a series. A **gap** is a missing sample where the dataset's nominal sample interval said one should be present.

Gap detection is heuristic — the median sample interval is taken as the nominal cadence, and any spacing between adjacent samples more than ~1.5× that cadence counts as a gap. Gaps are surfaced in the report's `dataset.gap_count` and in `series_index[].statistics.missing_sample_count`; they don't fail the run, but they're worth investigating if you see a lot of them (capture interruption, agent crash, disk full).

## Related

- **[Reference — Report schema (`dataset` + `series_index`)](../reference/report-schema.md#dataset)** — field-level.
- **[Reference — Canonical metric IDs](../reference/metric-ids.md)** — the rewrite table.
- **[Analyze a CSV](../guides/analyze-csv.md)** — the simplest input path.
- **[Analyze a BLG on Windows](../guides/analyze-blg-windows.md)** — when the capture is binary.
- **[Convert BLG on Linux](../guides/convert-blg-on-linux.md)** — when it's binary and you're not on Windows.
- **[Interpret the HTML report](../guides/interpret-html-report.md)** — the analyst-facing rendering of a dataset.
