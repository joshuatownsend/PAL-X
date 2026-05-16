---
title: Report schema
description: Field-by-field walkthrough of the pal.report/v1 JSON output emitted by every analysis run.
---

# Report schema — `pal.report/v1`

Every PAL-X analysis run emits a JSON document conforming to `pal.report/v1`. The HTML and Markdown renderings are derived views of the same document — JSON is canonical. If you want to build automation, dashboards, or downstream pipelines on top of PAL-X output, build against this schema.

The authoritative JSON Schema is at `dotnet/schemas/pal.report.v1.json`. This page is the human-readable rendering; if the two disagree, the JSON file wins.

## Document shape

```json
{
  "schema_version": "pal.report/v1",
  "report_id": "…",
  "generated_at_utc": "2026-05-15T10:23:14Z",
  "engine": { … },
  "input": { … },
  "dataset": { … },
  "packs": [ … ],
  "warnings": [ … ],
  "summary": { … },
  "findings": [ … ],
  "series_index": [ … ],
  "artifacts": { … }
}
```

All top-level fields are required.

## Top-level fields

| Field | Type | Notes |
|---|---|---|
| `schema_version` | string | Always `"pal.report/v1"`. |
| `report_id` | string | Content-hash ID: `base32(SHA-256(sorted_input_digests \|\| sorted_pack_ids_with_versions)[0..10])`. Same input + same packs = same ID. |
| `generated_at_utc` | string | ISO-8601 UTC timestamp. Override with `--now` for byte-identical golden tests. |
| `engine` | object | What ran the analysis. See below. |
| `input` | object | The dataset's source identity. |
| `dataset` | object | Summary statistics about the parsed dataset. |
| `packs` | array | All packs evaluated against this dataset. |
| `warnings` | array | Non-fatal issues encountered during the run. |
| `summary` | object | The tri-state health classification and finding counts. |
| `findings` | array | Sorted findings (see [sort order](#sort-order)). |
| `series_index` | array | One entry per ingested series, with summary statistics — but no raw samples. |
| `artifacts` | object | Paths to the JSON/HTML reports and any chart SVGs. |

## `engine`

| Field | Type | Notes |
|---|---|---|
| `name` | string | `"PAL-X"`. |
| `version` | string | Assembly version of `Pal.Engine` that ran. |
| `runtime` | string | .NET runtime version. |
| `host_os` | string | Optional — host operating system identifier. |
| `execution_mode` | string | Always `"cli"` today; reserved for future modes. |
| `duration_ms` | integer | Wall-clock duration of the analysis run. |

## `input`

| Field | Type | Notes |
|---|---|---|
| `source_type` | string | `"blg"` or `"csv"`. |
| `source_path` | string | Path passed on the command line (or upload identity for API jobs). |
| `source_count` | integer | Number of source files combined into the dataset (1 today; reserved for multi-file inputs). |
| `collector` | string | Which collector ingested the input — `CsvCollector` or `BlgCollector`. |
| `collector_version` | string | Assembly version of the collector. |

## `dataset`

| Field | Required | Type | Notes |
|---|---|---|---|
| `dataset_id` | yes | string | `ds_<first 16 hex chars of SHA-256 of input digest>`. |
| `machine_name` | no | string | Extracted from counter paths if present. |
| `time_zone` | no | string | Reserved — currently informational. |
| `start_time_utc` | yes | string | Earliest sample timestamp. |
| `end_time_utc` | yes | string | Latest sample timestamp. |
| `sample_interval_seconds` | yes | number | Median sample interval (`> 0`). |
| `series_count` | yes | integer | Number of distinct (counter × instance) series. |
| `sample_count` | yes | integer | Total samples across all series. |
| `gap_count` | yes | integer | Number of detected gaps where the spacing between two adjacent samples exceeds the interval. |
| `metadata` | no | object | Free-form. Includes `host_context.*` if known (e.g., `total_physical_memory_mb`, `logical_processor_count`). |

## `packs`

One entry per pack that was evaluated against the dataset, whether or not it produced findings.

| Field | Type | Notes |
|---|---|---|
| `pack_id` | string | From the pack's `pack_id` field. |
| `pack_name` | string | From the pack's `pack_name` field. |
| `version` | string | From the pack's `version` field. |
| `resolution_mode` | string | `"explicit"` if loaded by `--pack` / `--pack-dir`, `"auto"` if matched by applicability. |

## `warnings`

Non-fatal issues. The report still emits and the exit code is still success unless something analytical fails.

| Field | Type | Notes |
|---|---|---|
| `code` | string | Stable warning identifier — e.g., `host_context.unknown`, `metric.unmapped`, `dataset.gap_detected`. |
| `message` | string | Human-readable description. |
| `severity` | string | `"warning"` or `"informational"`. |
| `details` | object | Optional structured context (which rule, which metric, etc.). |

## `summary`

The tri-state health classification — see [ADR 0001](../architecture/adr/0001-deviations-from-seed-docs.md) for the rationale against a 0–100 score.

| Field | Type | Notes |
|---|---|---|
| `overall_status` | string | `critical` if any critical finding exists; `warning` if any warning (no criticals); otherwise `healthy`. |
| `finding_counts.critical` | integer | Count of critical findings. |
| `finding_counts.warning` | integer | Count of warning findings. |
| `finding_counts.informational` | integer | Count of informational findings. |
| `category_statuses.<category>` | string | Per-category tri-state status — same precedence rules, scoped to that category's findings. |
| `analysis_status` | string | `completed`, `completed_with_warnings`, or `failed`. The first two are exit code 0; `failed` corresponds to non-zero exit. |

## `findings`

Each finding describes one rule that fired and the evidence behind it. Findings are **always sorted** — see [sort order](#sort-order).

| Field | Required | Type | Notes |
|---|---|---|---|
| `finding_id` | yes | string | Content-hash ID: `base32(SHA-256(rule_id \|\| canonical_metric_id \|\| window_start \|\| window_end)[0..10])`. Stable across reruns. |
| `pack_id` | yes | string | Pack the rule came from. |
| `rule_id` | yes | string | From the rule's `rule_id` field. |
| `severity` | yes | string | `critical`, `warning`, or `informational`. |
| `category` | yes | string | One of the category enum values — see [pack schema](pack-schema-v1.md#rules). |
| `title` | yes | string | From the rule's `title` field. |
| `summary` | yes | string | From the rule's `summary` field. |
| `explanation` | yes | string | From the rule's `explanation` field (or summary if explanation is absent). |
| `time_window` | no | object | `{start_time_utc, end_time_utc}` — for rolling-window rules (v1.1), the window that fired. Absent for v1 capture-wide rules. |
| `evidence` | yes | object | The metrics that triggered this finding. |
| `recommendations` | yes | array | Materialised recommendation bodies — resolved from the pack's `recommendations` map. |

### `evidence`

| Field | Type | Notes |
|---|---|---|
| `metrics` | array | One entry per metric that contributed to the finding. |
| `charts` | array | Optional list of chart artifacts attached to this finding. |

### `evidence.metrics[]`

| Field | Type | Notes |
|---|---|---|
| `series_id` | string | The specific (counter, instance) series — stable per dataset. |
| `canonical_metric` | string | The snake-case canonical metric ID. |
| `statistics` | object | Summary statistics for the matched samples. |
| `trigger_details` | array | One entry per condition that fired (rules with multiple conditions yield multiple entries). |

### `evidence.metrics[].trigger_details[]`

| Field | Required | Type | Notes |
|---|---|---|---|
| `expression` | yes | string | Human-readable form of the declarative condition — e.g., `"avg(processor.percent_processor_time) > 80 for >= 20% of samples"`. |
| `result` | yes | boolean | True when the condition fired. False entries appear when a multi-condition rule had some conditions satisfied and others not (informational). |
| `actual_value` | no | number | The aggregated value from the input. |
| `expected_value` | no | number | The threshold from the rule. |
| `notes` | no | string | Free-form context — e.g., the resolved host_context threshold. |

### `evidence.charts[]`

| Field | Type | Notes |
|---|---|---|
| `chart_id` | string | Stable identifier per finding/metric combination. |
| `artifact_path` | string | Path to the SVG, relative to the report directory. |
| `title` | string | Human-readable chart title. |

### `recommendations[]` (on a finding)

These are the **materialised** recommendation bodies. The rule references a recommendation by ID; the report inlines the full body so consumers don't need to load the pack to render it.

| Field | Type | Notes |
|---|---|---|
| `id` | string | The pack-level recommendation ID. |
| `priority` | string | `high`, `medium`, `low`. |
| `text` | string | The action itself. |
| `rationale` | string | Why this helps. |
| `links` | array | Further reading. |

## `series_index`

A compact catalog of every ingested series. **No raw samples** — the index is small enough to embed in the report and lets consumers reason about coverage without re-ingesting.

| Field | Type | Notes |
|---|---|---|
| `series_id` | string | Stable per dataset. |
| `counter_path_original` | string | The raw Windows counter path as captured. |
| `canonical_metric` | string | The snake-case canonical metric ID; `unknown.*` if the path didn't match any alias. |
| `unit` | string | Optional — inferred from the metric ID where possible. |
| `statistics` | object | Summary statistics for the full series. |

### `statistics` (shared shape)

The `statistics` object appears on `evidence.metrics[]` and on `series_index[]` entries with the same shape.

| Field | Required | Type | Notes |
|---|---|---|---|
| `count` | yes | integer | Number of valid samples. |
| `min` / `max` / `avg` | yes | number | Min, max, and arithmetic mean. |
| `median` | no | number | 50th percentile. |
| `p90` / `p95` / `p99` | no | number | Tail percentiles. |
| `stddev` | no | number | Population standard deviation. |
| `trend_per_hour` | no | number | Slope of linear regression in units-per-hour. |
| `missing_sample_count` | no | integer | Gaps where the dataset cadence indicated a sample should have been present. |

## `artifacts`

| Field | Required | Type | Notes |
|---|---|---|---|
| `json_report_path` | yes | string | Output path of this JSON report. |
| `html_report_path` | no | string | Output path of the HTML rendering, if generated. |
| `chart_paths` | no | array | All SVG chart artifacts referenced by findings. |

## Sort order

Findings are sorted deterministically:

1. `severity` desc — `critical` → `warning` → `informational`
2. `category` asc — alphabetical (`cpu` < `disk` < `iis` < `memory` < …)
3. `rule_id` asc — alphabetical
4. `finding_id` asc — final tiebreaker on the content hash

Two runs against the same input with the same packs produce byte-identical reports when `--now` is fixed. This is the contract that makes golden-fixture testing possible.

## UTF-8 without BOM

JSON and HTML output is written with `new UTF8Encoding(false)` — no BOM. If you're diffing reports byte-for-byte, never re-encode them through a tool that re-injects a BOM.

## Related

- **[Pack schema v1](pack-schema-v1.md)** / **[v1.1](pack-schema-v1.1.md)** — the input side of the analyzer.
- **[Metric IDs](metric-ids.md)** — the canonical IDs that appear in `canonical_metric`.
- **[`pal analyze`](cli/pal-analyze.md)** — produces the JSON, HTML, and (optionally) Markdown forms.
- **[`pal inspect-dataset`](cli/pal-inspect-dataset.md)** — inspect the series_index without running rules.
- **[ADR 0001 — Deviations from Seed Docs](../architecture/adr/0001-deviations-from-seed-docs.md)** — why tri-state status, content-hash IDs, snake_case metric IDs.
