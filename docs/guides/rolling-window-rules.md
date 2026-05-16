---
title: Rolling-window rules
description: "Use schema v1.1's window: block to catch localised pressure that capture-wide aggregations miss."
---

# Rolling-window rules

Goal: write a rule that fires when a 5-minute window of CPU averages above 80% — even inside a 12-hour capture where the global average is benign.

For the schema change in full, see **[Reference — Pack schema v1.1](../reference/pack-schema-v1.1.md)** and **[ADR 0004 — Schema v1.1 Rolling Windows](../architecture/adr/0004-schema-v1.1-rolling-windows.md)**.

## When v1 isn't enough

The v1 rule shape evaluates an aggregation over the **full capture**:

```yaml
# v1 — fires only if avg CPU over the whole capture > 80% for 20% of samples
conditions:
  - metric: processor.percent_processor_time
    aggregation: avg
    operator: gt
    threshold: 80
    duration_percent: 20
```

That works for long, steady captures but loses signal in two cases:

1. **Spikes hidden by long captures.** A 12-hour log with a 30-minute CPU spike won't trip "20% of samples" — 30 minutes is only ~4% of 12 hours.
2. **Localised pressure.** You want "any 5-minute window where CPU averaged above 80%," not "averaged above 80% across the whole capture."

`schema_version: "pal.pack/v1.1"` adds an optional `window:` block on a condition. The aggregation is evaluated over each rolling window of the configured length, and the rule fires if **any window** satisfies the condition.

## Author a v1.1 rule

Save as `packs/local/cpu-windowed/pack.yaml`:

```yaml
schema_version: "pal.pack/v1.1"
pack_id: cpu-windowed
pack_name: "Windowed CPU"
version: "0.1.0"

applicability:
  always: true

recommendations:
  investigate-spike:
    priority: high
    text: "Identify what ran during the spike window using ETW or Process counters."
    rationale: "Short spikes are usually caused by a specific job; correlate with timestamps."

rules:
  - rule_id: cpu-spike-5min
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
          duration_seconds: 300      # 5 minutes
          step_seconds: 60           # slide every minute
          min_samples: 4             # ignore windows with fewer than 4 samples
    recommendations:
      - investigate-spike
```

What the `window:` fields do:

| Field | Effect |
|---|---|
| `duration_seconds: 300` | Length of each rolling window — 5 minutes. Must be `>= 30`. |
| `step_seconds: 60` | Window stride — a new window starts every minute. Smaller stride = more (overlapping) windows evaluated. Must be `>= 1` and `<= duration_seconds`. Defaults to the dataset's sample interval. |
| `min_samples: 4` | Skip windows containing fewer than this many valid samples (gap protection). Defaults to `2`. |

## Validate and run

```bash
pal validate-pack --path packs/local/cpu-windowed
pal analyze \
  --input fixtures/cpu-pressure/input.csv \
  --output out \
  --pack-dir packs/local/cpu-windowed
```

When the rule fires, the resulting finding includes a `time_window` field pointing to the specific window that triggered:

```json
{
  "rule_id": "cpu-spike-5min",
  "time_window": {
    "start_time_utc": "2026-05-15T10:24:30Z",
    "end_time_utc": "2026-05-15T10:29:30Z"
  },
  "evidence": {
    "metrics": [
      { "trigger_details": [{ "expression": "avg(processor.percent_processor_time) over 300s window > 80", "actual_value": 87.3 }] }
    ]
  }
}
```

The HTML report's chart for this finding renders the spike window highlighted; this is the same data the trends and diagnostics surfaces consume downstream.

## Constraints the validator will catch

- **`window:` on a v1 pack** → `'window' requires schema_version pal.pack/v1.1`. Bump the version.
- **`aggregation: trend` with a window** → `aggregation 'trend' is not supported with 'window' (trend is not windowed)`. Trend is a slope over the full series by design — windowing doesn't apply.
- **`duration_seconds: 10`** → `window.duration_seconds must be >= 30`. Sub-30s windows are too noisy at typical sample intervals.
- **`step_seconds > duration_seconds`** → `window.step_seconds must be <= duration_seconds`. Otherwise windows would have gaps between them, which isn't the semantic.

## When to use v1 vs v1.1 rules

| Use v1 (capture-wide) when... | Use v1.1 (windowed) when... |
|---|---|
| Captures are short (≤ 1 hour) | Captures are long (multi-hour or day-scale) |
| Pressure is expected to be sustained | Pressure of interest is bursty or localised |
| You want a single "is this capture healthy overall" answer | You want "did anything bad happen at any point" |

Many production packs ship **both** forms of a rule — `high-cpu-sustained` (v1, capture-wide) and `cpu-spike-5min` (v1.1, windowed) — each catching a different failure mode.

## Related

- **[Reference — Pack schema v1.1](../reference/pack-schema-v1.1.md)** — full field treatment.
- **[ADR 0004 — Schema v1.1 Rolling Windows](../architecture/adr/0004-schema-v1.1-rolling-windows.md)** — rationale and alternatives considered.
- **[Validate a pack](validate-a-pack.md)** — every v1.1 constraint is checked at validation time.
- **[Write a pack](write-a-pack.md)** — the v1 starting point this guide extends.
