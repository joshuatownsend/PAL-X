---
title: Convert BLG on Linux
description: Use Windows relog to convert BLG to CSV when your analysis host isn't Windows.
---

# Convert BLG on Linux

Goal: analyse a Windows perfmon `.blg` from a Linux or macOS host. PAL-X's BLG collector uses Windows PDH and won't run on non-Windows platforms; the workaround is to convert to CSV on a Windows machine first.

## Why CSV instead of native BLG?

`BlgCollector` calls into `Pdh.dll` — a Windows-only library. There is no portable BLG reader. Vendoring one would multiply binary size and add a non-trivial maintenance surface for an edge case (most production captures are converted to CSV anyway).

The workaround: convert with the Windows-bundled `relog.exe`, then analyse the resulting CSV on any platform.

## On a Windows machine

```powershell
# Convert one BLG to CSV
relog -f CSV input.blg -o input.csv

# Or convert every BLG in a directory
foreach ($f in Get-ChildItem *.blg) {
  relog -f CSV $f.FullName -o ($f.BaseName + ".csv")
}
```

`relog` is part of every Windows installation since XP; no install needed. Output is the same CSV format `typeperf` and PAL-X's `CsvCollector` consume.

Copy `input.csv` to your Linux host (scp, share, artifact) and run `pal analyze` against it as normal.

## On Linux — what you'll see if you don't convert first

```bash
$ pal analyze --input input.blg --output out
PlatformNotSupportedException: BLG ingestion requires Windows.
Convert to CSV on a Windows machine first:

    relog -f CSV input.blg -o input.csv

Then re-run pal analyze against the CSV.
```

The error message is intentionally actionable — it tells you the command to run on the right machine.

## Doing the conversion in CI

For a hybrid pipeline where the BLG is captured on Windows and analysis runs on Linux, the conversion step belongs in the Windows job:

```yaml
# Windows runner: capture + convert
- name: Convert BLG to CSV
  shell: pwsh
  run: relog -f CSV capture.blg -o capture.csv

- name: Upload CSV
  uses: actions/upload-artifact@v4
  with:
    name: perf-capture
    path: capture.csv

# Linux runner: analyse
- name: Download CSV
  uses: actions/download-artifact@v4
  with:
    name: perf-capture

- name: Analyse
  run: |
    pal analyze --input capture.csv --output out --pack-dir packs/thresholds
```

This is the canonical pattern: Windows for capture (typeperf / Data Collector Set / Perf Counter Monitor), Linux for analysis.

## Loss of fidelity

The CSV format preserves every value the BLG carried — there's no lossy compression and no rounding. The only thing that changes is encoding (binary → text) and file size. Analysis output against `input.csv` and `input.blg` (when both are run on Windows) is identical modulo `dataset.source_type` and `input.collector` fields.

## Host context

`relog -f CSV` does **not** propagate the BLG header's host context into the CSV output — CSV has no equivalent header. If your rules rely on `host_context.total_physical_memory_mb` or `host_context.logical_processor_count`, pass them on the analyse command:

```bash
pal analyze \
  --input capture.csv \
  --output out \
  --pack-dir packs/thresholds \
  --host-memory-mb 32768 \
  --host-cpu-count 16
```

Or, write a sidecar `host_context.json` next to the CSV before uploading.

## Related

- **[Analyze a BLG on Windows](analyze-blg-windows.md)** — the same workflow without conversion.
- **[Analyze a CSV](analyze-csv.md)** — the post-conversion step.
- **[Concepts — Datasets and inputs](../concepts/datasets-and-inputs.md)** — why BLG is Windows-only.
