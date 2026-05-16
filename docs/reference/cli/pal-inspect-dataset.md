---
title: pal inspect-dataset
description: Import and inspect a dataset without running rules. Useful for debugging counter coverage and pack applicability.
---

# `pal inspect-dataset`

Parse a CSV or BLG capture and emit a structured description of what's inside — counters present, instance set, sample count, time range, host metadata — without running any rules.

Useful when you want to understand why a pack didn't auto-resolve, or what canonical [metric IDs](../../getting-started/glossary.md#metric-id) PAL-X derived from the source counter paths.

## Synopsis

```text
pal inspect-dataset [OPTIONS]
```

## Options

| Option | Purpose |
|---|---|
| `--input <PATH>` | Path to the input dataset. Required. |
| `--format <auto\|csv\|blg>` | Input format. Defaults to `auto`. |
| `--output <PATH>` | Write the JSON inspection result to this path. If omitted, prints to stdout. |
| `--machine-name <NAME>` | Override machine name read from source metadata. |
| `--time-zone <TZ>` | Override or assign source time zone. |

## Examples

Print a quick summary of a capture:

```bash
pal inspect-dataset --input fixtures/cpu-pressure/input.csv
```

Capture the inspection result for later diffing:

```bash
pal inspect-dataset \
  --input server01.csv \
  --output out/server01-inspection.json
```

## What you get

The JSON output contains:

- `source_type` (`csv` or `blg`) and `source_path`.
- `machine_name`, `time_zone`, `host_context` (when available).
- `time_range`: first and last timestamps, total span.
- `sample_count` per counter.
- `metrics`: the canonical metric IDs PAL-X assigned, plus the raw counter paths they came from.
- `instances`: every instance value seen per metric.

This is the same metadata block embedded in the `input` section of a full analysis report, but produced an order of magnitude faster.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Inspection ran. |
| `2` | Invalid arguments. |
| `3` | Input file not found or unreadable. |
| `5` | Engine error (parse failure). |

## Related

- **[pal analyze](pal-analyze.md)** — run rules against the dataset.
- **Pack authoring guide** *(coming)* — uses `inspect-dataset` to verify counter coverage before designing rules.
