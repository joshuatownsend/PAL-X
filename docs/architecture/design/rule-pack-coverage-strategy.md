# Rule-Pack Coverage Strategy + Authoring Playbook

> **Status**: spike / pre-ADR  
> **Author**: design spike (plan 008), 2026-06-13  
> **Next step**: maintainer acceptance promotes the shared-base recommendation
> (section 4) to an ADR under
> `docs/architecture/adr/000N-shared-base-pack.md`, and spawns one implementation
> plan per first-wave pack (section 5) that cites the playbook in section 6.

---

## 1. Current State

PAL-X analyzes Windows/IIS/SQL performance captures by evaluating declarative rule
packs against a normalized dataset. It exists to reimplement the legacy "PAL"
(Performance Analysis of Logs) tool — but it ships only **three** packs against
legacy PAL's **78** threshold files. Closing that gap is the single largest barrier
to migrating legacy-PAL users and the clearest expansion of the product's core value.
This section establishes the facts the rest of the doc builds on.

### Shipped packs — exactly three

`ls packs/thresholds` returns exactly three directories:

| Pack | `pack.yaml` | `applicability` | Rule count |
|------|-------------|-----------------|-----------|
| `windows-core` | `packs/thresholds/windows-core/pack.yaml` | `always: true` (loads for every dataset) | 14 (per `validate-pack`) |
| `iis-core` | `packs/thresholds/iis-core/pack.yaml` | `requires_any:` on four `iis.*`/`aspnet.*` metrics | — |
| `sql-host-core` | `packs/thresholds/sql-host-core/pack.yaml` | counter-presence | — |

Auto-resolution is driven by `applicability`: `windows-core` declares `always: true`
(`packs/thresholds/windows-core/pack.yaml:7-8`) so it loads on every run, while
`iis-core` declares `requires_any` over a list of canonical metric IDs
(`packs/thresholds/iis-core/pack.yaml:7-12`) so it loads only when those counters are
present in the capture. `dotnet run --project dotnet/src/Pal.Cli -- validate-pack
--path packs/thresholds/windows-core` reports `Rules: 14 / Status: valid / Warnings: 0`.

### Pack shape (the authoring target)

A pack's top-level keys, modelled on `packs/thresholds/windows-core/pack.yaml`:

- `schema_version` (`"pal.pack/v1"`, or `"pal.pack/v1.1"` for rolling windows),
  `pack_id`, `pack_name`, `version`, `description`.
- `applicability` — one of `always: true`, `requires_any: [<metric_id>...]`, or
  `requires_all: [...]` (schema: `dotnet/schemas/pal.pack.v1.json:31-79`).
- `recommendations` — a keyed map of `{priority, text, rationale, links}` entries
  that rules reference by ID (`packs/thresholds/windows-core/pack.yaml:10-66`).
- `rules` — each with `rule_id, severity, category, title, summary, explanation`, an
  optional per-rule `applies_when` guard, a list of `conditions`
  (`metric, instance, aggregation, operator, threshold, duration_percent`), and a
  `recommendations` reference list
  (`packs/thresholds/windows-core/pack.yaml:70-91`).
- `metric_aliases` — **optional**; maps a canonical snake_case metric ID to one or
  more Windows counter-path patterns (schema: `dotnet/schemas/pal.pack.v1.json:35-43`).
  See the alias caveat below.

`threshold` may be a literal number or a `host_context` object
(`{host_context, factor, minimum, maximum}` — schema:
`dotnet/schemas/pal.pack.v1.json:81-101`), used by `windows-core` for the
RAM-relative `low-available-memory` rule
(`packs/thresholds/windows-core/pack.yaml:147-155`) and the CPU-count-relative
`excessive-context-switches` rule
(`packs/thresholds/windows-core/pack.yaml:340-347`).

### Where canonical metric IDs actually come from

**This is the most important correction a pack author must internalize.** The plan's
"Current state" describes `metric_aliases` (raw counter path → canonical ID) as the
mechanism, but the live code shows the **primary** alias source is C#, not YAML:

- `MetricAliasRegistry.BuildDefault()`
  (`dotnet/src/Pal.Engine/Normalization/MetricAliasRegistry.cs:10`) defines **63**
  `reg.Add(<regex>, <canonicalId>)` entries spanning ten canonical prefixes:
  `processor.`, `system.`, `memory.`, `pagingfile.`, `physicaldisk.`, `network.`,
  `process.`, `sql.`, `iis.`, `aspnet.`.
- A pack *can* contribute its own aliases — `MetricAliasRegistry.AddFromPack(...)`
  (`dotnet/src/Pal.Engine/Normalization/MetricAliasRegistry.cs:109`) reads a pack's
  `metric_aliases:` block and adds glob-to-regex entries. But **no shipped pack uses
  `metric_aliases`** today (`grep -rln 'metric_aliases' packs/` returns nothing); all
  three rely entirely on `BuildDefault`.

