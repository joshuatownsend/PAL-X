---
title: Pack schema v1
description: Field-by-field walkthrough of the pal.pack/v1 YAML schema — packs, rules, conditions, host_context thresholds.
---

# Pack schema — `pal.pack/v1`

A **pack** is a YAML document declaring rules and recommendations. PAL-X loads packs at analysis time and evaluates their rules against your dataset. This page is the field-level contract for the base schema; if you also want rolling-window aggregations, see [Pack schema v1.1](pack-schema-v1.1.md).

The authoritative JSON Schema is shipped in the repository at `dotnet/schemas/pal.pack.v1.json`. This page is the human-readable rendering of that schema; if the two disagree, the JSON file wins.

## Document structure

```yaml
schema_version: "pal.pack/v1"
pack_id: my-pack
pack_name: "My Pack"
version: "1.0.0"
description: "Optional human-readable description."

applicability:
  always: true        # or requires_any / requires_all

metric_aliases: {}    # optional — see below

recommendations:
  some-rec-id:
    priority: medium
    text: "What to do."
    rationale: "Why."

rules:
  - rule_id: …
    severity: …
    category: …
    title: …
    summary: …
    explanation: …
    applies_when: …
    conditions: [ … ]
    recommendations: [ … ]
```

## Top-level fields

