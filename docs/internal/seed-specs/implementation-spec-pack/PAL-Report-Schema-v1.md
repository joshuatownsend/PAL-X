# PAL Report Schema v1

## Purpose

This document defines the machine-readable output contract for a PAL analysis run.

JSON is the source of truth.
HTML reports are derived renderings.

The report schema is designed to support:
- CLI review
- future API responses
- snapshot testing
- automation and ticket attachment
- later visualization layers

---

## Format

- UTF-8 JSON
- file extension: `.pal-report.json` recommended
- schema identifier: `pal.report/v1`

---

## Top-level structure

```json
{
  "schema_version": "pal.report/v1",
  "report_id": "rep_01HTY...",
  "generated_at_utc": "2026-04-22T14:00:00Z",
  "engine": {},
  "input": {},
  "dataset": {},
  "packs": [],
  "warnings": [],
  "summary": {},
  "findings": [],
  "series_index": [],
  "artifacts": {}
}
```

---

## Top-level fields

### `schema_version`
Required string.
Allowed value in Phase 1:
- `pal.report/v1`

### `report_id`
Required string.
Opaque unique identifier for the run.

### `generated_at_utc`
Required ISO-8601 UTC timestamp.

### `engine`
Required object describing the engine and runtime.

### `input`
Required object describing the original input source.

### `dataset`
Required object describing the normalized dataset.

### `packs`
Required array of resolved packs.

### `warnings`
Required array, may be empty.

### `summary`
Required object containing rollup results.

### `findings`
Required array, may be empty.

### `series_index`
Required array of series summaries, may be empty.

### `artifacts`
Required object describing produced files.

---

## Engine object

```json
"engine": {
  "name": "PAL",
  "version": "2026.1.0",
  "runtime": ".NET 8.0",
  "host_os": "Windows 11",
  "execution_mode": "cli",
  "duration_ms": 18342
}
```

Fields:
- `name` required
- `version` required
- `runtime` required
- `host_os` optional
- `execution_mode` required
- `duration_ms` required integer

Allowed `execution_mode` in Phase 1:
- `cli`

---

## Input object

```json
"input": {
  "source_type": "blg",
  "source_path": "C:\\logs\\server01.blg",
  "source_count": 1,
  "collector": "Pal.Collectors.Blg",
  "collector_version": "1.0.0"
}
```

Fields:
- `source_type` required: `blg` or `csv`
- `source_path` required string
- `source_count` required integer
- `collector` required
- `collector_version` required

---

## Dataset object

```json
"dataset": {
  "dataset_id": "ds_01HTY...",
  "machine_name": "SERVER01",
  "time_zone": "UTC-04:00",
  "start_time_utc": "2026-04-21T18:00:00Z",
  "end_time_utc": "2026-04-21T19:00:00Z",
  "sample_interval_seconds": 15,
  "series_count": 86,
  "sample_count": 20640,
  "gap_count": 2,
  "metadata": {
    "import_warnings": 1
  }
}
```

Fields:
- `dataset_id` required
- `machine_name` optional
- `time_zone` optional
- `start_time_utc` required
- `end_time_utc` required
- `sample_interval_seconds` required integer or float
- `series_count` required integer
- `sample_count` required integer
- `gap_count` required integer
- `metadata` optional object

---

## Packs array

Each pack entry:

```json
{
  "pack_id": "windows-core",
  "pack_name": "Windows Core",
  "version": "1.0.0",
  "resolution_mode": "auto"
}
```

Fields:
- `pack_id` required
- `pack_name` required
- `version` required
- `resolution_mode` required: `explicit` or `auto`

---

## Warnings array

Warnings are non-fatal issues.

Example:

```json
{
  "code": "dataset.gaps_detected",
  "message": "Two sampling gaps were detected in the input log.",
  "severity": "warning",
  "details": {
    "gap_count": 2
  }
}
```

Fields:
- `code` required
- `message` required
- `severity` required: `warning` or `informational`
- `details` optional object

Phase 1 warning domains:
- dataset
- collector
- pack
- reporting

---

## Summary object

```json
"summary": {
  "overall_health_score": 54,
  "finding_counts": {
    "critical": 1,
    "warning": 4,
    "informational": 2
  },
  "category_scores": {
    "cpu": 70,
    "memory": 100,
    "disk": 45
  },
  "top_categories": ["memory", "cpu", "disk"],
  "analysis_status": "completed"
}
```

Fields:
- `overall_health_score` required integer 0-100
- `finding_counts` required object
- `category_scores` required object
- `top_categories` optional array
- `analysis_status` required

Allowed `analysis_status` in Phase 1:
- `completed`
- `completed_with_warnings`
- `failed`

---

## Findings array

Each finding object:

```json
{
  "finding_id": "fd_01HTY...",
  "pack_id": "windows-core",
  "rule_id": "high-cpu-sustained",
  "severity": "warning",
  "category": "cpu",
  "title": "Sustained high CPU utilization",
  "summary": "CPU remained elevated for a meaningful portion of the capture.",
  "explanation": "The total processor time stayed above expected operating range for sustained periods.",
  "time_window": {
    "start_time_utc": "2026-04-21T18:12:00Z",
    "end_time_utc": "2026-04-21T18:49:00Z"
  },
  "score_impact": 10,
  "evidence": {},
  "recommendations": []
}
```

