---
title: Work with correlations
description: Pull cross-metric correlations across recent jobs and use them to find load-bearing signal pairs.
---

# Work with correlations

Goal: find pairs of metrics that move together across recent analysis jobs — typically a hint that two signals share a root cause.

For the data shape, see **[HTTP API — Correlations](../reference/http-api/correlations.md)**. For the model, see **[Concepts — Analytics surfaces](../concepts/analytics-surfaces.md)**.

## Pull correlations

```bash
# Default — last 10 jobs
pal remote correlations

# Wider window
pal remote correlations --last 20 --json
```

The response is a `windowSize` plus an `items` array. Each item is one metric pair.

## Reading a correlation row

```json
{
  "metricA": "processor.percent_processor_time",
  "metricB": "physicaldisk.avg_disk_sec_per_read",
  "direction": "both-worsening",
  "confidence": "high",
  "samples": 10
}
```

What each field means:

- **`metricA`, `metricB`** — the two canonical metric IDs. The pair is unordered; A < B alphabetically.
- **`direction`** — `both-worsening` (both moving the bad way), `both-improving` (both moving the good way), `opposite` (one up, one down).
- **`confidence`** — derived from sample count and consistency. `high`, `medium`, `low`.
- **`samples`** — number of jobs in the window where both metrics had statistics.

## Direction matters more than magnitude

PAL-X doesn't emit a correlation coefficient like Pearson's r. The output is categorical because the goal is to surface signal pairs the operator should investigate, not to publish a statistical study. A `both-worsening` pair with `samples: 8` in a 10-job window is information you can act on; a 0.62 r-value is harder to make a decision on.

The model favours interpretability over precision. If you need the exact statistics, the underlying jobs have them — query each job's report and compute downstream.

## Triage order

Read in this order:

1. **`both-worsening`** — both signals in the pair are getting worse. Highest investigation priority.
2. **`opposite`** — one up, one down. Often interesting — e.g., available memory falling while paging rises is the same root cause expressed two ways. Sometimes a coincidence.
3. **`both-improving`** — both getting better. Confirms recovery from a previous incident, or that a recent change resolved the pressure. Useful as "is the fix working?" evidence.

## Beware spurious pairs

Two correlations PAL-X will happily emit but which usually aren't load-bearing:

- **Time-of-day pairs.** On a build server, CPU and disk both rise during the build window. They're correlated, but the "cause" is the build job, not one signal driving the other.
- **Both-restart pairs.** After a server restart, every counter starts at zero or its baseline value. A wide window across a restart can correlate many unrelated metrics.

The correlation surface is descriptive — it doesn't filter for these patterns. Operators are expected to bring domain context.

## Diagnostics uses correlations as evidence

The guided diagnostics service treats `both-worsening` correlations as one of three input signals (alongside findings and trends). When you read a per-job diagnostic insight, it may cite a specific correlation pair as part of the chain. See **[Use guided diagnostics](use-guided-diagnostics.md)**.

## Related

- **[Work with trends](work-with-trends.md)** — single-metric trajectories.
- **[Use guided diagnostics](use-guided-diagnostics.md)** — correlations folded into per-job insights.
- **[HTTP API — Correlations](../reference/http-api/correlations.md)** / **[CLI](../reference/cli/pal-remote-correlations.md)** — endpoint and flag references.
- **[Concepts — Analytics surfaces](../concepts/analytics-surfaces.md)** — descriptive-not-causal stance.