| Field | Required | Type | Notes |
|---|---|---|---|
| `schema_version` | yes | string | Must be `"pal.pack/v1"` for the base schema. Use `"pal.pack/v1.1"` to opt into rolling windows — see [v1.1](pack-schema-v1.1.md). |
| `pack_id` | yes | string | Kebab-case identifier, regex `^[a-z][a-z0-9-]*$`. Globally unique across all packs you load together. |
| `pack_name` | yes | string | Human-readable name, used in reports and the UI. |
| `version` | yes | string | Semver — must match `^\d+\.\d+\.\d+$`. |
| `description` | no | string | One-paragraph description shown in `pal list-packs`. |
| `applicability` | no | object | Whether the pack auto-loads against a given dataset. See [Applicability](#applicability). |
| `metric_aliases` | no | map | Custom counter-path → canonical-metric mappings, layered on top of the built-in registry. See [Metric aliases](#metric-aliases). |
| `recommendations` | no | map | Named recommendation bodies, referenced by ID from rules. |
| `rules` | yes | array | At least one rule. Each rule's `rule_id` must be unique within the pack. |

## Applicability

The `applicability` block decides whether PAL-X auto-loads the pack at analysis time. Exactly one mode applies per pack.

| Field | Type | Meaning |
|---|---|---|
| `always` | boolean | If true, the pack always loads. Use for base packs (e.g., `windows-core`). |
| `requires_any` | string array | Pack loads if at least one of the listed canonical metric IDs is present in the dataset. |
| `requires_all` | string array | Pack loads only if every listed canonical metric ID is present. |

```yaml
# Base pack: always applies
applicability:
  always: true

# Conditional: only loads if IIS counters were captured
applicability:
  requires_any:
    - iis.recent_worker_process_failures
    - aspnet.requests_rejected
```

## Metric aliases

If your counters use unusual names — non-English Windows builds, vendor counters, or you're translating from a non-Windows source — you can declare custom path-to-canonical mappings at the pack level. The built-in registry (see [metric-ids](metric-ids.md)) handles standard English Windows counter paths automatically; you only need `metric_aliases` for unusual cases.

```yaml
metric_aliases:
  processor.percent_processor_time:
    - '\\*\Processeur(_Total)\% Temps processeur'   # French Windows
```

Patterns support `*` and `?` glob wildcards (escaped to regex internally).

## Recommendations

Each rule fires with one or more **recommendations** — actionable next-step advice. To avoid duplication, recommendation bodies live in a pack-level `recommendations:` map and rules reference them by ID.

| Field | Required | Type | Notes |
|---|---|---|---|
| `priority` | yes | string | One of `high`, `medium`, `low`. |
| `text` | yes | string | The recommendation itself, written as an imperative sentence. |
| `rationale` | no | string | Why this recommendation helps; appears beneath `text` in the report. |
| `links` | no | string array | URLs for further reading. |

```yaml
recommendations:
  investigate-process-cpu:
    priority: high
    text: "Identify which process is consuming CPU using Process\\% Processor Time counters."
    rationale: "Total CPU alone does not identify the responsible process."
    links:
      - "https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/typeperf"
```

A rule then references it by ID:

```yaml
rules:
  - rule_id: high-cpu-sustained
    # …
    recommendations:
      - investigate-process-cpu
```

The pack validator rejects a rule that references a recommendation ID that isn't defined in the pack.

## Rules

A rule encodes a single finding the analyzer can emit.

| Field | Required | Type | Notes |
|---|---|---|---|
| `rule_id` | yes | string | Kebab-case, regex `^[a-z][a-z0-9-]*$`. Unique within the pack. |
| `severity` | yes | string | One of `critical`, `warning`, `informational`. Drives sort order and the overall report status. |
| `category` | yes | string | One of `cpu`, `memory`, `disk`, `network`, `process`, `iis`, `sql`, `dotnet`, `ad`, `system`, `collection`, `pack-validation`. |
| `title` | yes | string | One-line title shown in the findings table. |
| `summary` | yes | string | One-sentence finding summary shown in the report. |
| `explanation` | no | string | Longer body explaining why this finding matters and what to investigate. Markdown-friendly; rendered in the HTML report. |
| `applies_when` | no | object | Per-rule applicability guard evaluated against the dataset's metric set. Differs from pack-level `applicability` in that it skips just this rule (not the whole pack). |
| `conditions` | yes | array | At least one condition. All conditions must be satisfied for the rule to fire. |
| `recommendations` | yes | array | IDs of recommendations defined in the pack-level `recommendations` map. |

### `applies_when`

```yaml
applies_when:
  requires_all:
    - memory.available_mbytes
```

Same shape as pack-level `applicability` (`requires_any` / `requires_all`), but evaluated at rule-evaluation time. If the guard fails the rule is silently skipped — no warning, no informational finding.

### Conditions

A condition is a declarative comparison: take one canonical metric series, aggregate it to a single value, compare to a threshold, optionally require the comparison to hold for a percentage of samples.

| Field | Required | Type | Notes |
|---|---|---|---|
| `metric` | yes | string | Snake-case canonical metric ID (e.g., `processor.percent_processor_time`). See [metric-ids](metric-ids.md). |
| `instance` | no | string | Counter instance filter — e.g., `"_Total"`, `"C:"`, or `"*"` for any. Omit to match all instances. |
| `aggregation` | yes | string | One of `avg`, `min`, `max`, `p90`, `p95`, `p99`, `trend`. `trend` returns the per-hour slope of a linear fit. |
| `operator` | yes | string | One of `gt`, `lt`, `ge`, `le`, `eq`. |
| `threshold` | yes | number or object | Either a literal number, or a `host_context` object (see below). |
| `duration_percent` | no | number | Percentage `0..100` of samples that must satisfy the comparison for the rule to fire. Defaults to `1` — any single sample triggers. |

### `host_context` thresholds

When you want a threshold that scales with the host's hardware — e.g., "available memory below 10% of installed RAM" — use a `host_context` object instead of a literal number.

```yaml
threshold:
  host_context: total_physical_memory_mb
  factor: 0.10
  minimum: 128
```

| Field | Required | Type | Notes |
|---|---|---|---|
| `host_context` | yes | string | One of `total_physical_memory_mb`, `logical_processor_count`. |
| `factor` | no | number | Multiplier applied to the host context value. Must be `> 0`. |
| `minimum` | no | number | Floor — the computed threshold is clamped up to this value. |
| `maximum` | no | number | Ceiling — the computed threshold is clamped down to this value. |

**If the host context value is unknown:** the rule is silently skipped and PAL-X emits an informational warning (`host_context.unknown`) in the report. Capture host context with the `--host-memory-mb` and `--host-cpu-count` flags on `pal analyze`, or by including a `host_context.json` sidecar next to your input.

## Worked example

A minimal but realistic rule:

```yaml
schema_version: "pal.pack/v1"
pack_id: minimal
pack_name: "Minimal Example"
version: "1.0.0"

applicability:
  always: true

recommendations:
  investigate-cpu:
    priority: high
    text: "Investigate which process is consuming CPU."

rules:
  - rule_id: high-cpu-sustained
    severity: warning
    category: cpu
    title: "Sustained high CPU utilization"
    summary: "Total processor time averaged above 80% for more than 20% of the capture window."
    conditions:
      - metric: processor.percent_processor_time
        instance: "_Total"
        aggregation: avg
        operator: gt
        threshold: 80
        duration_percent: 20
    recommendations:
      - investigate-cpu
```

Validate it:

```bash
pal validate-pack --path path/to/my-pack
```

## Sort order

The rule engine emits findings in this order, deterministic per run:

1. `severity` desc (`critical` → `warning` → `informational`)
2. `category` asc (alphabetical)
3. `rule_id` asc (alphabetical)
4. `finding_id` asc (content-hash, see [report schema](report-schema.md#findings))

If you're diffing two reports byte-for-byte, this ordering is guaranteed — there is no implementation-defined tiebreaker.

## Related

- **[Pack schema v1.1](pack-schema-v1.1.md)** — adds the optional `window:` block on conditions for rolling-window aggregations.
- **[Report schema](report-schema.md)** — what the analyzer emits given a pack.
- **[Metric IDs](metric-ids.md)** — the canonical metric IDs you can reference from `conditions[].metric`.
- **[ADR 0002 — Declarative Rule Schema](../architecture/adr/0002-declarative-rule-schema.md)** — why there is no expression DSL.
- **[`pal validate-pack`](cli/pal-validate-pack.md)** — validate a pack against this schema.
- **[`pal packs sign`](cli/pal-packs.md#pal-packs-sign)** — sign a pack with RSA-PSS-SHA256.