Consequence for the roadmap: a new workload pack whose counters (e.g. `\NTDS\...`,
`\.NET CLR Memory(*)\...`) are **not** already in `BuildDefault` needs new canonical
IDs. Whether those IDs are registered in `BuildDefault` (C# change — out of scope for
a pure pack plan) or supplied via the pack's own `metric_aliases:` block is a
per-pack porting decision the playbook (section 6) must call out. The collector
runs `Resolve` before packs are loaded, so the safe default for shipping packs is to
add the canonical regex to `BuildDefault`; `metric_aliases:` is the fallback for
out-of-tree / contributor packs that cannot touch C#.

### Legacy threshold library — 78 XML files, 75 unique

`find legacy/pal-v2 -iname '*.xml' | wc -l` returns **78**; of these, **75** have
unique basenames. The canonical library lives in
`legacy/pal-v2/PAL2/PALWizard/bin/Debug/` (a checked-in build-output directory). Two
basenames duplicate into a second build dir and are not extra workloads:
`PALFunctions.xml` (also under `PAL2/PALFunctions/{bin,obj}/Debug/`) and
`PALWizard.xml` (also under `PAL2/PALWizard/obj/Debug/`). The submodule is read-only
reference per `CLAUDE.md` — this spike does not modify it.

### Legacy threshold structure and what maps

A legacy threshold file is a `<PAL>` root containing `<ANALYSIS>` blocks. Each
`<ANALYSIS>` has a `<DATASOURCE>` (the counter path), zero or more `<THRESHOLD>`
blocks (each carrying a `CONDITION` of `Warning`/`Critical` and a PowerShell `<CODE>`
expression), optional `<CHART>` elements, and an HTML `<DESCRIPTION>`. Example from
`legacy/pal-v2/PAL2/PALWizard/bin/Debug/DotNet.xml:4-8`:

```xml
<THRESHOLD NAME="More than 10 .NET CLR Exceptions Thrown / sec" CONDITION="Warning" COLOR="Yellow" PRIORITY="50">
  <CODE><![CDATA[
    StaticThreshold -CollectionOfCounterInstances $CollectionOfNETCLRExceptionsNumberOfExcepsThrownsecALL -Operator 'gt' -Threshold 10
  ]]></CODE>
</THRESHOLD>
```

**What maps cleanly to PAL-X's declarative shape:**

| Legacy construct | PAL-X equivalent |
|------------------|------------------|
| `<DATASOURCE EXPRESSIONPATH="\.NET CLR ...">` | a `metric:` canonical ID + the raw path as a `metric_aliases` / `BuildDefault` regex |
| `<THRESHOLD CONDITION="Warning">` / `"Critical"` | `severity: warning` / `severity: critical` |
| `StaticThreshold -Operator 'gt' -Threshold 10` | `condition: { operator: gt, threshold: 10, aggregation: avg }` |
| `CATEGORY="..."` | `category:` |
| `<DESCRIPTION>` HTML prose + "Next Steps" | a `recommendations:` entry (`text` + `rationale` + `links`) |
| per-counter instance wildcard `(*)` | `instance: "*"` (or omit) |

**What does NOT map** (must be re-expressed or dropped — these are the deviations
from the seed docs ratified in ADR 0001/0002):

- **Trend thresholds.** `DotNet.xml:54-57` uses `StaticThreshold ... -IsTrendOnly
  $True` ("increasing trend of more than 10 app domains per hour"). The v1/v1.1
  declarative aggregations are `avg, min, max, p90, p95, p99` — **not `trend`**
  (`CLAUDE.md`, ADR 0004). A trend rule must be re-expressed as a static threshold
  on the aggregated value, or dropped.
- **Expression / computed datasources.** `CalculatedIops.xml` uses interactive
  `<QUESTION QUESTIONVARNAME="RAID5Drives">` prompts and computed IOPS expressions
  (`legacy/pal-v2/PAL2/PALWizard/bin/Debug/CalculatedIops.xml:2-3`). There is no
  expression DSL in PAL-X (ADR 0001 / 0002, declarative comparators only), so
  computed-counter analyses do not port without dropping the computation.
- **Additive health scoring.** Legacy aggregates `PRIORITY` weights into a numeric
  score. PAL-X uses tri-state status (critical/warning/healthy), **no numeric score**
  (`CLAUDE.md`, ADR 0001). Drop the priority arithmetic; keep only the
  per-threshold severity.
- **`<INHERITANCE FILEPATH=...>` base-rule reuse.** `ActiveDirectory.xml:92-93`
  inherits `SystemOverview.xml` and `VMWare.xml` so every app file reuses a System
  Overview base. The pack schema has **no `depends`/`inherit`/`extends` key**
  (`grep -iE 'depends|inherit|extends' dotnet/schemas/pal.pack.v1.json` → no match).
  This is the central design question in section 4.
