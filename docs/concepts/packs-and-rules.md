---
title: Packs and rules
description: What a pack is, when to author one, and why every rule condition is a declarative comparator rather than an expression.
---

# Packs and rules

A **pack** is a YAML document containing rules. A **rule** declares when a finding should fire — given a metric, an aggregation, a comparison, and a threshold. PAL-X ships three packs out of the box (`windows-core`, `iis-core`, `sql-host-core`); you author your own when you need rules they don't cover.

For the field-level contract, see **[pack-schema-v1](../reference/pack-schema-v1.md)**. This page covers the why.

## Why packs at all?

PAL-X is a deterministic reporter. The interesting question is "given this capture, which signals are concerning?" The set of "concerning signals" is workload-dependent — what's normal CPU pressure for a build server is critical for a web tier — so the rules can't be baked into the engine.

A pack solves three problems at once:

1. **Sharing.** Teams can publish rules to a common location and consumers load them by reference. Each pack carries its own version (semver) so a consumer pins to a known-good ruleset.
2. **Composition.** Multiple packs can apply to the same capture. Windows-core + IIS-core both fire against a web-tier capture; the analyzer evaluates each rule independently and merges findings.
3. **Auditability.** A pack is a single YAML file. You can read it, diff it, sign it, and pin it. No code in a pack — no surprises.

## Declarative, not expressive

Every condition in a pack is the same shape:

```yaml
- metric: <canonical-metric-id>
  aggregation: <avg|p95|...>
  operator: <gt|lt|...>
  threshold: <number-or-host-context>
  duration_percent: <0..100>
```

There is no expression language. No `if cpu > 80 and disk_queue > 2`. No `avg(cpu) / count(cores)` math.

That sounds limiting. The trade-off is intentional — see **[ADR 0002 — Declarative Rule Schema](../architecture/adr/0002-declarative-rule-schema.md)** for the rationale. The short version:

- Every condition becomes a row in a table. Easy to validate, easy to lint, easy to diff between pack versions.
- No DSL means no parser, no operator precedence questions, no "what does `cpu * 1.5 > mem / 100` actually evaluate to."
- Conjunction is the only combinator: a rule with two conditions fires when *both* are satisfied. Disjunction is handled by authoring two rules.

When you outgrow what a declarative comparator can express, the answer is "write the rule slightly differently" rather than "extend the DSL." That trade-off favours pack readability and pack stability over expressivity.

## Three classes of pack

| Class | Example | When loaded |
|---|---|---|
| **Always-applies** | `windows-core` | The pack's `applicability.always: true` — loaded against every dataset. Holds rules that apply to any Windows host. |
| **Conditional** | `iis-core`, `sql-host-core` | The pack's `applicability.requires_any` lists canonical metric IDs; the pack loads only if at least one is present. Holds workload-specific rules. |
| **Explicit** | Vendor packs, custom packs | Loaded only when the operator passes `--pack <id>` (CLI) or includes it in the `packs` array (API). Used for risky / contextual rules you don't want firing by default. |

The first two are auto-loaded; the third is opt-in. The loaded set is recorded in the report's `packs` array with `resolution_mode: explicit | auto` so readers can audit which rules ran.

## When to write a pack

You write a pack when the shipped packs don't fire on something they should — or fire on something they shouldn't. The common cases:

- **A counter PAL-X doesn't recognise out of the box.** Most non-English Windows builds or vendor agents emit counter paths that don't match the built-in `MetricAliasRegistry`. Adding `metric_aliases` to a pack maps them onto canonical IDs and lets shipped rules fire against your captures unchanged.
- **A site-specific threshold.** The shipped `high-cpu-sustained` rule fires above 80%. Your build farm runs hot intentionally; you want it to fire only above 95%. Author a one-rule pack that supersedes (or sits alongside) the shipped one.
- **A workload PAL-X doesn't ship rules for.** Custom application counters, third-party services, internal middleware. Author a pack with applicability gated on the metrics you care about.

For each case the workflow is the same — see **[Write a pack](../guides/write-a-pack.md)**.

## Rules, recommendations, and recommendations

A rule's `recommendations: []` doesn't carry text — it carries IDs. The text lives in the pack-level `recommendations:` map. This is deliberate: a recommendation like *"identify which process is consuming CPU"* applies to several rules (sustained CPU, processor queue, context switches), so the body lives once and rules reference it.

The validator rejects a rule that references a recommendation ID not defined in the pack. The report inlines the resolved body so consumers don't need the pack file to render findings.

## Signing — for shared packs

For packs distributed across teams, signing is the answer to "did this YAML actually come from someone we trust?" The implementation is RSA-PSS-SHA256 over the raw `pack.yaml` bytes, with a `pack.yaml.sig` sidecar. See **[ADR 0003 — Pack Signing Format](../architecture/adr/0003-pack-signing-format.md)** for the trust model and **[Sign and trust packs](../guides/sign-and-trust-packs.md)** for the operator workflow.

## Related

- **[Pack schema v1](../reference/pack-schema-v1.md)** — field-by-field reference.
- **[Pack schema v1.1](../reference/pack-schema-v1.1.md)** — the rolling-window extension.
- **[Write a pack](../guides/write-a-pack.md)** — author your first pack end-to-end.
- **[Validate a pack](../guides/validate-a-pack.md)** — `pal validate-pack` in CI.
- **[Sign and trust packs](../guides/sign-and-trust-packs.md)** — RSA-PSS signing workflow.
- **[Rolling-window rules](../guides/rolling-window-rules.md)** — using schema v1.1 windows.
- **[ADR 0002 — Declarative Rule Schema](../architecture/adr/0002-declarative-rule-schema.md)** — why no DSL.
