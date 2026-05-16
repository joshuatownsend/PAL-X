---
title: pal analyze
description: Analyze one input dataset and generate report artifacts.
---

# `pal analyze`

Analyze one perfmon capture (CSV or BLG) and generate JSON, HTML, and optionally Markdown report artifacts.

## Synopsis

```text
pal analyze [OPTIONS]
```

## Options

| Option | Purpose |
|---|---|
| `--input <PATH>` | Path to the input dataset (CSV or BLG). Required. |
| `--output <DIR>` | Directory for report artifacts. Created if it doesn't exist. |
| `--format <auto\|csv\|blg>` | Input format. Defaults to `auto` (file-extension sniff). |
| `--pack <PACK-ID>` | Pack ID to load. Repeatable. |
| `--pack-dir <PATH>` | Additional search path for packs. Repeatable. |
| `--auto-resolve-packs` | Auto-load applicable packs based on dataset content (counters present). Mutually compatible with explicit `--pack`. |
| `--html` | Emit HTML report. Default: on. |
| `--json` | Emit JSON report. Default: on. |
| `--markdown` | Also emit a Markdown report. Default: off. |
| `--html-only` | Emit only HTML. Mutually exclusive with `--json-only`. |
| `--json-only` | Emit only JSON. Mutually exclusive with `--html-only`. |
| `--include-charts` | Emit one SVG chart per metric that fired a rule. |
| `--chart-limit <N>` | Cap on chart count. Default: `20`. |
| `--fail-on-warning` | Exit `1` if any warning finding is produced. Useful as a CI gate. |
| `--host-memory-mb <MB>` | Total physical memory in MB. Required for any rule that uses RAM-relative thresholds. |
| `--host-cpu-count <N>` | Logical processor count. Required for any rule that uses CPU-count-relative thresholds. |
| `--machine-name <NAME>` | Override machine name read from source metadata. |
| `--time-zone <TZ>` | Override or assign source time zone. |
| `--report-name <NAME>` | Base name for generated artifact files. Default: input filename stem. |
| `--now <ISO-8601>` | Override `generated_at_utc` for byte-deterministic test output. |
| `--verbose` | Verbose output. |

## Examples

Analyze the bundled CPU-pressure fixture:

```bash
pal analyze \
  --input fixtures/cpu-pressure/input.csv \
  --output out/cpu \
  --pack-dir packs/thresholds \
  --auto-resolve-packs
```

Analyze a real capture with host context:

```bash
pal analyze \
  --input server01.csv \
  --output out/server01 \
  --pack-dir packs/thresholds \
  --auto-resolve-packs \
  --host-memory-mb 16384 \
  --host-cpu-count 8
```

Generate only JSON for piping into another tool:

```bash
pal analyze \
  --input capture.csv \
  --output out/ \
  --pack-dir packs/thresholds \
  --auto-resolve-packs \
  --json-only
```

Generate a byte-deterministic report for golden-test comparison:

```bash
pal analyze \
  --input fixtures/cpu-pressure/input.csv \
  --output out/golden \
  --pack-dir packs/thresholds \
  --auto-resolve-packs \
  --now 2026-01-01T00:00:00Z
```

## Output artifacts

For an input file `<stem>.csv` and output dir `<out>`:

| File | When |
|---|---|
| `<out>/<stem>.pal-report.json` | Always, unless `--html-only` is set. |
| `<out>/<stem>.pal-report.html` | Always, unless `--json-only` is set. |
| `<out>/<stem>.pal-report.md` | Only with `--markdown`. |
| `<out>/charts/<stem>-<chart-id>.svg` | Only with `--include-charts`. |

Override the `<stem>` portion with `--report-name`.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Analysis ran. (No findings produced an error.) |
| `1` | `--fail-on-warning` set and at least one warning or critical finding fired. |
| `2` | Invalid argument combination (e.g. `--html-only` plus `--json-only`). |
| `3` | Input file not found or unreadable. |
| `4` | Pack validation failed during load. |
| `5` | Analysis engine error. |

## Notes

- **BLG on non-Windows:** native BLG ingestion is Windows-only (PDH interop). On macOS or Linux, convert first on a Windows machine: `relog -f CSV server.blg -o server.csv`.
- **Host context missing:** RAM-relative or CPU-count-relative rules require `--host-memory-mb` / `--host-cpu-count`. If unset, those rules emit an informational finding and are skipped — the run still succeeds.
- **Auto-resolve vs explicit `--pack`:** `--auto-resolve-packs` always loads `windows-core` and additionally loads `iis-core` / `sql-host-core` if those counters are present. Mix with `--pack <id>` to load extra packs explicitly.

## Related

- **[pal list-packs](pal-list-packs.md)** — see what packs the analyzer will pick up.
- **[pal validate-pack](pal-validate-pack.md)** — check a pack before relying on it.
- **[First analysis — local CLI](../../getting-started/first-analysis-local.md)** — walkthrough.
