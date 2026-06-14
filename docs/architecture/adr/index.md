---
title: Architecture Decision Records
description: Index of every accepted ADR, with status and one-line summary.
---

# Architecture Decision Records

Architecture Decision Records (ADRs) document **why** a particular architectural choice was made — the context, the alternatives considered, the trade-offs accepted. They're written once and not edited; if a decision changes, a new ADR supersedes the old.

For background on the ADR practice, see Michael Nygard's [original post](https://www.cognitect.com/blog/2011/11/15/documenting-architecture-decisions). PAL-X follows the standard lightweight pattern: one Markdown file per decision, numbered chronologically, with a status field.

## Status legend

| Status | Meaning |
|---|---|
| `Proposed` | Under discussion. Not yet implemented. |
| `Accepted` | Decided, ratified, implemented. |
| `Deprecated` | Decision still applies historically but a new ADR supersedes the recommendation. |
| `Superseded by ADR-####` | Replaced. The new ADR documents the change. |

No ADRs are currently `Deprecated` or `Superseded`.

## Accepted ADRs

| # | Title | Date | Status | One-line summary |
|---:|---|---|---|---|
| 0001 | [Ratified Deviations from Seed Documentation](0001-deviations-from-seed-docs.md) | 2026-04-23 | Accepted | 12 deviations from the ChatGPT-generated seed docs, ratified at project kickoff: tri-state status (no 0–100 score), declarative comparators (no DSL), content-hash IDs, snake_case fields, `host_context` in v1 schema, Spectre.Console.Cli over System.CommandLine, ScottPlot for charts, and others. |
| 0002 | [Declarative Rule Schema Instead of Custom DSL](0002-declarative-rule-schema.md) | 2026-04-23 | Accepted | Rule conditions are declarative — `metric` + `aggregation` + `operator` + `threshold` + `duration_percent` + optional `window`. No expression parser. Trades expressivity for stability, auditability, and zero parser maintenance. |
| 0003 | [Pack Signing Format and Trust Model](0003-pack-signing-format.md) | 2026-04-27 | Accepted | RSA-PSS-SHA256 with 3072-bit keys, signing raw `pack.yaml` bytes, sidecar file at `pack.yaml.sig`. BCL-only (no NuGet dep). Trust model is consumer-rooted via embedded project key + CLI `--trust-key`. |
| 0004 | [Schema v1.1: Rolling-Window Aggregations (In-Place Enum Bump)](0004-schema-v1.1-rolling-windows.md) | 2026-04-27 | Accepted | Pack schema gains rolling-window aggregations via an additive `window:` field on `Condition`. Schema discriminator is in-place (`schema_version: "pal.pack/v1.1"`); no new JSON Schema file. Validator gates `window:` on the v1.1 version. |
| 0005 | [Workload Pack Category Vocabulary (and `windows-core` as the shared base)](0005-workload-pack-categories.md) | 2026-06-13 | Accepted | Rule `category` stays a closed, controlled vocabulary but grows additively per workload family (this wave adds `dotnet` and `ad`), updated in the pack **and** report JSON schema enums plus `PackValidator`. Adopts `windows-core` (always-on) as the shared base for workload packs (strategy-doc Option B); defers a `depends:` schema key. |

## Reading an ADR

Each ADR follows the same structure:

- **Context** — the problem being solved and the constraints.
- **Decisions** — what was chosen, often broken into sub-decisions.
- **Consequences** — what changed, what we gave up, what's now harder or easier.
- **Alternatives considered** — what we didn't pick and why.

The most important read for new contributors is **[ADR 0001](0001-deviations-from-seed-docs.md)** — it documents every design choice that diverges from the seeded ChatGPT spec, and the diverging choice is the load-bearing one in nearly every case.

## Authoring a new ADR

When a non-trivial architectural decision is made:

1. Number the new ADR sequentially (e.g., `0005-…`).
2. Use the same heading structure as the existing ones.
3. Set status to `Accepted` once the decision is final; don't ship `Proposed` ADRs in production branches.
4. Link to the ADR from any code or doc that implements it — bidirectional references catch drift.
5. Add an entry to this index.

If an ADR supersedes an earlier one, update the earlier ADR's status to `Superseded by ADR-####` and link forward.

ADRs are not retrospective documentation. If a decision was made informally and you're documenting it after the fact, that's fine — but make it clear in the Context section. Date the ADR with when it was written; date the decision (in Context) with when it was made.

## Where ADRs are NOT the answer

ADRs are heavyweight. Don't write one for:

- **Bug fixes** — those live in commit messages and PR descriptions.
- **Refactors that preserve external behaviour** — same.
- **Tactical implementation choices** — e.g., "use a `HashSet` here" doesn't need an ADR.
- **Configuration defaults** — those belong in `appsettings.json` and **[Reference — Configuration](../../reference/configuration.md)**.

ADRs are for **decisions that constrain future work** — choices a future contributor needs to know about to avoid relitigating, breaking, or reversing without strong cause.

## Related

- **[Architecture index](../index.md)** — the broader architecture context.
- **[Data flow](../data-flow.md)** / **[Persistence](../persistence.md)** / **[Schema evolution](../schema-evolution.md)** — implementations of these decisions.
