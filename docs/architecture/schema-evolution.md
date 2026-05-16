---
title: Schema evolution
description: How PAL-X evolves its pack and report schemas — what v1→v1.1 taught us and the contract for future versions.
---

# Schema evolution

PAL-X has two versioned schemas: `pal.pack/v*` (the YAML rules format) and `pal.report/v*` (the JSON output). Both have a `schema_version` field; both can evolve independently. This page is the contract for how that evolution works in practice.

For the concrete schema specs, see **[Reference — Pack schema v1](../reference/pack-schema-v1.md)** / **[v1.1](../reference/pack-schema-v1.1.md)** / **[Report schema](../reference/report-schema.md)**.

## Two schemas, two histories

| Schema | Current | History |
|---|---|---|
| `pal.pack/v*` | `v1.1` | v1 → v1.1 (rolling-window aggregations added; see [ADR 0004](adr/0004-schema-v1.1-rolling-windows.md)) |
| `pal.report/v*` | `v1` | No published changes since launch. |

The pack schema has changed once (v1 → v1.1). The report schema hasn't changed in a published release — but the contract for when it will is the same one v1.1 followed.

## What "evolve" can mean

Three flavours of change, in increasing disruption:

### Additive change — minor bump, schema_version string changes

The pack schema went from `v1` to `v1.1` by **adding an optional `window:` field on conditions**. Existing v1 packs remain valid; the validator accepts them with `schema_version: "pal.pack/v1"`. New features require bumping `schema_version: "pal.pack/v1.1"` (the validator enforces that `window:` is only allowed at v1.1).

This is the cheapest kind of change for consumers. Existing packs need no update. The downside: every additive change permanently increments the schema version string, and the validator's enum grows.

ADR 0004 documents the alternative we considered (separate `pal.pack.v1.1.json` file) and why we chose in-place. The short version: an in-place additive change with a discriminator field is cleaner than maintaining two files and a discriminator in the loader.

### Breaking change — major bump (none shipped yet)

A v2 of either schema would mean shipping a separate file (`pal.pack.v2.json`) and either:

- **Auto-migrating v1 packs at load time.** Loader detects `schema_version: "pal.pack/v1.1"` and rewrites to v2 in memory. Existing packs continue working with no edits, at the cost of a v1 → v2 migration table in code.
- **Requiring explicit author migration.** v1 packs continue to validate against v1; v2 features require re-authoring. Two validators ship side-by-side.

The right choice depends on the size of the breaking change. A small change probably warrants auto-migration; a large change probably warrants the explicit fork. Neither has been needed yet.

### Deprecation — flag in current schema, remove in next major

If a v1 feature should be removed, mark it deprecated in the current docs and code (the validator can emit a warning) but don't actually remove. Removal happens at the next major bump, where breaking changes are expected anyway. This gives consumers a runway to migrate.

No features are currently deprecated.

## v1 → v1.1 — case study

The actual change was small: **one optional field on `Condition`** (`window:`) plus a constraint that `aggregation: trend` isn't allowed with `window:`.

What we got right:

- **Validator gates the new feature by `schema_version`.** A v1 pack with `window:` fails validation with a specific error message: `'window' requires schema_version pal.pack/v1.1`. The error is actionable.
- **Both versions live in the same JSON Schema file** (`pal.pack.v1.json`) with a discriminator in `schema_version`. One file, one loader.
- **No engine refactor required.** The rule evaluator already iterated samples; the change was "decide which samples to iterate" — windowed selection vs full-series. Most of the work was in the validator, not the engine.

What we'd do differently:

- The schema version string `"pal.pack/v1.1"` is awkward (the slash suggests a hierarchy that isn't really there). A future schema would probably use a flatter version (`schema_version: "1.1"` with a separate `schema_id` carrying the namespace).

## Versioning policy

Going forward, the policy:

- **Patch version**: documentation only. Schema bytes don't change.
- **Minor version** (e.g., v1.1, v1.2): additive only. Existing valid documents remain valid. New features may be gated on the version string.
- **Major version** (e.g., v2): breaking changes allowed. Loader strategy (auto-migrate or fork) decided at design time.

The version lives in the `schema_version` field of the document itself, **not** in a filename or directory. A v1.5 pack is identifiable by its content, not its location on disk.

## Report schema — same rules, slower cadence

`pal.report/v1` is the output side. It hasn't bumped because the output shape has been adequately stable. The same rules apply when it does:

- Additive fields → `pal.report/v1.x`.
- Breaking changes → `pal.report/v2`.
- Schema file at `dotnet/schemas/pal.report.v*.json`.

The asymmetry between pack and report cadence is natural: packs gain expressive features as users find limits in the rule format. Reports change only when the engine emits new kinds of information.

## How the engine knows which schema

For packs, `PackLoader` reads `schema_version` from the YAML and the validator dispatches based on the enum:

```csharp
private static readonly HashSet<string> ValidSchemaVersions = ["pal.pack/v1", "pal.pack/v1.1"];

if (!ValidSchemaVersions.Contains(pack.SchemaVersion))
    errors.Add($"schema_version '{pack.SchemaVersion}' is not recognized (expected pal.pack/v1 or pal.pack/v1.1)");
```

Anything else is rejected outright. The set is hardcoded in `PackValidator` — bumping the schema means updating the validator's allow-list in the same change.

For reports, the writer hardcodes `"pal.report/v1"` as the constant value. A future version would add a constructor parameter or use a different writer class entirely.

## Stable IDs across schema versions

A key property: a **rule's finding_id is stable across schema bumps**. The hash inputs (`rule_id`, `canonical_metric`, `window_start`, `window_end`) are content fields that don't change when the schema gains new optional fields elsewhere. So a finding produced by a v1 pack and a v1.1 version of the same rule on the same data has the same `finding_id`.

This matters for downstream tooling that joins findings across reports — upgrading the pack version shouldn't break those joins.

## Related

- **[ADR 0004 — Schema v1.1: Rolling-Window Aggregations](adr/0004-schema-v1.1-rolling-windows.md)** — the only schema bump we've shipped, plus alternatives considered.
- **[Reference — Pack schema v1](../reference/pack-schema-v1.md)** / **[v1.1](../reference/pack-schema-v1.1.md)** / **[Report schema](../reference/report-schema.md)** — current schemas.
- **[Data flow — Hop 3: Pack loading](data-flow.md#hop-3--pack-loading)** — where the version is read.
