---
_layout: landing
title: PAL-X Documentation
description: Performance Analysis of Logs — modernized, opinionated, automatable.
---

# PAL-X

<!--
LEARNING-MODE TODO (hero copy): replace the placeholder paragraph below with your own one-sentence value proposition.
Constraints: ≤30 words, present tense, names the audience (Windows ops engineers / SREs / platform teams),
names the input (perfmon CSV/BLG captures), and names the deliverable (findings + report).
Example prompt: "PAL-X reads your perfmon CSV or BLG capture and produces a ranked, evidence-cited list of
performance findings, locally or as a multi-tenant service." — improve on this.
-->

**PAL-X reads a perfmon CSV or BLG capture and produces a ranked, evidence-cited report of performance findings — locally from the command line, or hosted as a multi-tenant API.**

## What's here

This site is the user-facing documentation for PAL-X. The headings below are placeholders that will fill in as each section ships; for now, the most useful entry points are the **architecture decisions** (ratified ADRs) and the source repository itself.

- **Getting Started** — install, run your first local analysis, run your first remote analysis. *(coming up)*
- **Concepts** — datasets, packs, rules, findings, baselines, comparisons, trends, correlations, diagnostics, alerts, multi-tenancy. *(coming up)*
- **Guides** — task-oriented how-tos for the most common workflows. *(coming up)*
- **Reference** — every CLI command, every HTTP endpoint, every pack-schema field, every config key. *(coming up)*
- **Operations** — deploying and running the API in production. *(coming up)*
- **Architecture** — high-level overview plus the ratified ADRs at `docs/architecture/adr/`.
- **Contributing** — how to set up a dev environment and propose changes. *(coming up)*

## Architecture Decision Records (available now)

- [ADR 0001 — Ratified Deviations from Seed Documentation](architecture/adr/0001-deviations-from-seed-docs.md)
- [ADR 0002 — Declarative Rule Schema Instead of Custom DSL](architecture/adr/0002-declarative-rule-schema.md)
- [ADR 0003 — Pack Signing Format and Trust Model](architecture/adr/0003-pack-signing-format.md)
- [ADR 0004 — Schema v1.1: Rolling-Window Aggregations](architecture/adr/0004-schema-v1.1-rolling-windows.md)

## License & status

PAL-X is currently pre-1.0. Phase 4 has shipped; see the [project README](https://github.com/joshuatownsend/PAL-X) for status. The license for this codebase is being finalized.
