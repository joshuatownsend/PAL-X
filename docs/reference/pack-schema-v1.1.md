---
title: Pack schema v1.1
description: Rolling-window aggregations — the only addition layered on top of pal.pack/v1.
---

# Pack schema — `pal.pack/v1.1`

Schema v1.1 is a **strict superset** of [v1](pack-schema-v1.md): every v1 pack is also a valid v1.1 pack if you bump `schema_version`. v1.1 adds exactly one new field — `window:` on a condition — which evaluates aggregations over a rolling time window instead of the full capture.

If you don't need rolling windows, stay on v1. v1 packs continue to be supported indefinitely.

The motivation, trade-offs, and design alternatives are documented in [ADR 0004 — Schema v1.1 Rolling Windows](../architecture/adr/0004-schema-v1.1-rolling-windows.md).

## When to use a window

v1's `aggregation` + `duration_percent` pair answers questions like *"was avg CPU above 80% for at least 20% of the capture?"* That works well for long, steady captures but loses signal in two cases:

1. **Spikes hidden by a long capture.** A 12-hour log with a 30-minute CPU spike won't trip a "20% of samples" rule, because 30 minutes is only ~4% of 12 hours.
2. **Localised pressure.** You want "CPU averaged above 80% over any 5-minute window," not "averaged above 80% across the whole capture."

`window:` solves both. The aggregation is evaluated over each rolling window of the configured length, and the rule fires if **any window** satisfies the condition.

## What's new

A condition under `pal.pack/v1.1` may include an optional `window:` block:

```yaml
schema_version: "pal.pack/v1.1"
# …
rules:
  - rule_id: spike-cpu-5min
    severity: warning
    category: cpu
    title: "5-minute CPU spike"
    summary: "Avg CPU exceeded 80% over a 5-minute window."
    conditions:
      - metric: processor.percent_processor_time
        instance: "_Total"
        aggregation: avg
        operator: gt
        threshold: 80
        window:
          duration_seconds: 300        # 5-minute window
          step_seconds: 60             # slide by 1 minute
          min_samples: 4               # require at least 4 samples per window
    recommendations:
      - investigate-cpu
```

### The `window:` block

| Field | Required | Type | Notes |
|---|---|---|---|
| `duration_seconds` | yes | integer | Rolling window length in seconds. Must be `>= 30`. |
| `step_seconds` | no | integer | Window stride. Must be `>= 1` and `<= duration_seconds`. Defaults to the dataset's sample interval. |
| `min_samples` | no | integer | Skip the window if fewer than this many valid samples fall inside it. Defaults to `2`. |

A small `step_seconds` (e.g., 1 second) gives you a per-sample sliding evaluation; a `step_seconds` equal to `duration_seconds` gives non-overlapping windows.

## Constraints enforced by `pal validate-pack`

- **`window:` requires `schema_version: "pal.pack/v1.1"`.** Using `window:` on a v1 pack is a validation error: `'window' requires schema_version pal.pack/v1.1`.
- **`trend` aggregation is not allowed with `window:`.** Trend computes a slope over the entire input by design; the validator rejects the combination: `aggregation 'trend' is not supported with 'window' (trend is not windowed)`.
- **Numeric guardrails:** `duration_seconds >= 30`, `step_seconds >= 1`, `step_seconds <= duration_seconds`, `min_samples >= 1`.

## Interaction with `duration_percent`

`duration_percent` and `window:` are orthogonal — they can coexist, and each constrains the evaluation differently:

- **`duration_percent` without `window`:** the comparison must hold for that percentage of the full capture's samples (the v1 semantic).
- **`window` without `duration_percent`:** the rule fires if any window satisfies the comparison on its aggregate value.
- **Both:** within each rolling window, the comparison must hold for `duration_percent` of that window's samples. The rule fires if any window meets that bar.

The most common pattern is `window:` alone — let the window define the time horizon and the aggregation define the reduction.

## Worked example — adapting a v1 rule

The v1 base pack ships a `high-cpu-sustained` rule that triggers on full-capture CPU pressure:

```yaml
# v1 — fires on whole-capture average
conditions:
  - metric: processor.percent_processor_time
    aggregation: avg
    operator: gt
    threshold: 80
    duration_percent: 20
```

The v1.1 equivalent that catches a 5-minute spike, even in a 12-hour capture:

```yaml
# v1.1 — fires on any 5-minute window
conditions:
  - metric: processor.percent_processor_time
    aggregation: avg
    operator: gt
    threshold: 80
    window:
      duration_seconds: 300
```

You'd typically ship both in different rules — the v1 rule catches sustained pressure (capture-wide), the v1.1 rule catches localised spikes (window-bounded).

## Validation

```bash
pal validate-pack --path path/to/v1.1-pack
```

The validator distinguishes between v1 errors and v1.1-specific errors in its output. If you accidentally use `window:` on a v1 pack you'll see a clear schema-version error before any other rule logic runs.

## Related

- **[Pack schema v1](pack-schema-v1.md)** — everything else; v1.1 only adds `window:`.
- **[ADR 0004 — Schema v1.1 Rolling Windows](../architecture/adr/0004-schema-v1.1-rolling-windows.md)** — why, alternatives considered.
- **[Report schema](report-schema.md)** — the `time_window` field on a finding tells you which window fired (when applicable).
- **[`pal validate-pack`](cli/pal-validate-pack.md)** — enforces every constraint above.
