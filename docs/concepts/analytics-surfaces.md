---
title: Analytics surfaces
description: Trends, correlations, and guided diagnostics — three views that turn many jobs into actionable signal.
---

# Analytics surfaces

PAL-X's analytics layer turns a *workspace* worth of analysis jobs into rolled-up insight. Three surfaces are exposed:

- **Trends** — per-rule trajectories across the last N jobs.
- **Correlations** — metric pairs that move together across the same window.
- **Guided diagnostics** — rule-based insights citing trends, correlations, and findings.

Each surface has a corresponding HTTP endpoint and CLI command. This page covers what they share and what makes them distinct.

## Why three surfaces

A single analysis run answers "what fired against this capture." Multi-run questions need state that no single report carries:

- *"Is the situation getting better or worse?"* — needs to compare today against the last several days. → **Trends**.
- *"Does CPU pressure always coincide with disk latency on this host?"* — needs to look at signal pairs across runs. → **Correlations**.
- *"Given what I'm seeing today, where should I start investigating?"* — needs to combine findings, trends, and correlations into prioritised next-steps. → **Guided diagnostics**.

All three are workspace-scoped (they roll up jobs within one workspace) and all three are read-only — they compute over completed jobs and emit summary tables. No model training, no ML, no calibration.

## Descriptive, not causal

A correlation row says "these two metrics co-vary in the recent window." It does **not** say one caused the other. A trend row says "this rule has fired in 8 of the last 10 runs." It does **not** say the system is doomed.

The framing throughout is descriptive: surface signal, name what's moving, let the operator decide what's load-bearing. There's no inference layer in front of you saying "the database is the problem" — when guided diagnostics does say that, it cites which rules, trends, and correlations led to the conclusion. The citation chain is the load-bearing part; nothing comes out of a black box.

## Trends

`TrendAnalyzer` rolls up the last N completed jobs in a workspace and categorises every rule that has fired anywhere in that window into:

- `appearing` — found in recent jobs, absent earlier.
- `worsening` — present throughout, statistics degrading.
- `improving` — present throughout, statistics getting better.
- `resolved` — present earlier, absent recently.
- `unchanged` — stable.

Categorisation is purely statistical: slope of severity counts and per-rule aggregated metric values across the window. There's no human-tuned weight or threshold beyond what's in the analyzer source — see `Pal.Application/Trends/TrendAnalyzer.cs`.

The window size defaults to `last=10`; you can override per-request. Smaller windows give noisier trends but reflect recent state faster; larger windows are more stable but slower to react.

## Correlations

`CorrelationAnalyzer` looks at metric pairs across the same N-job window and finds pairs whose summary statistics co-vary. Output rows annotate the pair with:

- `direction` — `both-worsening`, `both-improving`, or `opposite` (one up, one down).
- `confidence` — derived from sample count; more jobs → higher confidence.

Why summary statistics and not raw samples: a job's report carries `series_index[].statistics` (avg, p95, etc.) but does not embed raw timeseries unless you submit with `includeDataset: true`. The correlation surface uses what's persistent in the report — it works without ever asking for the gzipped dataset.

The "descriptive not causal" warning matters most here. A correlation between CPU and disk latency on a SQL host probably is causal; on a build server they might co-vary because they're both proxies for "test job is running." Operators interpret correlations; PAL-X surfaces them.

## Guided diagnostics

`DiagnosticsService` takes one completed job and produces a list of `DiagnosticInsightDto` items. Each item has:

- A title and category.
- A severity (`critical` or `warning`).
- A list of `affectedRuleIds` — the specific rules whose firing led to this insight.
- An optional `sourceDirection` — `worsening-trend`, `appearing-trend`, `correlation-both-worsening`, or `findings` — naming what kind of evidence produced it.

The sourcing is rule-based and exhaustively cited. There is no black-box inference, no hidden model. If you see an insight saying "investigate disk pressure," it carries the rule IDs that fired and (when applicable) the trend or correlation row that elevated them.

The implementation is in `Pal.Application/Diagnostics/DiagnosticsService.cs` and the design is informed by **[ADR 0001](../architecture/adr/0001-deviations-from-seed-docs.md)** — no health score, no opaque inference.

## What's NOT here

- **Real-time streaming.** All three surfaces operate on completed jobs only.
- **Cross-workspace.** Each surface is workspace-scoped.
- **Forecasting.** Trends are descriptive, not predictive. PAL-X does not say "you will run out of memory next week."
- **Anomaly detection on a single job.** Anomalies on one capture are findings (the rule engine's job). Anomalies across captures are trends. There is no third axis.

## Related

- **[HTTP API — Trends](../reference/http-api/trends.md)** / **[Correlations](../reference/http-api/correlations.md)** / **[Diagnostics on a job](../reference/http-api/analysis-jobs.md#get-analysisiddiagnostics)** — endpoint contracts.
- **[Work with trends](../guides/work-with-trends.md)** / **[Work with correlations](../guides/work-with-correlations.md)** / **[Use guided diagnostics](../guides/use-guided-diagnostics.md)** — guides.
- **[ADR 0001 — Deviations from seed docs](../architecture/adr/0001-deviations-from-seed-docs.md)** — why no health score, why citations matter.
