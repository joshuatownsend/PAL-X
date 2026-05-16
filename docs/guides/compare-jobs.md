---
title: Compare jobs
description: Diff a baseline and a candidate job — manually after both complete, or automatically at submit time.
---

# Compare jobs

Goal: produce a diff between two completed analysis jobs. The runner surfaces appearing, resolved, unchanged, worsening, and improving findings — the precise model is documented in **[Concepts — Baselines and comparisons](../concepts/baselines-and-comparisons.md)**.

There are two ways to run a compare: manually after both jobs exist, or automatically on submit by naming a baseline.

## Manual compare

When both jobs already exist:

```bash
pal remote compare \
  --baseline 38d1... \
  --candidate 7e2a...
```

Output is a structured diff with one line per finding category. To see the same diff as JSON:

```bash
pal remote compare \
  --baseline 38d1... \
  --candidate 7e2a... \
  --json
```

The persisted `CompareResult` is also retrievable later by id:

```bash
# Lists all comparisons in the workspace
pal remote compare list

# Get one by id
pal remote compare get <compare-id>
```

## Auto-compare on submit

If your workflow always compares against a known baseline, name it at submit time:

```bash
pal remote submit \
  --file new-capture.csv \
  --pack windows-core \
  --baseline 38d1...
```

`--baseline` takes a job id (the same id you'd pass to `pal remote baselines set`). When the new job completes, `IAutoCompareService` runs the diff and persists a `CompareResult` linked to the candidate job. No extra round-trip.

The auto-compare result is reachable via the candidate job's id:

```bash
pal remote status 7e2a...
# Reports: completed, with autoCompareId: <compare-id>
```

## Reading the diff

The CLI output is grouped by category:

```text
Compare: 38d1...  →  7e2a...

✗ APPEARING (1)
  high-cpu-sustained (warning, cpu) — fired in candidate, absent from baseline

▲ WORSENING (2)
  sustained-disk-read-latency (warning, disk) — avg moved 32ms → 41ms (+28%)
  high-cpu-sustained (warning, cpu) — avg moved 82% → 89% (+7pp)

✓ IMPROVING (1)
  high-paging-activity (warning, memory) — avg moved 1340 → 410 pages/sec (-69%)

― UNCHANGED (5)
  ...

✓ RESOLVED (2)
  excessive-context-switches (warning, system) — present in baseline, absent in candidate
```

What each section means:

- **Appearing** — new findings in the candidate. Investigate first; these are regressions.
- **Worsening** — same finding firing in both, but the metric moved in the bad direction.
- **Improving** — same finding firing in both, but the metric moved in the good direction. Often more interesting than it looks (it tells you something is *less* broken).
- **Unchanged** — same finding, similar statistics. Skim, don't focus.
- **Resolved** — was firing, isn't anymore. Suggests recovery — or that a counter stopped being captured.

## Pack-set drift

If the baseline and candidate ran against different pack versions, the diff annotates it:

```text
NOTE: pack-set drift detected
  baseline: windows-core@1.0.0
  candidate: windows-core@1.0.1
```

This matters because "appearing" might be a new rule in the newer pack — not a regression. Always read pack drift alongside the appearing list.

## Worked example — release CI gate

A CI pipeline that fails when a deployment introduces critical regressions:

```bash
#!/bin/bash
set -e

# Submit the candidate, auto-comparing against the named release baseline
JOB_ID=$(pal remote submit \
  --file post-deploy.csv \
  --pack windows-core \
  --baseline "$BASELINE_JOB_ID" \
  --wait \
  --json | jq -r .id)

# Pull the auto-compare result
COMPARE_ID=$(pal remote status "$JOB_ID" --json | jq -r .autoCompareId)
DIFF=$(pal remote compare get "$COMPARE_ID" --json)

# Fail if any appearing finding is critical
CRITICAL=$(echo "$DIFF" | jq '[.items[] | select(.category == "appearing" and .severity == "critical")] | length')
if [ "$CRITICAL" -gt 0 ]; then
  echo "❌ deploy introduced $CRITICAL critical regressions"
  exit 1
fi
echo "✅ no new criticals"
```

The same shape works in PowerShell with `ConvertFrom-Json` instead of `jq`.

## Related

- **[Set a baseline](set-a-baseline.md)** — the prerequisite.
- **[CLI — `pal remote compare`](../reference/cli/pal-remote-compare.md)** — flag reference.
- **[HTTP API — Compare](../reference/http-api/compare.md)** — request/response shape.
- **[Concepts — Baselines and comparisons](../concepts/baselines-and-comparisons.md)** — the diff categories explained.
