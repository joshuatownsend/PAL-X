---
title: Use guided diagnostics
description: Pull per-job diagnostic insights that combine findings, trends, and correlations into prioritised next-steps.
---

# Use guided diagnostics

Goal: take a completed job and get a short list of "what to investigate first" that combines what fired today, what's trending, and what's correlated — every item citing its evidence.

For the contract, see **[HTTP API — Diagnostics on a job](../reference/http-api/analysis-jobs.md#get-analysisiddiagnostics)**. For the framing, see **[Concepts — Analytics surfaces](../concepts/analytics-surfaces.md)**.

## Pull diagnostics for a job

```bash
pal remote diagnostics <jobId>
```

The job must be `completed`. Output is a list of `DiagnosticInsight` items, each with a title, severity, affected rule IDs, and a source direction.

JSON:

```bash
pal remote diagnostics <jobId> --json
```

## Reading an insight

```json
{
  "id": "diag_a1b2c3",
  "title": "Sustained CPU pressure co-occurs with disk read latency",
  "category": "cpu",
  "severity": "warning",
  "affectedRuleIds": ["high-cpu-sustained", "sustained-disk-read-latency"],
  "sourceDirection": "correlation-both-worsening"
}
```

What each field tells you:

- **`title`** — the insight, in plain English.
- **`category`** — `cpu`, `memory`, `disk`, etc. — usually the category of the primary affected rule.
- **`severity`** — `critical` or `warning`. Derived from the highest-severity finding in `affectedRuleIds`.
- **`affectedRuleIds`** — the load-bearing citation. Look these up in today's report findings to see the specific evidence.
- **`sourceDirection`** — what kind of signal raised this insight:
  - `findings` — today's report contained these rule firings.
  - `worsening-trend` — these rules' statistics are degrading over the recent window.
  - `appearing-trend` — these rules started firing recently.
  - `correlation-both-worsening` — these two metric IDs are correlated and both worsening.

## Why no inference

You'll notice the insight isn't "PAL-X thinks your disk is the problem." It's "these specific rules fired and these specific signals are correlated." Every insight is **rule-based and fully cited** — there's no opaque model.

This is intentional. See **[ADR 0001](../architecture/adr/0001-deviations-from-seed-docs.md)** for the design decision. The trade-off: insights are less impressive than a black-box "ranked diagnosis," but you can always trace the evidence and disagree.

## Triage workflow

Diagnostics are emitted in severity-first order. A reasonable workflow:

1. **Start at the top** — highest severity, broadest cited evidence.
2. **For each insight, look up the affected rules** in the job's report findings. The full evidence (statistics, charts, time windows) is there.
3. **Cross-check the source direction**:
   - `findings`-only → today's snapshot.
   - `*-trend` → check **[trends](work-with-trends.md)** for the wider trajectory.
   - `correlation-*` → check **[correlations](work-with-correlations.md)** for the pair.
4. **Decide what action to take**, using the rule's recommendations from the pack as the next-step starting point.

## When diagnostics are empty

A completed job with `"items": []` from diagnostics means: no critical/warning findings, no worsening/appearing trends, no both-worsening correlations. Either:

- The system is genuinely healthy. Read the report's `summary.overall_status` to confirm `healthy`.
- The window of recent jobs is too short for trends or correlations to have anything to say. Run a few more captures.

There's no "we don't know" output — empty means "no rule-based evidence triggered an insight." Absence of evidence is not evidence of absence.

## In the UI

Each job detail page in the Blazor UI embeds the diagnostics list as a collapsible `<details>` block. Same data, same citations. You don't have to hit the CLI to read them.

## Related

- **[Work with trends](work-with-trends.md)** / **[Work with correlations](work-with-correlations.md)** — the inputs.
- **[HTTP API — Diagnostics on a job](../reference/http-api/analysis-jobs.md#get-analysisiddiagnostics)** — endpoint shape.
- **[CLI — `pal remote diagnostics`](../reference/cli/pal-remote-diagnostics.md)** — flag reference.
- **[Concepts — Analytics surfaces](../concepts/analytics-surfaces.md)** — descriptive-not-causal stance.
- **[ADR 0001 — Deviations from seed docs](../architecture/adr/0001-deviations-from-seed-docs.md)** — why citations, not inference.
