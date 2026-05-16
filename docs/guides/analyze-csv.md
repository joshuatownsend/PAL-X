---
title: Analyze a CSV
description: Run pal analyze against a Windows perfmon CSV — end-to-end with output, charts, and an exit-code-friendly invocation.
---

# Analyze a CSV

Goal: take a perfmon CSV export, run it through PAL-X, and read the findings in HTML.

For the CLI flag reference, see **[CLI — `pal analyze`](../reference/cli/pal-analyze.md)**. For how a CSV becomes a dataset under the hood, see **[Concepts — Datasets and inputs](../concepts/datasets-and-inputs.md)**.

## 1. Get a CSV

If you have a perfmon Data Collector Set running, stop it and export to CSV. From PowerShell on a captured `.blg`:

```powershell
relog -f CSV input.blg -o input.csv
```

Or use one of the bundled fixtures:

```bash
ls fixtures/cpu-pressure/
# golden.pal-report.json  input.csv
```

## 2. Run the analyzer

```bash
pal analyze \
  --input fixtures/cpu-pressure/input.csv \
  --output out \
  --pack-dir packs/thresholds
```

What lands in `out/`:

```text
out/
├── input.pal-report.json       # canonical JSON
├── input.pal-report.html       # browser-friendly rendering
└── charts/                     # SVG charts for triggered findings (if --include-charts)
```

The output stems from the input filename (`input.csv` → `input.pal-report.*`).

## 3. Read the report

Open `out/input.pal-report.html` in any browser. The page is self-contained — no JavaScript framework, no external assets, ~150KB.

The HTML walks you through:

- **Status banner** at the top: tri-state (`critical` / `warning` / `healthy`).
- **Per-category status** strip — which categories (cpu, memory, disk, …) are clean and which fired.
- **Findings table** sorted severity desc, then category, then rule.
- **Each finding** expands to show its evidence: the metric series, statistics, and the human-readable expression of the condition that fired.

See **[Interpret the HTML report](interpret-html-report.md)** for a deeper walkthrough.

## Useful flags

| Flag | Effect |
|---|---|
| `--pack <id>` | Load just this pack (skip auto-resolution). Repeatable. |
| `--pack-dir <path>` | Load every pack under this directory. Repeatable. |
| `--markdown` | Also emit a GFM Markdown report. |
| `--include-charts` | Emit SVG chart artifacts per triggered finding (`out/charts/*.svg`). |
| `--chart-limit 50` | Cap chart count. Default is 20. |
| `--host-memory-mb 32768` | Provide total RAM in MB — enables RAM-relative thresholds. |
| `--host-cpu-count 16` | Provide logical processor count — enables CPU-count-relative thresholds. |
| `--strict` | Treat pack-validation warnings as errors (good for CI). |
| `--now <iso>` | Override `generated_at_utc` for byte-identical golden output. |

## Gate a CI pipeline on the report

The CLI exit codes don't reflect finding severity — exit `0` means *the run succeeded*, not *the system is healthy*. To gate CI on findings, read the JSON:

```bash
pal analyze --input cpu.csv --output out --pack-dir packs/thresholds

status=$(jq -r '.summary.overall_status' out/cpu.pal-report.json)
case "$status" in
  critical) echo "❌ critical"; exit 2 ;;
  warning)  echo "⚠️  warning";  exit 1 ;;
  healthy)  echo "✅ healthy";  exit 0 ;;
esac
```

`overall_status` is the rolled-up tri-state; the same JSON has per-category statuses if you want finer-grained gating.

## Common pitfalls

| Symptom | Cause | Fix |
|---|---|---|
| `unknown.*` series in the report | Counter path didn't match a built-in alias (non-English Windows, vendor counter) | Add `metric_aliases:` to your pack |
| Memory rule didn't fire when it should | RAM-relative threshold but no host context | Pass `--host-memory-mb` or include `host_context.json` next to the input |
| `Pack validation failed` (exit 4) | Pack schema violation | Run `pal validate-pack --path <dir>` for the specific error |
| Many "gap detected" warnings | Capture had interruptions or was multi-machine | Look at `dataset.gap_count` and `series_index[].missing_sample_count` |

## Related

- **[CLI — `pal analyze`](../reference/cli/pal-analyze.md)** — every flag.
- **[Concepts — Datasets and inputs](../concepts/datasets-and-inputs.md)** — how CSV becomes a dataset.
- **[Interpret the HTML report](interpret-html-report.md)** — next step.
- **[Analyze a BLG on Windows](analyze-blg-windows.md)** — same workflow with the binary format.
