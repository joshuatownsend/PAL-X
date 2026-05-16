---
title: Work with trends
description: Pull workspace-level trend data, interpret the categories, and triage what to investigate first.
---

# Work with trends

Goal: see which rules are firing more often, less often, or for the first time in the last N analysis jobs in your workspace.

For the data shape, see **[HTTP API — Trends](../reference/http-api/trends.md)**. For the design, see **[Concepts — Analytics surfaces](../concepts/analytics-surfaces.md)**.

## Pull trends

```bash
# Default — last 10 jobs
pal remote trends

# Wider window
pal remote trends --last 20
```

JSON output (good for downstream tooling):

```bash
pal remote trends --json | jq '.items | group_by(.trend)'
```

The response has a `windowSize` field (how many jobs were used) and an `items` array — one entry per rule that fired anywhere in the window.

## Triage order

Read the result in this order:

1. **`appearing`** — rules firing in recent runs but absent earlier. **New problems** since the window started. Often the most actionable category.
2. **`worsening`** — rules firing throughout, with statistics degrading. **Existing problems getting worse**.
3. **`resolved`** — rules that *were* firing but stopped recently. Two cases: real recovery, or a counter stopped being captured. Worth a sanity check.
4. **`improving`** — same rules, statistics moving the right way. Don't dismiss — they tell you something *is less broken*, which is sometimes counter-intuitive evidence.
5. **`unchanged`** — same rules, stable statistics. Background, not signal.

## What "appearing" really means

A rule appears if it fires in the candidate runs of the window but didn't in the older portion. The cutoff isn't half-and-half — `TrendAnalyzer` weights the recent jobs more heavily. A rule firing in the last 3 runs of a 10-run window is "appearing" even if it also fired once 8 runs ago.

The implication: noisy rules (those that flicker on and off) can land in `unchanged` rather than oscillating between `appearing` and `resolved`. That's intentional — categorisation favours stability.

## Tying back to specific jobs

Each trend item links to the runs where it fired. From the CLI:

```bash
pal remote trends --json \
  | jq '.items[] | select(.trend == "appearing") | {ruleId, firstSeenAt, lastSeenAt}'
```

For the full evidence chain — which specific jobs fired which rules — query the analysis list for the workspace and filter by status:

```bash
pal remote results <jobId>
```

There's no "show me the job that produced this trend row" single call; the trend is a rollup, not a join.

## When to widen or narrow the window

| Want to... | Set `--last` to... |
|---|---|
| See what changed today | 5–10 |
| Smooth out daily oscillation in a noisy environment | 20–30 |
| Look at a full release cycle | Number of jobs across the cycle |

Larger windows take more compute (the analyzer reads N reports) but are still fast — these are summaries, not full reports.

## Diagnostics combines trends with findings

If you want PAL-X to do the "given today's findings, which trends are relevant?" join automatically, look at **[guided diagnostics](use-guided-diagnostics.md)** for a specific job. The diagnostics service feeds in trends, correlations, and current findings and emits prioritised insights with citations.

## Related

- **[Work with correlations](work-with-correlations.md)** — the sibling surface.
- **[Use guided diagnostics](use-guided-diagnostics.md)** — trends rolled into per-job insights.
- **[CLI — `pal remote trends`](../reference/cli/pal-remote-trends.md)** — flag reference.
- **[Concepts — Analytics surfaces](../concepts/analytics-surfaces.md)** — descriptive-not-causal framing.