- **`<CHART>` / `<SERIES>` directives.** Legacy charts are described inline per
  analysis. PAL-X renders charts via ScottPlot from the engine output, not from
  pack-declared series — so chart blocks are discarded on port.

### Conventions that constrain every ported rule

From `CLAUDE.md` and ADR 0001/0002: declarative comparators only (no DSL);
snake_case canonical metric IDs with legacy paths in aliases; tri-state severity, no
numeric score; `host_context.total_physical_memory_mb` /
`logical_processor_count` for RAM/CPU-relative thresholds; and when a referenced
`host_context` value is unknown, the engine emits an informational warning and skips
the rule (it does not fail the run — verified by
`GoldenFixtureTests.MemoryPressure_WithoutHostContext_EmitsSkipWarning`,
`dotnet/tests/Pal.Cli.Tests/GoldenFixtureTests.cs:74-86`). Every authored pack must
pass `pal validate-pack --path <dir>` (and the v1.1 version gate in `PackValidator`).

---

## 2. Goal

"Good pack coverage" means a migrating legacy-PAL user can point PAL-X at the same
capture they used to feed legacy PAL for their **deployed, supported** workload and
get equivalent findings — without porting end-of-life products nobody still runs.

Concretely, the target is:

- **Coverage of still-shipping roles.** The first wave covers the highest-value
  modern Windows roles (AD, .NET CLR / ASP.NET); subsequent waves cover Exchange
  2016/2019, Hyper-V, SharePoint, and SQL feature areas not in `sql-host-core`.
- **A bounded cost per pack.** Authoring a new pack should cost:
  - **one `pack.yaml`** copied from the `windows-core` skeleton,
  - **canonical metric IDs** for the workload's counters — added to
    `MetricAliasRegistry.BuildDefault` (in-tree) or the pack's `metric_aliases:`
    block (contributor),
  - **N declarative rules** re-expressed from the legacy `<THRESHOLD>` blocks
    (dropping trend/expression/score constructs per section 1),
  - **a golden fixture** under `fixtures/<workload>/` plus an assertion modelled on
    `GoldenFixtureTests`,
  - **`pal validate-pack` passing** with zero errors.
- **A repeatable playbook** (section 6) a contributor or a future executor plan can
  follow without this spike's context — which is what lets the long tail be filled by
  the community via the already-designed pack registry
  (`docs/architecture/design/shareable-pack-registry.md`).

A rough budget per still-relevant pack: 8–20 declarative rules, 1 fixture, 0–1 C#
alias change. EOL products are explicitly not in the budget (section 8).

---

## 3. Triage — every legacy stem bucketed

The 75 unique stems sort into three buckets: **(a) still-relevant** workload packs
worth porting, **(b) EOL/legacy** workloads (port only on explicit request), and
**(c) non-pack utilities** (not workload packs at all). `<ANALYSIS>` counts below are
from `grep -c '<ANALYSIS ' <file>` in `legacy/pal-v2/PAL2/PALWizard/bin/Debug/`.

### Bucket (a) — still-relevant (port candidates)

| Stem | Workload | ~`<ANALYSIS>` | Notes |
|------|----------|--------------|-------|
| `ActiveDirectory.xml` | AD domain controllers | 11 | NTDS / LSASS / DRA counters; inherits SystemOverview + VMWare |
| `DotNet.xml` | .NET CLR | 4 | CLR exceptions, GC %time, heap, appdomains (some trend-only) |
| `AspDotNet.xml` | ASP.NET | 10 | request exec time, app/worker restarts, request queue |
| `Asp.xml` | classic ASP | — | legacy ASP; lower value, kept for completeness |
| `HyperV30.xml` | Hyper-V (2012+) | 57 | large; logical/virtual processor, VM health, partitions |
| `Lync2013-FrontEnd.xml` | Skype for Business / Lync 2013 FE | 62 | + `Lync2013-Edge`, `Lync2013-Mediation` |
| `Lync2013-Edge.xml` | Lync 2013 Edge | — | SfB role split |
| `Lync2013-Mediation.xml` | Lync 2013 Mediation | — | SfB role split |
| `Exchange2013.xml` | Exchange 2013 | 72 | large; many trend/computed analyses to triage |
| `Exchange2016.xml` | Exchange 2016 | 72 | large; modern, highest Exchange value |
| `SP2013.xml` | SharePoint 2013 | — | modern SharePoint farm |
| `SPS2010.xml` / `SharePoint.xml` | SharePoint 2010 / generic | — | older but still deployed; lower priority |
| `MOSS_Search.xml` / `FS4SP.xml` | SharePoint search / FAST | — | SharePoint search feature areas |
| `SQLServer2014.xml` | SQL Server 2014 | — | extends host-level `sql-host-core` with DB-engine counters |
| `SQLServer2012.xml` / `SQLServer2008R2.xml` / `SQLServer.xml` | SQL Server (older) | — | overlaps `sql-host-core`; mine for gaps |
| `CitrixXenApp.xml` | Citrix XenApp | — | third-party; common in VDI estates |
| `PrintServer.xml` | Print Server | 9 | small, self-contained, cheap port |
| `ProjectServer.xml` | Project Server | 78 | large; niche |
| `DynamicsAX2012AOS.xml` / `DynamicsAX.xml` | Dynamics AX 2012 AOS | — | niche ERP |
| `DynamicsCRM2013_*.xml` (3 files) | Dynamics CRM 2013 (BE/FE/Full) | — | niche; role-split |
| `VMWare.xml` | VMware guest counters | — | used as an `<INHERITANCE>` base by AD |
| `Exchange2010.xml` | Exchange 2010 | — | extended support ended; borderline (a)/(b) |
| `HyperV.xml` | Hyper-V (2008 R2) | — | older Hyper-V; superseded by `HyperV30` |

