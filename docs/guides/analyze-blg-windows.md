---
title: Analyze a BLG on Windows
description: Run pal analyze directly against a Windows perfmon BLG — no conversion step.
---

# Analyze a BLG on Windows

Goal: analyse a Windows perfmon binary log (`.blg`) without converting to CSV first. Same command as CSV; the dispatch is by file extension.

This guide assumes you're running on Windows. If you're not, see **[Convert BLG on Linux](convert-blg-on-linux.md)** for the workaround.

## Run

```powershell
pal analyze `
  --input fixtures/cpu-pressure-blg/input.blg `
  --output out `
  --pack-dir packs/thresholds
```

`BlgCollector` opens the BLG via Windows PDH (Performance Data Helper) interop. The output paths follow the input stem: `out/input.pal-report.{json,html}`.

## Why BLG over CSV?

| | BLG | CSV |
|---|---|---|
| Size | Smaller (binary) | Larger (text) |
| Capture speed | Faster (no formatting) | Slower |
| Cross-platform | Windows only (PDH) | Anywhere |
| Hand-editable | No | Yes |
| PerfMon-native | Yes | Requires `relog` |

For long captures or production deployments, BLG is the right format. CSV is the right format for sharing across teams or debugging an unexpected sample.

## Host context from a BLG

BLG captures occasionally carry host information in their binary header. When present, PAL-X reads it automatically — no need to pass `--host-memory-mb` or `--host-cpu-count`. If the header doesn't include it, the host_context variables are unknown and host-context-dependent rules are silently skipped.

To force host context regardless of what the BLG carries:

```powershell
pal analyze `
  --input input.blg `
  --output out `
  --pack-dir packs/thresholds `
  --host-memory-mb 65536 `
  --host-cpu-count 32
```

Explicit flags win over the BLG header. This is what you want if the capture machine reports stale or wrong hardware identity (VM with adjusted RAM, hot-swapped CPU, etc.).

## When the BLG won't open

| Symptom | Cause | Fix |
|---|---|---|
| `PdhOpenLog returned 0x800007D5` (file not found) | Path issue, including UNC paths with stale credentials | Verify the path opens in PerfMon first |
| `PlatformNotSupportedException` | Running on non-Windows | Use the [Linux fallback](convert-blg-on-linux.md) |
| `PdhExpandWildCardPath returned 0xC0000BB8` (no items found) | BLG has no counters (empty capture) | Inspect with `pal inspect-dataset --input input.blg` |
| Hangs at "Opening BLG" | Large file, slow disk | The BLG collector is single-pass — wait it out; CSV would also be slow |

## Inspect first

For an unfamiliar BLG, list the series before running rules:

```powershell
pal inspect-dataset --input input.blg
```

You'll see the series count, the machine name from the header, and the canonical metric IDs each counter normalises to. Useful for diagnosing `unknown.*` mappings before they bite a rule run.

## Related

- **[CLI — `pal analyze`](../reference/cli/pal-analyze.md)** — flags identical between CSV and BLG.
- **[CLI — `pal inspect-dataset`](../reference/cli/pal-inspect-dataset.md)** — peek at the dataset without running rules.
- **[Concepts — Datasets and inputs](../concepts/datasets-and-inputs.md)** — CSV vs BLG trade-offs in depth.
- **[Convert BLG on Linux](convert-blg-on-linux.md)** — when you're not on Windows.
