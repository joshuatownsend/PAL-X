---
title: Baselines and comparisons
description: How PAL-X models a "known-good" reference run and what a compare actually reports.
---

# Baselines and comparisons

A **baseline** is a completed analysis job designated as a reference point. A **comparison** diffs two completed jobs — typically a baseline and a candidate — and surfaces what changed.

This page covers the model. For the API contract, see **[HTTP API — Baselines](../reference/http-api/baselines.md)** and **[HTTP API — Compare](../reference/http-api/compare.md)**.

## Why "designate," not "store"

A baseline isn't a separate artifact. It's a flag (`isBaseline: true`) on an existing analysis job, plus four discriminator fields the operator sets at designation time:

| Field | Purpose |
|---|---|
| `label` | Free-form description for human eyes ("v2.5.0 reference"). |
| `type` | One of `machine`, `role`, `workload`, `release` — what kind of baseline this is. |
| `contextJson` | JSON-serialised discriminator (e.g., `{"machine":"WEB-01"}`). Normalised server-side so equivalent JSON merges. |

This design has one important consequence: a baseline carries all the metadata of a normal job (input, packs, findings, statistics). The same row serves both roles — *report* when you read it directly, *baseline* when you compare against it.

## Implicit versioning

Multiple baselines sharing the same `(type, contextJson)` are treated as versions of the same logical baseline. There is no separate "baseline version" entity — versioning is implicit on `createdAt desc`.

Concretely: designate the v2.5.0 build's job as `type: release, contextJson: {"release":"v2.5.0"}`. Three months later designate the v2.6.0 build similarly. Both rows exist; both are baselines; querying by `(release, {"release":"v2.5.0"})` returns the v2.5.0 row; the **list of versions** for the release type returns both.

This makes "compare to the previous baseline of the same kind" a one-API-call workflow — the operator never has to maintain a separate registry.

## What a compare reports

The `CompareRunner` diffs two completed jobs and emits a `CompareResult` with these categories:

| Category | Meaning |
|---|---|
| `appearing` | Finding present in candidate, absent from baseline. |
| `resolved` | Finding present in baseline, absent from candidate. |
| `unchanged` | Same finding present in both with similar statistics. |
| `worsening` | Same finding present in both, statistics moved in the wrong direction. |
| `improving` | Same finding present in both, statistics moved in the right direction. |

"Similar statistics" and "wrong direction" are defined in `CompareRunner` and tuned for each canonical metric — for `processor.percent_processor_time` a 5-percentage-point increase counts as worsening; for `physicaldisk.avg_disk_sec_per_read` a 20% increase. Direction is folded into the metric definition; there's no per-rule "higher is worse" flag.

## Manual vs auto-compare

Two ways to run a comparison:

- **Manual:** `POST /compare { baselineJobId, candidateJobId }`. Both jobs must already be completed.
- **Auto:** on submit, include `selectedBaselineId` in the analysis request. When the worker completes the new job, `IAutoCompareService` runs the diff automatically and persists a `CompareResult`. The candidate's job record links to the comparison.

Auto-compare is what you want in a "every new build is compared against the last release" CI workflow. Manual is what you want for ad-hoc investigation.

## Pack-set drift

Two jobs don't have to share the same pack set to be compared. The diff includes a note when pack versions differ — "candidate used `windows-core@1.0.1`, baseline used `windows-core@1.0.0`." This lets you distinguish *the system got worse* from *the rules got stricter*.

In practice this matters when a pack is updated mid-comparison — your old baseline was scored against v1.0.0 rules but your candidate ran with v1.0.1. The diff still runs; pack drift is annotated, not suppressed.

## Related

- **[HTTP API — Baselines](../reference/http-api/baselines.md)** / **[Compare](../reference/http-api/compare.md)** — endpoint contracts.
- **[CLI — `pal remote baselines`](../reference/cli/pal-remote-baselines.md)** / **[`pal remote compare`](../reference/cli/pal-remote-compare.md)** — CLI surface.
- **[Set a baseline](../guides/set-a-baseline.md)** / **[Compare jobs](../guides/compare-jobs.md)** — how-to walk-throughs.