### Bucket (b) — EOL / legacy (port only on explicit request)

| Stem(s) | Workload | Why deprioritized |
|---------|----------|-------------------|
| `Exchange2003.xml` | Exchange 2003 | end of life |
| `Exchange.xml`, `Exchange2007-*.xml` (12 files: CAS/HUB/MBX/EDGE/UM + TechNet variants) | Exchange 2007 roles | end of life |
| `OCS2007R2-*.xml` (7 files) | Office Communications Server 2007 R2 | end of life (pre-Lync) |
| `Lync2010.xml`, `Lync2010-Edge/FrontEnd/Mediation/Monitoring_Archiving.xml` (5 files) | Lync 2010 | end of life |
| `BizTalkServer2006.xml` | BizTalk Server 2006 | end of life |
| `TMG2010.xml` | Forefront Threat Management Gateway 2010 | discontinued product |
| `UAG2010.xml` | Forefront Unified Access Gateway 2010 | discontinued product |
| `PAL_VMwareView_PCoIP.xml`, `PAL_VMwareView_VDM.xml` | VMware View (old) | superseded by Horizon |
| `WindowsUpdate.xml` | Windows Update agent | 2 analyses; niche/diagnostic, low value |
| `PowerStates.xml` | CPU power states | 1 analysis; niche |
| `ICIPThresholds.xml` | "ICIP" thresholds | 1 analysis; provenance unclear, niche |
| `logrhythm-SLF.xml` | LogRhythm SLF | 1 analysis; third-party SIEM, niche |
| `EMRA.xml` | "EMRA" all-counters set | 167 analyses; appears to be a meta/everything file, not a focused workload — triage before any port |

### Bucket (c) — non-pack utilities (not workload packs)

| Stem | What it is | Disposition |
|------|-----------|-------------|
| `CounterLang.xml` | Localized counter-name lookup table (`<CounterObject>`/`<CounterName>` per language) — `legacy/pal-v2/PAL2/PALWizard/bin/Debug/CounterLang.xml:1-7` | PAL-X has no localization layer; counter normalization is regex in `BuildDefault`. Not a pack. |
| `PALFunctions.xml` | .NET XML-doc for the `PALFunctions` PowerShell assembly (`<doc><assembly><name>PALFunctions`) — `legacy/pal-v2/PAL2/PALWizard/bin/Debug/PALFunctions.xml:1-5` | Build artifact / API doc, not thresholds. Ignore. |
| `PALWizard.xml` | 0 `<ANALYSIS>` blocks — wizard config | Not a workload pack. |
| `Custom.xml` | 0 `<ANALYSIS>` blocks — empty "bring your own" template | The PAL-X analogue is authoring a new pack from the skeleton. Not a port. |
| `CalculatedIops.xml` | Interactive `<QUESTION>` prompts + computed IOPS expressions | Computation has no declarative equivalent (section 1). Not portable as-is. |
| `SystemOverview.xml` / `QuickSystemOverview.xml` | The System Overview base (CPU/mem/disk/net) inherited by app files; 12 / 74 analyses | **Not a workload pack** — this is the shared base. Its content already maps to `windows-core`. It is the subject of the shared-base decision in section 4, not a standalone port. |

This buckets all 75 unique stems (78 files = 75 unique + 2 duplicate build copies).

---

## 4. Design Options — the shared-base-rules question

Legacy PAL used `<INHERITANCE FILEPATH="SystemOverview.xml" />` so every workload file
inherited a common System Overview base (CPU, memory, disk, network). PAL-X has **no
such mechanism** — `grep -iE 'depends|inherit|extends' dotnet/schemas/pal.pack.v1.json`
returns no matches. The question: when we port an app pack that legacy-inherited the
base, how does a PAL-X user still get the base CPU/mem/disk rules alongside the
app-specific ones?

### Option A — Duplicate base rules into every pack

Copy the relevant `windows-core` CPU/mem/disk rules into each new workload pack.

**Pros**
- Zero infrastructure change; each pack is fully self-contained and portable to the
  registry without resolution-order assumptions.
