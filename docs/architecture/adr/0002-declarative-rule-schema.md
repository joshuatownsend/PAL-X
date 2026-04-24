# ADR 0002 — Declarative Rule Schema Instead of Custom DSL

**Status:** Accepted  
**Date:** 2026-04-23

## Context

The seeded architecture specified a custom expression DSL for rule conditions:
```
avg(metric('windows.processor.% processor time[instance=_total]')) >= 80
percent_time_over(metric('...'), threshold=80, window_percent=20)
```

This requires a lexer, parser, type checker, and evaluator — all custom code.

## Decision

Phase 1 uses declarative comparators with five fields:

| Field | Purpose | Example |
|-------|---------|---------|
| `metric` | Canonical metric ID | `processor.percent_processor_time` |
| `aggregation` | How to reduce the series | `avg`, `min`, `max`, `p95`, `trend` |
| `operator` | Comparison direction | `gt`, `lt`, `ge`, `le`, `eq` |
| `threshold` | Numeric or host_context expression | `80` or `{host_context: total_physical_memory_mb, factor: 0.10}` |
| `duration_percent` | % of samples that must satisfy the condition | `20` (= 20%) |

## Why This Covers All Legacy PAL Patterns

| Legacy pattern | Declarative representation |
|---------------|---------------------------|
| Absolute threshold (CPU > 80%) | `aggregation: avg, operator: gt, threshold: 80, duration_percent: 20` |
| Inverted threshold (PLE < 300) | `aggregation: avg, operator: lt, threshold: 300` |
| Trend threshold (leak detection) | `aggregation: trend, operator: gt, threshold: 100` |
| RAM-relative (< 10% of RAM) | `threshold: {host_context: total_physical_memory_mb, factor: 0.10}` |
| CPU-count-relative (> 2 × cores) | `threshold: {host_context: logical_processor_count, factor: 2}` |
| Existence (any > 0) | `aggregation: max, operator: gt, threshold: 0, duration_percent: 1` |

## Benefits

- **No parser.** Schema validation is pure JSON Schema pattern matching + semantic checks in `PackValidator.cs`.
- **Diffable.** Every threshold change is a readable YAML diff, not an opaque expression string.
- **Testable.** `RuleEvaluator.Evaluate()` is a pure function over 5 typed inputs.
- **Portable.** Any future report consumer or UI can reconstruct human-readable expressions from the structured fields.

## Migration Path to CEL / Expression Language

If future requirements (complex multi-metric ratios, boolean logic between conditions, per-instance correlation)
cannot be expressed declaratively:

1. Add an optional `expression` field alongside the declarative fields in schema v1.1.
2. `expression` takes precedence over the declarative fields when present.
3. Implement a CEL evaluator in `Pal.Engine/Rules/CelEvaluator.cs` (isolated, no impact on existing rules).
4. Migrate rules incrementally — existing declarative rules continue working unchanged.

## Rejected Alternatives

- **Full CEL in v1:** Adds a non-trivial dependency and a complex type system. No current rule pattern requires it.
- **Jsonnet templating:** Powerful but unfamiliar to most Windows operations teams who are the target pack authors.
