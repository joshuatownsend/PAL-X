# ADR 0001 — Ratified Deviations from Seed Documentation

**Status:** Accepted  
**Date:** 2026-04-23  
**Deciders:** Josh Townsend (project lead)

## Context

The PAL-X repository was seeded with ChatGPT-generated Product Requirements Documents and architectural
specifications covering a 4-phase platform build. A skeptical second-pass review identified 12 places
where the seed documents prescribed choices that experienced engineers would push back on. All 12 deviations
were presented to the project lead and ratified before implementation began.

The seed docs remain on disk as historical reference in `docs/PAL-2026-Implementation-Spec-Pack/`. This ADR
documents what changed and why, so future contributors understand the authority of each document.

---

## Ratified Deviations

### 1. Polyglot monorepo → .NET-only

**Seed:** Next.js web app, Fastify API, 4 Node.js workers, 6 JS packages under `apps/`, `services/`, `packages/*`.  
**Phase 1:** .NET only. No JavaScript, no `package.json`, no `turbo.json`.  
**Why:** Phase 1 has no network surface. Scaffolded TypeScript files rot before Phase 2 ships. The
full-stack structure is re-added when a real web consumer exists, not speculatively.  
**Reversibility:** High. Add `apps/` and `packages/` when Phase 2 begins; nothing in Phase 1 prevents it.

---

### 2. Custom expression DSL → Declarative comparators

**Seed:** Custom expression language: `avg(metric('...')) >= 80`, `percent_time_over(threshold, ...)`.  
**Phase 1:** Declarative fields: `metric + aggregation + operator + threshold + duration_percent`.  
**Why:** A custom DSL requires a parser, tokenizer, and evaluator — 1-2 sessions of work that covers no
rule patterns the seeded DSL already handles. Every legacy PAL rule pattern maps cleanly to the
declarative form. CEL/Jsonnet remain a future upgrade path if needed.  
**Reversibility:** Medium. Schema v1.1 can add an `expression` field alongside declarative fields.

---

### 3. Seeded pack schema → Revised pack schema v1

**Seed:** `docs/PAL-2026-Implementation-Spec-Pack/PAL-Pack-Schema-v1.md`  
**Phase 1:** `dotnet/schemas/pal.pack.v1.json` (this codebase) is the authoritative schema.  
**Why:** The seeded schema used the custom expression DSL, lacked `host_context` support, and used
non-snake_case metric IDs. The revised schema is consistent with the engine we actually built.  
**Reversibility:** N/A. The seeded doc is reference material only.

---

### 4. Spaces/% in canonical metric IDs → snake_case

**Seed:** Counter names preserve spaces and `%`: `Memory.Available MBytes`, `Processor.% Processor Time`.  
**Phase 1:** `memory.available_mbytes`, `processor.percent_processor_time`.  
**Why:** Spaces and `%` require escaping in YAML, JSON, URLs, log messages, and command-line arguments.
Snake_case is stable across every serialization format. Legacy names live in the `metric_aliases`
table in each pack and in `MetricAliasRegistry.cs`.  
**Reversibility:** N/A. The alias registry handles translation.

---

### 5. Dynamic thresholds deferred → `host_context` in schema v1

**Seed:** RAM-relative and CPU-count-relative thresholds deferred to schema v1.1.  
**Phase 1:** `host_context.total_physical_memory_mb` and `host_context.logical_processor_count`
are first-class threshold variables in schema v1.  
**Why:** Without `host_context`, Phase 1 is a regression from legacy PAL. Rules like "Available MBytes
< 10% of RAM" cannot be expressed. The dynamic threshold patterns in legacy PAL are among the most
important rules (memory, processor queue, context switches).  
**Reversibility:** N/A. `host_context` is in v1 by design.

---

### 6. ULID-style time-sortable IDs → Content-hash IDs

**Seed:** `finding_id = ULID seeded from input hash`.  
**Phase 1:** `finding_id = base32(SHA-256(rule_id || canonical_metric || window_start || window_end)[0..10])`.  
**Why:** Content-hash IDs are deterministic across machines and time — two runs with the same input
produce the same IDs. ULID IDs involve a time component that would break fixture tests.  
**Reversibility:** Medium. A v2 schema can change the ID scheme.

---

### 7. Additive health score (30/10/2) → Tri-state status