- A pack reviewer sees every rule the pack will fire in one file.

**Cons**
- Drift: a fix to a base CPU threshold must be applied in N packs.
- Duplicate findings: `windows-core` already loads `always: true`, so a duplicated
  `high-cpu-sustained` would fire twice with different `rule_id`s unless renamed —
  confusing in the report.
- Bloats every pack with rules that have nothing to do with the workload.

**Verdict**: rejected — the duplicate-finding hazard alone makes this a poor fit given
`windows-core` already auto-loads.

### Option B — Ship the base as `windows-core` (already `always: true`) and rely on co-resolution (recommended)

PAL-X already has what legacy approximated with inheritance: `windows-core` declares
`applicability: { always: true }` (`packs/thresholds/windows-core/pack.yaml:7-8`), so
it loads alongside **every** app pack automatically. A ported app pack therefore omits
base CPU/mem/disk rules entirely and contributes only its workload-specific rules; the
base comes "for free" from `windows-core` in the same run. `legacy/SystemOverview.xml`
and `QuickSystemOverview.xml` map onto the existing `windows-core` content, so no new
base pack is even required for the common case.

**Pros**
- **No schema change, no new resolution behavior** — `windows-core`'s `always: true`
  already provides exactly the "every run gets the base" semantics inheritance gave.
- No duplication, no drift, no double findings.
- App packs stay small and workload-focused, which keeps the per-pack cost (section 2)
  low and makes contributor packs easy to review.

**Cons**
- The base coverage is only what `windows-core` currently has (14 rules). If a legacy
  app file inherited a System Overview rule that `windows-core` lacks (e.g. a
  network-interface or paging-file threshold), that gap must be closed by **adding the
  rule to `windows-core`**, not the app pack — a small, deliberate change with its own
  review, but it touches a shared pack.
- The coupling ("base always loads") is implicit in `windows-core`'s applicability
  rather than declared by each app pack. A contributor must know this convention; the
  playbook documents it.

**Verdict**: recommended. It is the lowest-risk path precisely because `windows-core`
already auto-loads always — Option B is less "a new design" than "name and document the
behavior we already ship." Any base-rule gap is closed by enriching `windows-core`.

### Option C — Add a `depends:` / `extends:` key to the pack schema

Introduce a schema key so a pack can declare `depends: [windows-core]` (or
`extends:`), and have the resolver load and merge declared dependencies.

**Pros**
- Explicit, self-describing dependency graph; a pack states its base in its own file.
- Generalizes beyond a single hard-wired base (e.g. an Exchange pack could depend on
  both `windows-core` and a future `dotnet-clr` pack).

**Cons**
- **Schema change** → its own ADR, validator support in `PackValidator`, resolver
  changes in `PackResolver`, and migration/versioning of the schema. This is the
  `dotnet/src/**` + `dotnet/schemas/**` change this spike is explicitly forbidden from
  making.
- Merge semantics (override vs. union of rules, recommendation-ID collisions, alias
  precedence) are non-trivial and interact with the registry's per-pack versioning
  (`docs/architecture/design/shareable-pack-registry.md`).
- Risk of re-introducing the very complexity ADR 0001/0002 removed when they deleted
  the legacy inheritance + expression model.

**Verdict**: deferred. Worth revisiting only if the first wave proves Option B's
"enrich `windows-core`" approach is insufficient (e.g. workloads that need a *different*
base, like VMware-guest vs. physical). **Needs maintainer decision** if a second,
non-`windows-core` base ever becomes necessary; until then Option C is over-engineering.

**Recommendation**: Option B for the first wave and the foreseeable roadmap. Document
the "`windows-core` is the always-loaded base; app packs add only workload rules;
base gaps are fixed in `windows-core`" convention in the playbook. Reserve Option C
(with its own ADR) for the first concrete case that Option B cannot express.

---

## 5. Prioritized Port Roadmap

Candidates ranked by **install base × diagnostic value × low porting cost** (high =
common, still-supported, small counter surface, no host-context complications).

| Rank | Proposed pack | Source legacy XML | Est. rules | New canonical IDs? | host_context? |
|------|---------------|-------------------|-----------|--------------------|---------------|
| 1 | `dotnet-clr` | `DotNet.xml` (+ `AspDotNet.xml`) | ~10–14 | yes — `dotnetclr.*`, `aspnet.*` (some `aspnet.` already in `BuildDefault`) | no |
| 2 | `active-directory` | `ActiveDirectory.xml` | ~8–11 | yes — `ntds.*`, plus `process(lsass).*` | no |
| 3 | `hyper-v` | `HyperV30.xml` | ~20–30 (triage from 57) | yes — `hyperv.*` (hypervisor/VP/partition) | maybe (logical-proc-relative) |
| 4 | `sharepoint-2013` | `SP2013.xml` (+ `MOSS_Search.xml`) | ~15–25 | yes — `sharepoint.*` | no |
| 5 | `exchange-2016` | `Exchange2016.xml` | ~25–40 (triage from 72) | yes — many `msexchange*.*`; heavy trend/computed triage | maybe |
| 6 | `print-server` | `PrintServer.xml` | ~9 | yes — `printqueue.*` | no — small, good "second contributor" pack |
| 7 | `sql-engine-2014` | `SQLServer2014.xml` | gap-fill vs `sql-host-core` | partial — extend `sql.*` | no |

