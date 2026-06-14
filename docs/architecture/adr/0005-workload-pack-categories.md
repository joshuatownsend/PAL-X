# ADR 0005 — Workload Pack Category Vocabulary (and `windows-core` as the shared base)

**Status:** Accepted  
**Date:** 2026-06-13  
**Deciders:** Josh Townsend (project lead)

## Context

The rule-pack coverage roadmap (`docs/architecture/design/rule-pack-coverage-strategy.md`)
begins porting legacy-PAL threshold files into PAL-X packs, starting with a `dotnet-clr`
pack (.NET CLR + ASP.NET). Authoring the first workload pack surfaced two design questions
the strategy spike flagged for a decision:

1. **Rule `category` is a closed vocabulary.** A rule's `category` is validated against a
   fixed allow-list in **two** places — the JSON schema enum
   (`dotnet/schemas/pal.pack.v1.json`) and a C# `HashSet` in
   `PackValidator.ValidCategories` (`dotnet/src/Pal.Packs/PackValidator.cs`). The original
   list — `cpu, memory, disk, network, process, iis, sql, system, collection,
   pack-validation` — is Windows-core-centric and has no value for managed-runtime
   workloads (nor, for later waves, Active Directory, Exchange, Hyper-V, SharePoint). A
   `.NET` rule could only be shoehorned into `process`/`memory`, which mixes its findings
   with OS-level findings in the report's `category_statuses` grouping.

2. **No shared-base mechanism.** Legacy PAL used `<INHERITANCE FILEPATH="SystemOverview.xml" />`
   so every app threshold file reused a common CPU/memory/disk base. The pack schema has no
   `depends`/`inherit`/`extends` key.

`category` flows into report output: `StatusClassifier.ClassifyByCategory`
(`dotnet/src/Pal.Engine/Scoring/StatusClassifier.cs`) groups findings by category to compute
`category_statuses`. Crucially, that grouping is **dynamic** (`GroupBy(f => f.Category)`) — it
does not enumerate a hard-coded category list — so adding a category value does not change any
report-shaping code, and captures without the new counters produce no findings in the new
category (existing golden fixtures are unaffected).

## Decision

### 1. Extend the category vocabulary additively, per workload family

Keep `category` a **closed, controlled vocabulary** (not a free-form string) so report grouping
stays consistent and typos remain validation errors. Grow it by adding one value per workload
family as packs land, in **both** gates:

- the `category` enum in `dotnet/schemas/pal.pack.v1.json`, and
- `PackValidator.ValidCategories` in `dotnet/src/Pal.Packs/PackValidator.cs`.

This ADR adds the **first two** workload-family values together, because the first wave of the
strategy doc ships two packs: **`dotnet`** (managed runtime: .NET CLR and ASP.NET application
execution) for the `dotnet-clr` pack, and **`ad`** (Active Directory domain-controller health:
NTDS) for the `active-directory` pack. Future waves add their own value in the same additive way
(e.g. `exchange`, `hyperv`, `sharepoint`) — each in the pack's own implementation change, not
speculatively here.

**Rejected alternative — free-form `category` string:** would remove the typo-catching
validation and let report grouping fragment into near-duplicate categories
(`dotnet` vs `dotnet-clr` vs `.net`). The controlled vocabulary is the point.

### 2. `windows-core` is the shared base (strategy doc Option B)

Adopt the strategy doc's recommended Option B rather than a schema `depends:`/`extends:` key.
`windows-core` already declares `applicability: { always: true }`, so it loads alongside every
workload pack automatically — exactly the "every run gets the base" semantics legacy achieved
with inheritance. **Workload packs therefore declare only their workload-specific rules and must
not redeclare base CPU/memory/disk rules.** If a base rule a workload needs is missing, it is
added to `windows-core` (a deliberate, reviewed change to the shared pack), not duplicated into
the app pack.

**Deferred — Option C (`depends:`/`extends:` schema key):** revisit only if a workload ever
needs a base *other than* `windows-core` (e.g. a VMware-guest base distinct from physical). It
would require a schema change, `PackResolver`/`PackValidator` support, and merge semantics — its
own ADR. Until a concrete case exists, it is over-engineering.

## Consequences

- Adding a workload category is a two-line, additive change (schema enum + validator set) with
  no impact on report-shaping code or existing golden fixtures. Captures without the new
  counters never produce findings in the new category.
- The `dotnet` category ships with the `dotnet-clr` pack. ASP.NET *hosting/queue* health
  (requests rejected, request wait time, requests queued, application restarts) remains in
  `iis-core` under category `iis`; `dotnet-clr` covers managed-runtime concerns (CLR exceptions,
  GC) and ASP.NET *application execution time* — the split avoids duplicate findings.
- The `ad` category ships with the `active-directory` pack (NTDS LDAP bind latency and DRA
  replication backlog), which domain-controller captures load alongside `windows-core`.
- The finding `category` enum lives in **two** schemas — `pal.pack.v1.json` (input) and
  `pal.report.v1.json` (output). Both must grow together, or reports emitted by a newly valid
  pack would fail validation against `pal.report/v1`. Adding a workload category is therefore a
  change in both schema enums plus the validator set.
- Workload packs stay small and focused because the base comes from `windows-core`. A reviewer
  of a new pack should confirm it does not redeclare base rules and that any genuinely missing
  base rule is added to `windows-core` instead.
- The controlled vocabulary means every new workload family touches this list once; that is the
  intended, auditable extension point, recorded here so the pattern is followed per wave.

## Changes made under this ADR

- `dotnet/schemas/pal.pack.v1.json`: added `"dotnet"` and `"ad"` to the `category` enum.
- `dotnet/schemas/pal.report.v1.json`: added `"dotnet"` and `"ad"` to the finding `category` enum,
  keeping the output schema in sync with the pack schema so emitted reports validate against
  `pal.report/v1`.
- `dotnet/src/Pal.Packs/PackValidator.cs`: added `"dotnet"` and `"ad"` to `ValidCategories`.
- `dotnet/src/Pal.Engine/Model/Finding.cs`: updated the `Category` doc comment to list the new values.
- `dotnet/src/Pal.Engine/Normalization/MetricAliasRegistry.cs`: added `dotnetclr.*` aliases
  (CLR exceptions/sec, % Time in GC) and a versioned-path alias for ASP.NET Request Execution
  Time, plus `ntds.*` aliases (LDAP bind time, DRA pending replication operations).
- `packs/thresholds/dotnet-clr/pack.yaml`: new managed-runtime workload pack (first port under the strategy doc).
- `packs/thresholds/active-directory/pack.yaml`: new Active Directory (NTDS) workload pack.
