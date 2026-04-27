# ADR 0004 — Schema v1.1: Rolling-Window Aggregations (In-Place Enum Bump)

**Status:** Accepted  
**Date:** 2026-04-27  
**Deciders:** Josh Townsend (project lead)

## Context

Phase 1.5 adds rolling-window aggregations: the ability for a rule condition to evaluate a statistic
(avg, p95, etc.) over a sliding window rather than the full time series. This enables detection of
transient spikes that full-series aggregation would mask.

The change requires a schema update. Two options were considered:

1. **New schema file:** `dotnet/schemas/pal.pack.v1.1.json` with a new `$id`.
2. **In-place enum bump:** Edit `dotnet/schemas/pal.pack.v1.json` to accept both `"pal.pack/v1"` and `"pal.pack/v1.1"` as valid `schema_version` values.

## Decision: Option 2 — In-place enum bump

**Rationale:**

- The `window` field is **purely additive** — it is optional on `Condition`, and v1 packs
  (without `window`) validate unchanged against the updated schema. There is no breaking change.
- A new schema file would require updating all tooling that resolves `$id` references,
  all pack-loading code that validates against a schema, and all documentation pointers.
- The validator (`PackValidator`) enforces the version-gate: a condition with `window` present
  must have `schema_version: "pal.pack/v1.1"`. This preserves the invariant that v1 packs
  are never exposed to v1.1-only fields.
- `$schema` and `$id` continue to reference `pal.pack/v1`; the `schema_version` enum is
  the operational discriminator, not the JSON Schema `$id`.

## Schema changes to `dotnet/schemas/pal.pack.v1.json`

1. `schema_version`: changed from `const: "pal.pack/v1"` to `enum: ["pal.pack/v1", "pal.pack/v1.1"]`.
2. `Condition.window` added as an optional sub-object with fields:
   - `duration_seconds` (required integer ≥ 30)
   - `step_seconds` (optional integer ≥ 1)
   - `min_samples` (optional integer ≥ 1, default 2)

## Validator enforcement rules

When `window` is present on a Condition:
- `pack.schema_version` MUST be `"pal.pack/v1.1"` (error if not)
- `aggregation` MUST NOT be `"trend"` (windowed trend is undefined; error if set)
- `step_seconds` MUST be ≤ `duration_seconds` (error if violated)

## Consequences

- All existing v1 packs remain valid with no changes required.
- Authors wanting rolling-window rules bump `schema_version: "pal.pack/v1.1"` and add a `window:` block to conditions.
- `RuleEvaluator` dispatches to `RollingWindowAggregator` when `condition.Window != null`.
- The "worst window" value (max for gt/ge operators, min for lt/le) is reported as `ActualValue`.
- The expression string format for windowed conditions: `"p95(metric) over 5m rolling window > 90"`.

## Not addressed here

- Wildcard metric aliases (referenced in Phase 1 spec §225)
- Schema v1.2+ changes