(≥6 candidates, as required; ranks 8+ — Citrix XenApp, Dynamics AX/CRM, Project Server,
SfB/Lync 2013 roles — follow as demand warrants.)

### First wave (recommended): `dotnet-clr` and `active-directory`

Both are common, still-shipping, structurally small (4–11 legacy analyses), and have
**no host-context complications** — the cleanest possible first ports, and the best
proving ground for the playbook.

**Pack 1 — `dotnet-clr`** (from `DotNet.xml`, optionally folding in `AspDotNet.xml`)
- **Source**: `legacy/pal-v2/PAL2/PALWizard/bin/Debug/DotNet.xml` (4 analyses) and
  `legacy/pal-v2/PAL2/PALWizard/bin/Debug/AspDotNet.xml` (10 analyses).
- **Rules (est. ~10–14)**: CLR exceptions/sec (warn >10, crit >50 — direct port of
  `DotNet.xml:4-14`), % Time in GC (warn >10), heap-bytes growth (re-express the
  trend-only rule as a static threshold or drop), ASP.NET request execution time,
  application/worker restarts, requests-in-queue, request wait time.
- **New canonical IDs**: `dotnetclr.*` (exceptions, gc, heap, appdomains). Several
  `aspnet.*` IDs already exist in `BuildDefault` (used by `iis-core`); reuse those and
  add only the missing CLR-side IDs.
- **host_context**: none.
- **Trend/expression to drop or re-express**: the `-IsTrendOnly $True` heap and
  appdomain rules (`DotNet.xml:54-77`).
- **applicability**: `requires_any: [dotnetclr.percent_time_in_gc,
  dotnetclr.exceptions_thrown_per_sec, ...]` so it auto-loads only on .NET captures.

**Pack 2 — `active-directory`** (from `ActiveDirectory.xml`)
- **Source**: `legacy/pal-v2/PAL2/PALWizard/bin/Debug/ActiveDirectory.xml` (11
  analyses).
- **Rules (est. ~8–11)**: NTDS LDAP Bind Time (warn >15ms, crit >20ms — direct port of
  `ActiveDirectory.xml:6-17`), LSASS `% Processor Time` and `Working Set`, NTDS
  Kerberos/NTLM authentications, DRA replication counters.
- **New canonical IDs**: `ntds.*` (LDAP bind time, authentications, DRA); LSASS reuses
  the existing `process.percent_processor_time` / `process.working_set` IDs filtered to
  `instance: "lsass"`.
- **host_context**: none.
- **Drop**: the `<INHERITANCE FILEPATH="SystemOverview.xml" />` and `VMWare.xml`
  references (`ActiveDirectory.xml:92-93`) — base CPU/mem/disk comes from
  `windows-core` per Option B.
- **applicability**: `requires_any: [ntds.ldap_bind_time, ...]`.

Each first-wave pack becomes one implementation plan that cites the playbook below.

---

## 6. Pack-Authoring Playbook

A self-contained, numbered procedure to port a legacy threshold file into a PAL-X pack.
A contributor (or a future executor plan) can follow this without reading the rest of
this spike. The running example is porting `DotNet.xml` into a `dotnet-clr` pack.

1. **Create the pack directory and copy the skeleton.** Make
   `packs/thresholds/<pack-id>/pack.yaml`. Copy the header and structure from the
   exemplar `packs/thresholds/windows-core/pack.yaml`: set `schema_version:
   "pal.pack/v1"` (use `"pal.pack/v1.1"` only if you need a rolling `window:`),
   `pack_id` (kebab-case, e.g. `dotnet-clr`), `pack_name`, `version: "1.0.0"`, and a
   one-line `description`.

2. **Open the legacy XML and enumerate its `<ANALYSIS>` blocks.** For
   `legacy/pal-v2/PAL2/PALWizard/bin/Debug/DotNet.xml`, each `<ANALYSIS>` is a
   candidate rule. Record, for each: the `<DATASOURCE EXPRESSIONPATH>` (raw counter
   path), the `CATEGORY`, and every `<THRESHOLD>` (its `CONDITION` = severity and the
   `-Operator`/`-Threshold` from the `<CODE>` block).

3. **Triage out what does not map** (per section 1). Drop or flag any analysis whose
   `<CODE>` uses `-IsTrendOnly $True` (no `trend` aggregation), computed/expression
   datasources (no DSL), or relies on additive `PRIORITY` scoring (no numeric score).
   Re-express a trend rule as a static `avg`/`max` threshold if a sensible one exists;
   otherwise drop it and note the omission.

