---
title: First analysis — local CLI
description: Run `pal analyze` against a bundled fixture and read the resulting findings, end to end.
---

# First analysis — local CLI

This walkthrough runs PAL-X end-to-end on a small, deterministic, bundled fixture. After it, you'll know:

- how to invoke `pal analyze`
- what the report looks like and where to find each finding
- what the exit code means

Total time: under five minutes once you have the repo built.

## Before you start

- The repo is cloned and `dotnet build dotnet/Pal.sln -c Release` has run cleanly. If not, see **[Installation](installation.md)**.
- Your shell is at the repo root.

## The sample data

The repo ships with several test fixtures under `fixtures/`. We'll use `cpu-pressure` — a synthetic capture where CPU utilization is intentionally high and PAL-X should flag it.

```bash
ls fixtures/cpu-pressure
# golden.pal-report.json   input.csv
```

`input.csv` is in PDH-CSV format — exactly what Windows Performance Monitor exports when you save a Data Collector Set as CSV. Open it in a text editor if you're curious; the first row is counter names, every subsequent row is a timestamp + values.

`golden.pal-report.json` is the expected output — the test suite compares against it byte-for-byte. We'll generate our own version next and compare.

## Run the analysis

```bash
dotnet run --project dotnet/src/Pal.Cli -c Release -- \
  analyze \
  --input fixtures/cpu-pressure/input.csv \
  --output out/first-analysis \
  --pack-dir packs/thresholds \
  --auto-resolve-packs
```

What each flag does:

| Flag | Purpose |
|---|---|
| `--input` | Path to the perfmon capture. CSV or BLG; PAL-X auto-detects from the file extension. |
| `--output` | Directory for the report artifacts. Created if it doesn't exist. |
| `--pack-dir` | Where to look for rule packs. `packs/thresholds/` ships with the repo. |
| `--auto-resolve-packs` | Load every pack whose `applicability` matches the dataset. For this CSV that means `windows-core` (always-on) and nothing else. |

When it finishes, the CLI prints a six-line summary:

```text
PAL 2026.2.0
Input:     fixtures/cpu-pressure/input.csv
Collector: CSV
Output:    out/first-analysis
Analyzing... done — packs: windows-core
Findings:  1 (0 critical, 1 warning, 0 informational)
```

The findings themselves go to the report artifacts in `--output`, not to stdout. To see them per-line on the terminal, open the HTML or read the JSON. Exit code `0` means the analysis ran. Pass `--fail-on-warning` to get exit `1` when any warning or critical finding is present — useful as a CI gate. See [exit codes](#exit-codes) below.

## Read the report

```bash
ls out/first-analysis
# input.pal-report.html   input.pal-report.json
```

Two artifacts:

- **`input.pal-report.json`** — machine-readable, conforms to `dotnet/schemas/pal.report.v1.json`. Use it from CI, scripts, or other tools.
- **`input.pal-report.html`** — self-contained HTML you open in a browser.

Open the HTML report:

```bash
# Windows
start out/first-analysis/input.pal-report.html

# macOS
open out/first-analysis/input.pal-report.html

# Linux
xdg-open out/first-analysis/input.pal-report.html
```

You'll see four sections, top to bottom:

1. **Header** — overall status (`critical`, `warning`, or `healthy`), generated-at timestamp, the report ID (a content hash — same input + same packs → same ID), and a per-category status grid.
2. **Findings list** — every finding, ranked by severity descending, then category, then rule ID. Each one has its title, severity badge, summary, the triggering metric values, and a recommended remediation.
3. **Charts** *(optional — pass `--include-charts` to enable)* — SVG charts of the metrics that fired rules, one chart per metric.
4. **Inputs** — what the analysis ran against: dataset shape, counters seen, packs loaded, rule versions.

For the `cpu-pressure` fixture you should see one finding around sustained high CPU utilization. That's PAL-X working.

## Inspect the JSON

If you want to feed the result to another tool, look at the JSON:

```bash
cat out/first-analysis/input.pal-report.json | head -40
```

Top-level shape:

```json
{
  "schema_version": "pal.report/v1",
  "report_id": "rep_...",
  "summary": {
    "overall_status": "warning",
    "finding_counts": { "critical": 0, "warning": 1, "informational": 0 },
    "category_statuses": { "cpu": "warning" }
  },
  "findings": [ /* one entry per finding */ ],
  "input": { /* dataset, packs, host context */ }
}
```

The full schema lives at `dotnet/schemas/pal.report.v1.json`.

## Try Markdown output

If you want a Markdown report instead of (or in addition to) HTML, add `--markdown`:

```bash
dotnet run --project dotnet/src/Pal.Cli -c Release -- \
  analyze \
  --input fixtures/cpu-pressure/input.csv \
  --output out/first-analysis \
  --pack-dir packs/thresholds \
  --auto-resolve-packs \
  --markdown
```

You'll get an additional `input.pal-report.md` alongside the JSON and HTML.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success — analysis ran, no warning/critical findings (or `--fail-on-warning` not set). |
| `1` | Analysis ran AND emitted at least one warning finding, with `--fail-on-warning` set. |
| `2` | Invalid option combination (e.g. `--html-only --json-only` together). |
| `3` | Input file not found or unreadable. |
| `4` | Pack validation failed. |
| `5` | Analysis engine error. |

Use exit code `1` (with `--fail-on-warning`) as a CI gate.

## What's next

- **Use a real perfmon capture** — export one from PerfMon as CSV (Data Collector Set → save as CSV) and rerun with `--input <your-file.csv>`. Add `--host-memory-mb` and `--host-cpu-count` so memory-relative and CPU-count-relative rules can fire. If you have RAM-relative rules and don't pass `--host-memory-mb`, those rules emit an informational warning and are skipped (a pack authoring guide will cover this in more depth once published).
- **Try the BLG fixture** *(Windows only)* — `dotnet run --project dotnet/src/Pal.Cli -c Release -- analyze --input fixtures/cpu-pressure-blg/input.blg --output out/blg-test --pack-dir packs/thresholds --auto-resolve-packs`. On macOS or Linux, see the BLG-conversion note in **[Installation](installation.md)**.
- **Write your own rule pack** — guide coming in the Guides section. For now, look at `packs/thresholds/windows-core/pack.yaml` and the schema at `dotnet/schemas/pal.pack.v1.json`.
- **Move to the hosted API** — when you outgrow one-shot local analysis, see **[First analysis — remote API](first-analysis-remote.md)**.