**Seed:** `overall_health_score = 100 − min(100, Σ impacts)`. Critical=30, Warning=10, Info=2.  
**Phase 1:** `overall_status: critical | warning | healthy`. Per-category breakdown uses the same enum.  
**Why:** The additive formula is misleading. 3 medium warnings → score "70" (seems good).
7 critical findings → score "0" regardless of whether there are 7 or 70. A tri-state status
preserves the severity signal: any critical → Critical; any warning (no criticals) → Warning; else Healthy.  
**Reversibility:** Medium. A score can be added alongside status in a future schema version.

---

### 8. `System.CommandLine` → `Spectre.Console.Cli`

**Seed:** `System.CommandLine` for the CLI.  
**Phase 1:** `Spectre.Console.Cli`.  
**Why:** `System.CommandLine` has been in public beta for years with breaking API churn between versions.
`Spectre.Console.Cli` is production-stable with better help text, error UX, and an active maintenance track.  
**Reversibility:** Medium. CLI commands are isolated; switching would be a refactor, not a rewrite.

---

### 9. Hand-rolled `SvgLineChartRenderer` → `ScottPlot`

**Seed:** Custom SVG renderer built from scratch.  
**Phase 1:** `ScottPlot` (5.x) for deterministic server-side SVG with threshold bands.  
**Why:** Eliminates 1-2 sessions of chart rendering work. ScottPlot is proven in .NET diagnostics tooling.  
**Risk:** ScottPlot 5.x SVG rendering is generally deterministic but axis tick spacing can vary with
font metrics on some systems. A unit test asserting byte-identical SVG on two renders of the same
data is included to catch any non-determinism early.  
**Reversibility:** High. `ScottPlotRenderer.cs` is isolated; swap if the determinism test fails.  
**Status (2026-04-27):** Implemented in Phase 1.5. `SvgCanonicalizer` post-processes Skia-generated
SVG to normalize auto-generated clip IDs and strip comments. Byte-identical determinism test passes.

---

### 10. `packages/contracts/schemas/` → `dotnet/schemas/`

**Seed:** JSON schemas in a JS-workspace-managed `packages/contracts/schemas/` directory.  
**Phase 1:** `dotnet/schemas/` with a repo-root `schemas/README.md` pointer for external tooling.  
**Why:** There is no JS workspace in Phase 1. Schemas ship next to the code that consumes them.  
**Reversibility:** High. Copy to `packages/contracts/schemas/` in Phase 2 when the JS workspace exists.

---

### 11. 10 JSON schemas scaffolded → 2 schemas only

**Seed:** 10 schemas pre-scaffolded in `packages/contracts/schemas/`.  
**Phase 1:** 2 schemas: `pal.pack.v1.json` and `pal.report.v1.json`.  
**Why:** The other 8 schemas (evidence-bundle, baseline, alert, compare, etc.) are guesses about
future phases. Writing them now guarantees rewrites when the actual phase requirements are known.  
**Reversibility:** N/A. Additional schemas land with their owning phase.

---

### 12. Full BLG implementation → BLG stub

**Seed:** Full BLG collector in Phase 1.  
**Phase 1:** `BlgCollectorStub` throws `NotSupportedException` with the exact `relog -f CSV ...` command.  
**Why:** PDH interop is Windows-only native code that adds build complexity. The CLI contract
spec's own implementation order lists BLG as step 9 of 11. The stub gives a clear error message
that unblocks users while Phase 1.5 adds the real implementation.  
**Reversibility:** High. Replace `BlgCollectorStub.ThrowNotSupported()` with a real PDH implementation.  
**Status (2026-04-27):** Phase 1.5 scope — see plan file. `IDatasetCollector` interface introduced;
`CollectorFactory` dispatches to `BlgCollector` (Windows PDH) or `CsvCollector` by file extension.

---

## Phase 2 Closure Note (2026-04-27)

Phase 2 ("Headless diagnostics platform") is complete. The following PRD §8.3–§8.5 capabilities were
delivered on `feature/phase-2-closure`:

- **Markdown report format** (`Pal.Reporting.Markdown.MarkdownReportWriter`) — third artifact alongside JSON/HTML.
- **Raw dataset download** — opt-in `IncludeDataset` flag on job submission; GZip-compressed JSON streamed via `GET /analysis/{id}/dataset`; purged by `RetentionWorker`.
- **Pack version listing** — `GET /packs/{id}/versions` exposes the version history stored by `PackRegistrySyncService`.
- **Pack validation API** — `GET /packs/{id}/versions/{version}/validation` runs `PackLoader` + `PackValidator` against stored YAML.
- **CLI parity** — `remote validate-pack`, `remote dataset`, `remote report --format markdown`, `remote submit --include-dataset` added to `Pal.Cli`.