4. **Choose canonical metric IDs** (snake_case, dot-delimited, lowercase segments,
   e.g. `dotnetclr.percent_time_in_gc`). For each kept counter:
   - First check whether the path already resolves — `MetricAliasRegistry.BuildDefault`
     (`dotnet/src/Pal.Engine/Normalization/MetricAliasRegistry.cs:10`) has 63 entries
     across `processor./memory./physicaldisk./network./process./sql./iis./aspnet.`
     etc. Reuse an existing ID where the counter already maps (e.g. LSASS reuses
     `process.percent_processor_time`).
   - For a genuinely new counter, register it. **In-tree shipped packs**: add a
     `reg.Add(@"<regex>", "<canonical.id>")` line to `BuildDefault` (a C# change — out
     of scope for a pure pack plan, so the per-pack implementation plan must include
     it). **Contributor / out-of-tree packs**: declare a `metric_aliases:` block in the
     pack (schema `dotnet/schemas/pal.pack.v1.json:35-43`); `AddFromPack`
     (`MetricAliasRegistry.cs:109`) loads it. Note the ordering caveat from section 1:
     pack aliases are loaded after collection, so `BuildDefault` is preferred for
     shipping packs.

5. **Write each rule declaratively.** Under `rules:`, give each a kebab-case `rule_id`,
   a `severity` (from the legacy `CONDITION`), a `category`, a `title`, a one-line
   `summary`, and a prose `explanation` (port the legacy `<DESCRIPTION>`, stripping its
   HTML). Express the threshold as a `conditions:` list:
   ```yaml
   conditions:
     - metric: dotnetclr.exceptions_thrown_per_sec
       instance: "*"            # legacy (*) wildcard; omit to match all
       aggregation: avg          # avg|min|max|p90|p95|p99 (no trend)
       operator: gt              # from StaticThreshold -Operator
       threshold: 10             # from -Threshold, or a host_context object
       duration_percent: 20      # how much of the window must satisfy it
   ```
   For RAM/CPU-relative thresholds use a `host_context` object
   (`{host_context: total_physical_memory_mb, factor: 0.10, minimum: 128}` —
   `packs/thresholds/windows-core/pack.yaml:147-155`). When two legacy
   `<THRESHOLD>`s exist for one counter (Warning + Critical), emit **two** rules
   (one per severity), as `windows-core` does for CPU and disk.

6. **Wire recommendations.** Port the "Next Steps" prose from the legacy
   `<DESCRIPTION>` into `recommendations:` entries (`{priority, text, rationale,
   links}`) keyed by ID, and reference them from each rule's `recommendations:` list —
   modelled on `packs/thresholds/windows-core/pack.yaml:10-66`.

7. **Set `applicability` for auto-resolution.** Use
   `requires_any: [<one or more canonical metric IDs the pack needs>]` so the CLI loads
   the pack only when those counters are present (mirror
   `packs/thresholds/iis-core/pack.yaml:7-12`). Do **not** use `always: true` for a
   workload pack — that is reserved for the `windows-core` base. **Do not re-declare
   base CPU/mem/disk rules**: they come from the always-loaded `windows-core` (section
   4, Option B); if a base rule you need is missing, add it to `windows-core` instead.

8. **Validate.** Run
   `dotnet run --project dotnet/src/Pal.Cli -- validate-pack --path
   packs/thresholds/<pack-id>` and confirm `Status: valid` with zero errors (the gate
   reports rule count and warnings; `windows-core` reports `Rules: 14 / Status:
   valid`). Fix any schema or version-gate failure `PackValidator` reports before
   proceeding.

9. **Add a golden-fixture test.** Create `fixtures/<workload>/input.csv` — a small
   capture containing the counters your rules target (and, if a rule uses
   `host_context`, a `host-context.json` sidecar, as `fixtures/memory-pressure/` does).
   Then add a test modelled on `dotnet/tests/Pal.Cli.Tests/GoldenFixtureTests.cs`: it
   collects the CSV with `CsvCollector`, resolves packs via `PackResolver`, runs
   `RuleEngine`, and asserts the expected findings fire (see
   `GoldenFixtureTests.CpuPressure_FindsHighCpuSustained`,
   `GoldenFixtureTests.cs:19-30`). For byte-stable golden JSON, write a
   `golden.pal-report.json` and assert against it via the `AssertMatchesGolden` /
   `MaskEngineFields` pattern (`GoldenFixtureTests.cs:212-271`), which masks
   machine-specific fields (`report_id`, `dataset_id`, engine version/OS). Pin
   `GeneratedAt` to a fixed `DateTimeOffset` (the tests use `2026-01-01T00:00:00Z`) so
   output is deterministic.

