---
title: Write a pack
description: Author a minimal pack from scratch — one rule, one recommendation, one validation step.
---

# Write a pack

Goal: author a one-rule pack that fires when sustained CPU exceeds a threshold you choose, validate it, and run it against a capture.

If you want to know what a pack is and when to write one, see **[Concepts — Packs and rules](../concepts/packs-and-rules.md)**. If you want the field-level contract, see **[Reference — Pack schema v1](../reference/pack-schema-v1.md)**.

## 1. Lay out the directory

A pack is a directory containing a `pack.yaml`. Pick a kebab-case id and create the directory:

```bash
mkdir -p packs/local/my-cpu-pack
```

`packs/local/` is a convention for site-specific packs — adjust to taste.

## 2. Write `pack.yaml`

Save this as `packs/local/my-cpu-pack/pack.yaml`:

```yaml
schema_version: "pal.pack/v1"
pack_id: my-cpu-pack
pack_name: "My CPU Pack"
version: "0.1.0"
description: "Local CPU thresholds, tighter than the shipped windows-core defaults."

applicability:
  always: true

recommendations:
  identify-process:
    priority: high
    text: "Identify the responsible process via Process(*)\\% Processor Time."
    rationale: "Total CPU alone does not tell you which workload is responsible."

rules:
  - rule_id: sustained-cpu-pressure
    severity: warning
    category: cpu
    title: "Sustained CPU pressure"
    summary: "Total processor time averaged above 85% for more than 20% of the capture."
    explanation: |
      Sustained CPU above 85% means the host is operating with little headroom.
      The 20% sample threshold tolerates short spikes but flags persistent pressure.
    conditions:
      - metric: processor.percent_processor_time
        instance: "_Total"
        aggregation: avg
        operator: gt
        threshold: 85
        duration_percent: 20
    recommendations:
      - identify-process
```

Key choices to note:

| Field | Why this value |
|---|---|
| `schema_version: "pal.pack/v1"` | The base schema — switch to `v1.1` only if you need rolling windows. |
| `applicability.always: true` | Pack loads against every dataset. Use `requires_any: [...]` if you only want this pack on workload-specific captures. |
| `instance: "_Total"` | Filters the `Processor(*)` series to just the aggregate. Without this filter, the rule would also fire on individual cores. |
| `duration_percent: 20` | Tolerates short spikes — 20% of samples must exceed the threshold before the rule fires. The default is `1` (any single sample triggers). |

## 3. Validate

Before running it, validate the YAML against the schema:

```bash
pal validate-pack --path packs/local/my-cpu-pack
```

Expected output: `Pack is valid.`

A validation error tells you exactly which rule and which field — see **[Validate a pack](validate-a-pack.md)** for the full workflow.

## 4. Run it against a capture

You can run your pack alone or alongside the shipped ones. To run alongside `windows-core`:

```bash
pal analyze \
  --input fixtures/cpu-pressure/input.csv \
  --output out \
  --pack-dir packs/thresholds \
  --pack-dir packs/local/my-cpu-pack
```

`--pack-dir` is repeatable; the analyzer walks each directory looking for `pack.yaml`. Findings from your pack appear in the same report under `findings[]` with `pack_id: "my-cpu-pack"`.

If you only want your pack to run (no shipped rules), drop the first `--pack-dir`.

## 5. Iterate

Pack authoring is fast — the loop is:

1. Edit `pack.yaml`.
2. `pal validate-pack --path packs/local/my-cpu-pack` (or `--strict` for CI parity).
3. `pal analyze --input <capture> --output out --pack-dir packs/local/my-cpu-pack`.
4. Read `out/<stem>.pal-report.html` in a browser.

No build step, no compile, no daemon to restart.

## Common refinements

| Want to… | Change |
|---|---|
| Fire only on web tiers | Replace `applicability.always: true` with `requires_any: [aspnet.requests_rejected]` |
| Use a host-aware threshold | Replace the number with a `host_context` block — see **[Reference: host_context thresholds](../reference/pack-schema-v1.md#host_context-thresholds)** |
| Evaluate over a 5-minute window | Bump to `schema_version: "pal.pack/v1.1"` and add a `window:` block — see **[Rolling-window rules](rolling-window-rules.md)** |
| Trigger on multiple conditions | Add more entries to `conditions:` — all must hold for the rule to fire |
| Share with another team | Sign the pack — see **[Sign and trust packs](sign-and-trust-packs.md)** |

## Related

- **[Concepts — Packs and rules](../concepts/packs-and-rules.md)** — why packs are declarative.
- **[Reference — Pack schema v1](../reference/pack-schema-v1.md)** — field-by-field contract.
- **[Reference — Canonical metric IDs](../reference/metric-ids.md)** — the IDs `metric:` accepts.
- **[Validate a pack](validate-a-pack.md)** — next step in CI.
- **[Sign and trust packs](sign-and-trust-packs.md)** — distributing your pack to other teams.