Required fields:
- `finding_id`
- `pack_id`
- `rule_id`
- `severity`
- `category`
- `title`
- `summary`
- `explanation`
- `score_impact`
- `evidence`
- `recommendations`

Optional fields:
- `time_window`

Allowed severity values:
- `critical`
- `warning`
- `informational`

Allowed category values:
- `cpu`
- `memory`
- `disk`
- `network`
- `process`
- `iis`
- `sql`
- `system`
- `collection`
- `pack-validation`

---

## Evidence object

```json
"evidence": {
  "metrics": [
    {
      "series_id": "ser_01",
      "canonical_metric": "windows.processor.% processor time[instance=_total]",
      "statistics": {
        "avg": 84.2,
        "max": 99.4,
        "p95": 97.8
      },
      "trigger_details": [
        {
          "expression": "avg(metric('windows.processor.% processor time[instance=_total]')) >= 80",
          "result": true,
          "actual_value": 84.2
        }
      ]
    }
  ],
  "charts": [
    {
      "chart_id": "cpu-total-line",
      "artifact_path": "charts/cpu-total-line.svg",
      "title": "CPU % Processor Time (_Total)"
    }
  ]
}
```

Fields:
- `metrics` required array
- `charts` optional array

### Evidence metric entry
Fields:
- `series_id` required
- `canonical_metric` required
- `statistics` required object
- `trigger_details` required array

### Trigger detail entry
Fields:
- `expression` required
- `result` required boolean
- `actual_value` optional
- `expected_value` optional
- `notes` optional

---

## Recommendations array

Each recommendation object:

```json
{
  "id": "capture-process-cpu",
  "priority": "high",
  "text": "Capture process-level CPU counters during the next reproduction.",
  "rationale": "Total CPU alone does not identify which process is responsible."
}
```

Fields:
- `id` required
- `priority` required: `high`, `medium`, `low`
- `text` required
- `rationale` optional
- `links` optional array
- `next_collection` optional array

---

## Series index

The report should include a compact summary for all ingested series.

Example:

```json
{
  "series_id": "ser_01",
  "counter_path_original": "\\\\SERVER01\\Processor(_Total)\\% Processor Time",
  "canonical_metric": "windows.processor.% processor time[instance=_total]",
  "unit": "percent",
  "statistics": {
    "count": 240,
    "min": 8.2,
    "max": 99.4,
    "avg": 84.2,
    "median": 86.1,
    "p90": 96.0,
    "p95": 97.8,
    "p99": 99.0,
    "stddev": 12.3,
    "missing_sample_count": 0
  }
}
```

Fields:
- `series_id` required
- `counter_path_original` required
- `canonical_metric` required
- `unit` optional
- `statistics` required object

Phase 1 should not embed all raw samples in `series_index`.

---

## Artifacts object

```json
"artifacts": {
  "json_report_path": "out\\server01.pal-report.json",
  "html_report_path": "out\\server01.pal-report.html",
  "chart_paths": [
    "out\\charts\\cpu-total-line.svg"
  ]
}
```

Fields:
- `json_report_path` required
- `html_report_path` optional
- `chart_paths` optional array

---

## JSON example

```json
{
  "schema_version": "pal.report/v1",
  "report_id": "rep_123",
  "generated_at_utc": "2026-04-22T14:00:00Z",
  "engine": {
    "name": "PAL",
    "version": "2026.1.0",
    "runtime": ".NET 8.0",
    "execution_mode": "cli",
    "duration_ms": 18211
  },
  "input": {
    "source_type": "csv",
    "source_path": "C:\\logs\\sample.csv",
    "source_count": 1,
    "collector": "Pal.Collectors.Csv",
    "collector_version": "1.0.0"
  },
  "dataset": {
    "dataset_id": "ds_123",
    "machine_name": "SERVER01",
    "start_time_utc": "2026-04-21T18:00:00Z",
    "end_time_utc": "2026-04-21T19:00:00Z",
    "sample_interval_seconds": 15,
    "series_count": 86,
    "sample_count": 20640,
    "gap_count": 0
  },
  "packs": [
    {
      "pack_id": "windows-core",
      "pack_name": "Windows Core",
      "version": "1.0.0",
      "resolution_mode": "explicit"
    }
  ],
  "warnings": [],
  "summary": {
    "overall_health_score": 61,
    "finding_counts": {
      "critical": 0,
      "warning": 3,
      "informational": 1
    },
    "category_scores": {
      "cpu": 70,
      "disk": 80
    },
    "analysis_status": "completed"
  },
  "findings": [],
  "series_index": [],
  "artifacts": {
    "json_report_path": "out\\sample.pal-report.json",
    "html_report_path": "out\\sample.pal-report.html"
  }
}
```

---

## Validation requirements

A report is invalid if:
- `schema_version` is unsupported
- missing required top-level fields
- finding references unknown `pack_id`
- severity or category values are invalid
- timestamps are not valid ISO-8601 where required
- `overall_health_score` outside 0-100
- duplicate `finding_id` or `series_id`

Warnings:
- no findings present
- no HTML artifact present
- no charts emitted

---

## Compatibility policy

Phase 1 consumers must treat unknown fields as additive and ignore them safely.

Breaking changes require a new schema version.