10. **Build and run the suite.** `dotnet build dotnet/Pal.sln -c Release` then
    `dotnet test dotnet/Pal.sln -c Release --filter
    "FullyQualifiedName!~Pal.Api.Tests"`. The new pack must validate, the fixture test
    must pass, and no existing golden fixture may regress (an unintended overlap with
    `windows-core` rules would change other reports).

---

## 7. Open Questions

### 7a. Which shared-base mechanism — Option B convention, or Option C schema key?

**Current evidence**: `windows-core` already loads `always: true`
(`packs/thresholds/windows-core/pack.yaml:7-8`); the schema has no
`depends`/`inherit`/`extends` key. Option B (section 4) needs no code change; Option C
needs a schema + resolver + validator change and its own ADR.

**Proposed answer**: adopt Option B for the first wave; revisit Option C only if a
workload needs a base other than `windows-core`.

**Needs maintainer decision** — this is the doc's central design choice and the input
to whether a `000N-shared-base-pack` ADR is written.

### 7b. New canonical IDs — `BuildDefault` (C#) or pack `metric_aliases:` (YAML)?

**Current evidence**: all 3 shipped packs rely on `BuildDefault`'s 63 entries; none use
`metric_aliases:`. `AddFromPack` exists (`MetricAliasRegistry.cs:109`) but is unused by
shipped packs, and collection runs before pack aliases load (section 1).

**Proposed answer**: in-tree shipped packs add canonical regexes to `BuildDefault`;
contributor/out-of-tree packs use `metric_aliases:`. Each first-wave plan therefore
includes a small `BuildDefault` change.

**Needs maintainer decision** if the project prefers to keep `BuildDefault` frozen and
push *all* new aliases into pack YAML (which would require moving alias-loading ahead of
collection in `AnalysisRunner` — itself an ADR).

### 7c. How does pack versioning/aliasing interact with the pack registry?

**Current evidence**: the registry design
(`docs/architecture/design/shareable-pack-registry.md`) owns pack distribution and
per-pack versioning; this spike only authors packs.

**Proposed answer**: keep `pack_id` stable and bump `version` per pack; let the
registry handle distribution. A first-wave pack that adds `BuildDefault` aliases couples
to an engine version — the registry must record an engine-version floor for such packs.

**Needs maintainer decision** — owned by the registry doc; flagged here so the two
designs stay consistent.

### 7d. Are community-contributed packs in scope?

**Current evidence**: the playbook (section 6) is deliberately self-contained, and the
registry design anticipates third-party distribution. But contributor packs cannot
touch `BuildDefault` (C#), so they are limited to counters that either already resolve
or that they can express via `metric_aliases:`.

**Proposed answer**: yes, as a goal — but contributor packs must be authorable using
only `pack.yaml` + `metric_aliases:` (no C# change). Curated/first-party packs may add
`BuildDefault` entries.

**Needs maintainer decision** on whether the first wave should also seed a contributor
contribution guide alongside the registry.

### 7e. Counter-recommendation metadata — forward-link to plan 012

**Current evidence**: plan `012-counter-collection-recommender.md` ("counter-collection
'what to capture' recommender + logman template export") will recommend which counters
to capture per workload. Each pack here declares (via `applicability` + rule `metric`s)
exactly which counters it needs.

**Proposed answer**: when authoring a pack, record its required counter set so plan
012's recommender / logman templates can be generated from the same source of truth.
This is a metadata coupling, not a blocker for the first wave.

**Needs maintainer decision** on whether pack `applicability`/rule metrics should be the
canonical input to the plan-012 recommender, or a separate manifest.

---

## 8. Non-Goals

The following are explicitly out of scope for the pack-coverage work and must not be
folded in without a separate ADR or plan:

- **Porting EOL products by default.** Exchange 2003/2007, OCS 2007 R2, Lync 2010,
  BizTalk 2006, TMG/UAG 2010, and old VMware View (bucket b, section 3) are ported
  **only on explicit request**, not as part of the roadmap.

- **Adding a numeric health score.** Legacy PAL's additive `PRIORITY` scoring stays
  dropped; PAL-X remains tri-state (critical/warning/healthy) per ADR 0001.

- **Introducing an expression DSL.** Computed/calculated analyses (e.g.
  `CalculatedIops.xml`) do not motivate an expression parser. Declarative comparators
  only (ADR 0001/0002). Counters that require computation are dropped, not ported via a
  new DSL.

- **A `trend` aggregation.** Legacy `-IsTrendOnly` rules are re-expressed as static
  thresholds on `avg`/`max`/percentile or dropped. Adding `trend` to the schema is a
  separate ADR (ADR 0004 explicitly excluded it).

- **A `depends:`/`extends:` schema key (Option C).** Deferred to its own ADR if and
  when a non-`windows-core` base is actually needed (section 4).

- **Authoring the actual pack YAML in this spike.** This document only *plans* the
  packs and provides the playbook. Each first-wave pack is its own implementation plan.

- **Pack distribution.** Owned by
  `docs/architecture/design/shareable-pack-registry.md`; not re-decided here.
