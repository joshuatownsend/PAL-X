# Counter-Collection Recommender Design

> **Status**: spike / pre-ADR  
> **Author**: design spike (plan 012), 2026-06-13  
> **Next step**: maintainer acceptance promotes the schema decision (Section 5) and the
> CLI surface (Section 7) to an ADR under
> `docs/architecture/adr/000N-counter-collection-recommender.md`

---

## 1. Current State

PAL-X analyzes a capture that **already exists**. Nothing in the tool tells an operator
*which counters to capture* before they run PerfMon or `logman`. This is the inverse of
everything the codebase does today, and the gap is the onboarding cliff: a first-time user
guesses at counters, captures an incomplete log, and gets a thin report.

### No "recommended counters" concept exists in the schema

The authoritative pack schema `dotnet/schemas/pal.pack.v1.json` has no
`recommended_counters`, `required_counters`, `capture`, or `logman`/template key. A grep
for `recommend|require.?counter|capture|logman|template` matches only the
**remediation-advice** keys — `recommendations` (pack-level property, line 44) and the
`RecommendationDef` definition (line 188), plus the `recommendations` array each rule
references by ID (line 250). These are *human remediation text* ("Add physical memory to
this server"), **not** a list of counters to capture. The two must not be conflated. The
top-level schema is closed: `required` is `[schema_version, pack_id, pack_name, version,
rules]` (line 7) and `additionalProperties: false` (line 8), so any new top-level block is
a deliberate, ADR-worthy schema change (see Section 5).

### Packs already encode the counters they evaluate

Every rule condition names a canonical snake_case metric ID in `condition.metric`. In
`packs/thresholds/windows-core/pack.yaml`:

```yaml
conditions:
  - metric: processor.percent_processor_time   # pack.yaml:83
    instance: "_Total"                          # pack.yaml:84
    aggregation: avg
    operator: gt
    threshold: 80
    duration_percent: 20
```

The full set of metrics a pack evaluates is exactly the union of every rule's
`condition.metric`. For `windows-core` that is 14 distinct canonical IDs across CPU,
memory, disk, and system categories; for `iis-core` it is 5 (`iis.*`, `aspnet.*`); for
`sql-host-core` it is 5 (`sql.page_life_expectancy`, `sql.buffer_cache_hit_ratio`,
`sql.lazy_writes_per_sec`, `sql.deadlocks_per_sec`, `sql.memory_grants_pending`). **The
raw material for "what to capture" is already in every pack** — it is the rules' metric
list.

### The canonical-ID → PerfMon-path table lives in source, not in the packs

This is the single most important correction to the plan's premise. The plan's "Current
state" says `metric_aliases` in each pack maps canonical IDs to legacy PerfMon paths.
**The shipped packs contain no `metric_aliases` block** — `grep -rln metric_aliases
packs/` returns nothing. The schema *permits* an optional pack-level `metric_aliases`
object (`pal.pack.v1.json:35`) and the loader supports it via
`MetricAliasRegistry.AddFromPack` (`MetricAliasRegistry.cs:109`), but no pack uses it.
The actual mapping is hardcoded in
`dotnet/src/Pal.Engine/Normalization/MetricAliasRegistry.cs`, in
`BuildDefault()` (lines 10–102):

```csharp
// MetricAliasRegistry.cs:15
reg.Add(@"\\[^\\]+\\Processor\([^)]*\)\\% Processor Time", "processor.percent_processor_time");
// MetricAliasRegistry.cs:24
reg.Add(@"\\[^\\]+\\Memory\\Available MBytes", "memory.available_mbytes");
// MetricAliasRegistry.cs:34
reg.Add(@"\\[^\\]+\\PhysicalDisk\([^)]*\)\\Avg\. Disk sec/Read", "physicaldisk.avg_disk_sec_per_read");
```

Two consequences for the recommender:

1. **The reverse-lookup table is centralized and complete.** Every canonical ID a pack
   can name has exactly one `BuildDefault` entry. That makes the recommender's core
   transform — canonical ID → counter path — a single deterministic dictionary lookup, not
   a scatter-gather across pack files.
2. **The entries are forward regexes, not literal paths.** The host segment is
   `\\[^\\]+\\` (matches any machine name) and the instance is `\([^)]*\)` (matches any
   instance). A regex cannot be emitted into a `logman` template verbatim — the recommender
   needs a **canonical display path** per metric (a literal `\Object(instance)\Counter`
   form). That display path does not exist anywhere today; producing it is the one piece of
   new derivation work (Section 4).

### Auto-resolution is the mirror image of the recommender

The CLI auto-loads `windows-core` always, `iis-core` when IIS counters are present, and
`sql-host-core` when SQL counters are present (`README.md:63`, `README.md:243-244`). This
is `applicability.requires_any` on the pack (e.g. `iis-core/pack.yaml:7-12`) asking *which
pack fits these counters?* The recommender asks the inverse: *which counters does this pack
want?* The two share the same metric vocabulary, so a well-designed recommender should emit
precisely the counters whose presence would trigger auto-resolution of the same pack
(Section 9, Q3).

### CLI surface today

`pal` exposes `analyze`, `validate-pack`, `inspect-dataset`, `list-packs`, and a `packs`
branch containing only `sign` (`dotnet/src/Pal.Cli/Program.cs:12-29`). There is no
counter-recommendation or capture-template command. A new `pal packs counters <pack-id>`
slots directly into the existing `config.AddBranch("packs", …)` block beside
`packs.AddCommand<SignPackCommand>("sign")` (`Program.cs:24-28`).

### Legacy did exactly this — and it is the strongest precedent

The legacy PAL v2 Wizard shipped this feature. `frmPALExecutionWizard.vb:1076-1090`
exports a threshold file to a capture template in three modes
(`WINSEVEN`, `WINSIX`, `LOGMAN`), dispatching to three functions in
`legacy/pal-v2/PAL2/PALFunctions/PALFunctions.vb`:

| Legacy function | Line | Output |
|-----------------|------|--------|
| `ExportThresholdFileToPerfmonTemplate` | `PALFunctions.vb:83` | Windows 7+ PerfMon `.xml` Data Collector Set template |
| `ExportThresholdFileToDataCollectorTemplate` | `PALFunctions.vb:436` | Older Data Collector template |
| `ExportThresholdFileToLogmanCounterListFile` | `PALFunctions.vb:284` | newline-delimited counter list for `logman … -cf <file>` |

The logman exporter (`PALFunctions.vb:284-434`) is the algorithm we are porting:

- It walks every `//DATASOURCE[@TYPE='CounterLog']` node and reads its `EXPRESSIONPATH`
  (e.g. `\.NET CLR Exceptions(*)\# of Exceps Thrown / sec`, from
  `legacy/.../bin/Debug/DotNet.xml:3`) — the legacy equivalent of a rule's
  `condition.metric` after path resolution.
- It **dedups by collapsing the instance to `*`** (`PALFunctions.vb:346-364`): two
  datasources on the same object+counter but different instances merge into one
  `\Object(*)\Counter` line.
- It special-cases **SQL named instances**, prompting for instance names and expanding
  `SQLServer:` objects to `MSSQL$<INSTANCE>:` (`PALFunctions.vb:377-423`) — directly
  relevant to our `sql.*` metrics, whose `BuildDefault` patterns already match both
  `SQLServer` and `MSSQL$…` (`MetricAliasRegistry.cs:63`).
- It **sorts** the list and emits one counter path per line (`PALFunctions.vb:425-433`).

The localization table is also present: `CounterLang.xml`
(`legacy/.../bin/Debug/CounterLang.xml`) holds per-locale counter names — each
`<CounterName>` carries `enUS`, `deDE`, `frFR`, `jaJP`, … attributes. This grounds the
localization open question (Section 9, Q5) rather than leaving it speculative.

---

## 2. Goal

Given a target workload — a pack ID, or the set of packs the operator intends to analyze
against — emit:

1. A **human-readable counter list** (canonical IDs + their PerfMon paths + which rules
   each one feeds), so the operator understands *why* each counter is recommended.
2. A ready-to-run **`logman create counter`** command (or counter-list file), so the
   operator captures the right data on the first try.
3. Optionally a PerfMon **`.xml` Data Collector Set** template and a `relog` hint for
   converting existing BLG captures.

The bar for "right data the first time": the emitted counter set, once captured and fed
back to `pal analyze`, must satisfy every rule's `condition.metric` (and `applies_when`
gate) in the target pack, so no rule is skipped for a missing counter. The recommender is
the onboarding inverse of analysis and the natural companion to pack-coverage expansion
(plan 008): every new pack ships its recommended counters from day one.

---

## 3. Goal vs. Non-Goal boundary (what the recommender produces)

The recommender is a **pure, offline transform** over pack metadata plus the
`MetricAliasRegistry` table. It reads packs and the alias registry; it never touches a live
machine, never runs `logman`, and never reads a capture. Its only output is text (a counter
list and one or more template files). This keeps it testable with golden fixtures the same
way report writers are (`--now`-style determinism is trivial — the output is a pure function
of the pack set). Everything beyond text generation is a Non-Goal (Section 10).

---

## 4. Derivable vs. Explicit Metadata

The central design question the plan poses: how much of a recommended counter set can be
**derived** from existing pack content, and how much needs **new explicit metadata**?

### What is derivable today (the large majority)

For each metric ID `M` named by any rule in the target pack:

1. **Collect** `M` from the union of `rule.conditions[].metric` (and any
   `applies_when.requires_all/requires_any` IDs, which name the same vocabulary).
2. **Reverse-resolve** `M` to a PerfMon counter path. There is exactly one `BuildDefault`
   entry per canonical ID, so this is a one-to-one lookup against the regex table in
   `MetricAliasRegistry.cs:10-102`.
3. **Materialize a display path** from that regex by substituting the wildcard segments:
   `\\[^\\]+\\` → `\` (drop the local-machine host prefix, as PerfMon paths are
   host-relative when captured locally) and `\([^)]*\)` → `(*)` (capture all instances).
   So `processor.percent_processor_time` →
   `\Processor(*)\% Processor Time`, `memory.available_mbytes` →
   `\Memory\Available MBytes`, `physicaldisk.avg_disk_sec_per_read` →
   `\PhysicalDisk(*)\Avg. Disk sec/Read`.

Step 3 is the only genuinely new logic, and it is small: the regex grammar in
`BuildDefault` is uniform (`\\[^\\]+\\` host, optional `\([^)]*\)` instance, literal
counter name with the `\.` / `/sec` escapes un-escaped). A de-regex helper of ~20 lines
covers every shipped entry. **All 14 windows-core metrics, all 5 iis-core metrics, and all
5 sql-host-core metrics derive cleanly this way** — roughly 100% of the *currently shipped*
recommended set is derivable with zero new pack metadata.

### What is NOT derivable and needs an authoring decision

Three classes of information are absent from the rules and cannot be invented:

1. **Context counters no rule evaluates.** The legacy PAL packs captured counters purely
   for *correlation* — e.g. per-process `\Process(*)\% Processor Time` so an operator can
   attribute total CPU to a process — even though no threshold fires on them. PAL-X's own
   remediation text already gestures at this: `windows-core/pack.yaml:13` advises
   "Identify which process is consuming CPU using Process\% Processor Time counters", but
   **no rule captures `process.percent_processor_time`** (the alias exists at
   `MetricAliasRegistry.cs:56`, but no windows-core rule names it). A purely derived set
   would omit the very counter the remediation tells the operator to look at.
2. **Instance scoping.** Rules pin specific instances (`instance: "_Total"`,
   `windows-core/pack.yaml:84`) for *evaluation*, but *capture* almost always wants the
   wildcard `(*)` so the per-instance breakdown is available for correlation. The mapping
   "evaluate `_Total`, capture `(*)`" is a sensible default but is a policy choice, not a
   fact in the pack.
3. **Sample interval.** Nothing in any pack states how often to sample. `logman` needs a
   `-si` value; the right interval is workload-dependent (15s is a reasonable Windows-OS
   default; high-rate counters like context switches may want finer resolution).

### Recommendation: derive the baseline, add an optional `capture:` block for the extras

Derive the baseline counter set automatically from rule metrics + the alias table (no
schema change, works for every pack that exists today), and add a **single optional**
pack-level `capture:` block for the three non-derivable extras. Packs that omit it still
get a correct derived set; packs that include it enrich the recommendation. This is the
back-compatible answer and is detailed in Section 5.

---

## 5. Schema Addition (optional, back-compatible) — **ADR-worthy**

> **Needs maintainer decision.** A `capture:` block touches both
> `dotnet/schemas/pal.pack.v1.json` and `PackValidator`. Per project convention any schema
> change is ADR-worthy. This section proposes the shape; ratifying it is the ADR.

Because the top-level schema is `additionalProperties: false` (`pal.pack.v1.json:8`),
adding `capture` is a real (if additive) schema edit — the validator rejects unknown
top-level keys today, so existing packs would *not* tolerate a stray `capture` block until
the schema declares it. The proposed block is **optional** (not added to `required`), so
every shipped pack remains valid unchanged:

```yaml
# optional; absent from all current packs — recommender falls back to pure derivation
capture:
  interval_seconds: 15           # logman -si default for this pack
  extra_counters:                # context counters no rule evaluates
    - "\\Process(*)\\% Processor Time"
    - "\\Process(*)\\Private Bytes"
  instance_overrides:            # capture wider than the rule evaluates
    processor.percent_processor_time: "*"   # rule pins _Total; capture all
```

Schema sketch (added under `properties`, mirroring the existing optional `metric_aliases`
object at `pal.pack.v1.json:35`):

```jsonc
"capture": {
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "interval_seconds": { "type": "integer", "minimum": 1 },
    "extra_counters":   { "type": "array", "items": { "type": "string" } },
    "instance_overrides": {
      "type": "object",
      "additionalProperties": { "type": "string" }
    }
  }
}
```

Design constraints for the ADR:

- **Strictly optional.** Not added to the top-level `required` array
  (`pal.pack.v1.json:7`). Absence ⇒ pure derivation with a tool-level default interval.
- **`PackValidator` parity.** The validator that enforces `additionalProperties: false`
  must learn the new key, or it will reject any pack that adopts it. This is the
  implementation seam the ADR scopes; it is **out of scope for this spike** (touching
  `dotnet/src` would violate the plan's STOP condition).
- **Schema version.** The `capture` block is additive and could ship under `pal.pack/v1`
  without a version bump (it does not change rule evaluation), but the ADR may prefer to
  gate it behind a new `pal.pack/v1.2` enum value (`pal.pack.v1.json:12`) for clarity,
  consistent with how the `window:` block was gated under `v1.1` (ADR 0004).

**Alternative considered — derive everything, no schema change.** Viable for the three
shipped packs (Section 4 shows 100% derivability of the evaluated set). It is rejected only
because it cannot express the *correlation* counters the remediation text already assumes,
nor a per-pack interval. If the maintainer judges those extras unnecessary for Phase 1, the
zero-schema-change path is fully acceptable and the recommender ships as a pure CLI feature
with no pack edits at all.

---

## 6. Export Formats (the `logman` template)

The recommender emits the following for a given pack (or merged pack set). All PerfMon
paths come from the materialized display paths of Section 4 — **real counter paths derived
from `MetricAliasRegistry.BuildDefault`, never canonical snake_case IDs**.

### 6a. Human-readable counter list (default output)

```
Pack: windows-core (Windows Core 1.0.0)
Recommended counters (14), sample interval 15s:

  \Processor(*)\% Processor Time          → processor.percent_processor_time
                                            rules: high-cpu-sustained, high-cpu-critical
  \Processor(*)\% Privileged Time         → processor.percent_privileged_time
                                            rules: high-privileged-time
  \Memory\Available MBytes                → memory.available_mbytes
                                            rules: low-available-memory, critical-low-available-memory
  \Memory\% Committed Bytes In Use        → memory.percent_committed_bytes_in_use
  \Memory\Pages/sec                       → memory.pages_per_sec
  \PhysicalDisk(*)\Avg. Disk sec/Read     → physicaldisk.avg_disk_sec_per_read
  \PhysicalDisk(*)\Avg. Disk sec/Write    → physicaldisk.avg_disk_sec_per_write
  \PhysicalDisk(*)\Current Disk Queue Length → physicaldisk.current_disk_queue_length
  \PhysicalDisk(*)\% Idle Time            → physicaldisk.percent_idle_time
  \System\Context Switches/sec            → system.context_switches_per_sec
  \System\Processor Queue Length          → system.processor_queue_length
  …
```

The metric→rule attribution is recovered by inverting the rule list (each
`condition.metric` back to its `rule_id`), so the operator sees the cost/benefit of every
counter.

### 6b. `logman create counter` command (the primary ask)

Two equivalent forms, both built from the same derived paths. Inline `-c`:

```bat
logman create counter PAL_windows-core ^
  -c "\Processor(*)\% Processor Time" ^
     "\Processor(*)\% Privileged Time" ^
     "\Memory\Available MBytes" ^
     "\Memory\% Committed Bytes In Use" ^
     "\Memory\Pages/sec" ^
     "\PhysicalDisk(*)\Avg. Disk sec/Read" ^
     "\PhysicalDisk(*)\Avg. Disk sec/Write" ^
     "\PhysicalDisk(*)\Current Disk Queue Length" ^
     "\PhysicalDisk(*)\% Idle Time" ^
     "\System\Context Switches/sec" ^
     "\System\Processor Queue Length" ^
  -si 00:00:15 -f bin -o "%SystemDrive%\PerfLogs\PAL\windows-core" -v mmddhhmm
```

Counter-list-file form (mirrors the legacy
`ExportThresholdFileToLogmanCounterListFile` output, `PALFunctions.vb:425-433`) — one path
per line in `windows-core.counters.txt`, consumed with `-cf`:

```bat
logman create counter PAL_windows-core -cf windows-core.counters.txt ^
  -si 00:00:15 -f bin -o "%SystemDrive%\PerfLogs\PAL\windows-core"
```

`logman start PAL_windows-core` / `logman stop PAL_windows-core` bracket the capture. The
`-f bin` produces a `.blg` (analyzable directly via `BlgCollector` on Windows); `-f csv`
produces a `.csv` for the cross-platform `CsvCollector`. Both formats are first-class
PAL-X inputs (`CLAUDE.md` BLG ingestion / CSV collector notes).

For `iis-core`, the derived paths come straight from the `iis.*`/`aspnet.*` entries
(`MetricAliasRegistry.cs:89-99`):

```bat
logman create counter PAL_iis-core ^
  -c "\APP_POOL_WAS(*)\Recent Worker Process Failures" ^
     "\ASP.NET\Requests Rejected" ^
     "\ASP.NET\Request Wait Time" ^
     "\ASP.NET\Application Restarts" ^
     "\ASP.NET Applications(*)\Requests In Application Queue" ^
  -si 00:00:15 -f bin -o "%SystemDrive%\PerfLogs\PAL\iis-core"
```

### 6c. PerfMon Data Collector Set `.xml` (secondary)

The legacy `ExportThresholdFileToPerfmonTemplate` (`PALFunctions.vb:83`) emitted a
`<DataCollectorSet>` XML importable via **PerfMon → User Defined → Import** or
`logman import`. PAL-X can emit the same `<PerformanceCounterDataCollector>` shape with one
`<Counter>` element per derived path and the interval as `<SampleInterval>`. Lower priority
than the `logman` command (which most operators script), but valued by GUI-driven admins —
keep it behind a `--format perfmon-xml` flag.

### 6d. `relog` hint (informational)

For operators who already have a broad BLG, a `relog` line trims it to the recommended set
rather than re-capturing:

```bat
relog input.blg -cf windows-core.counters.txt -f CSV -o trimmed.csv
```

This mirrors the legacy BLG→CSV conversion (`PALFunctions.vb:1123-1125`) and the project's
documented `relog -f CSV` fallback for non-Windows BLG ingestion.

### Format summary

| Format | Flag | Built from | Priority |
|--------|------|-----------|----------|
| Human counter list | default | derived display paths + rule attribution | P1 |
| `logman create counter` (inline / `-cf`) | `--logman` | derived display paths + interval | **P1 (primary ask)** |
| PerfMon DCS `.xml` | `--format perfmon-xml` | same paths, XML envelope | P2 |
| `relog` hint | `--relog` | counter-list file | P3 |

---

## 7. CLI / API / UI Surface

### 7a. CLI (Phase 1 — the only surface built now)

A new command under the existing `packs` branch (`Program.cs:24-28`):

```
pal packs counters <pack-id> [--logman] [--format <list|logman|perfmon-xml>]
                             [--interval <seconds>] [--output <file>]
                             [--include-base] [--instances <_Total|wildcard>]
```

- Lives in `dotnet/src/Pal.Cli/Commands/Packs/` beside `SignPackCommand.cs`, registered as
  `packs.AddCommand<CountersCommand>("counters")` next to the `sign` registration
  (`Program.cs:27`). It is a `Command<TSettings>` exactly like `SignPackCommand`
  (`SignPackCommand.cs:19`), so it inherits the same Spectre.Console.Cli wiring and exit-code
  conventions (`ExitCodes`).
- It resolves the pack via the same pack loader path `list-packs`/`validate-pack` use, reads
  `MetricAliasRegistry.BuildDefault()` for the reverse table, and prints to stdout (or
  `--output`). No DI container needed (the CLI constructs services directly, consistent with
  the collector-extensibility findings).
- Default `<list>` is human-readable; `--logman` is shorthand for `--format logman`.

### 7b. Transitive resolution (ties to plan 008)

> **Needs maintainer decision.** If packs gain a shared base (plan 008's
> shared-base/inheritance question), `pal packs counters iis-core` should arguably emit the
> *union* of `iis-core` and the always-on `windows-core`, since IIS is never analyzed in
> isolation — the auto-resolver always loads `windows-core` too (`README.md:63`). Proposed
> default: emit the named pack only, with `--include-base` (or `--with windows-core`) to add
> the always-on core. The merge must **dedup by counter path** exactly as the legacy logman
> exporter did (`PALFunctions.vb:346-364`), collapsing duplicate object+counter pairs onto a
> single `(*)` line. Until plan 008 settles base-pack semantics, keep merge behavior opt-in.

### 7c. API (later increment)

A read-only endpoint mirrors the CLI for the Phase 2 web consumer:
`GET /packs/{id}/versions/{version}/counters?format=logman`, alongside the existing
pack-validation endpoint `GET /packs/{id}/versions/{version}/validation` (`CLAUDE.md` Pack
validation API). Same pure transform, served over HTTP. Reachable as
`pal remote packs counters <id> <version>` to match the `pal remote validate-pack` pattern.
**Deferred** — not built in this spike.

### 7d. Wizard / UI (later increment)

The legacy Wizard equivalent: a `/packs/<id>` page (or a step in an onboarding wizard) with
a "Download capture template" button offering logman / PerfMon-XML downloads — the direct
descendant of `frmPALExecutionWizard.vb:1076-1090`. **Deferred** to a future increment;
listed here only to show the eventual shape.

---

## 8. Recommended First Step

A scoped outline for a follow-up implementation plan (not executed here).

**Title**: "Implement `pal packs counters` with logman export (pure derivation)"

**Scope (no schema change in the first cut):**

1. Add a `CounterRecommender` service in `Pal.Packs` (or `Pal.Application`) that takes a
   loaded pack + `MetricAliasRegistry` and returns an ordered, deduplicated list of
   `(canonicalId, displayPath, ruleIds[])`.
2. Add the de-regex helper that materializes a literal display path from a `BuildDefault`
   regex (`\\[^\\]+\\` → `\`, `\([^)]*\)` → `(*)`, unescape `\.` and counter-name
   literals). Unit-test it against every entry in `MetricAliasRegistry.cs:10-102` so a new
   alias that breaks the grammar fails loudly.
3. Add formatters: human list (P1), `logman create counter` inline + `-cf` file (P1).
4. Add `CountersCommand : Command<CountersSettings>` under `Commands/Packs/`, register it
   on the `packs` branch (`Program.cs:24-28`).
5. Golden-fixture tests: assert byte-identical logman output for `windows-core`,
   `iis-core`, and `sql-host-core` (the transform is pure, so output is deterministic
   without a clock override).

**Second increment (the schema part — its own ADR):** add the optional `capture:` block
(Section 5), teach `PackValidator` and the schema about it, and have the recommender fold
`extra_counters` / `instance_overrides` / `interval_seconds` into the derived set.

**Explicitly NOT in the first cut:** PerfMon `.xml` and `relog` formats, the API endpoint,
the Wizard/UI, transitive base-pack merging, and counter-name localization.

**Estimated effort**: S–M. The derivation and logman formatter are mechanical; the de-regex
helper is the only subtle piece, and it is fully covered by the existing alias table.

---

## 9. Open Questions

### 9a. Sample-interval default per workload

**Evidence**: no pack states an interval; `logman -si` is mandatory. Legacy left this to
the operator. **Proposed**: tool-wide default of 15s, overridable by `--interval` and
(later) by `capture.interval_seconds`. High-rate counters (`system.context_switches_per_sec`)
may warrant a finer default, but a single per-pack interval is simpler and matches PerfMon's
single-`<SampleInterval>` model. **Needs maintainer decision** on the default value and
whether per-counter intervals are ever worth the complexity (PerfMon DCS supports only one).

### 9b. Instance scope — capture `_Total` or wildcard `(*)`?

**Evidence**: rules pin `instance: "_Total"` for *evaluation* (`windows-core/pack.yaml:84`),
but the `BuildDefault` patterns match any instance (`\([^)]*\)`,
`MetricAliasRegistry.cs:15`), and legacy capture collapsed everything to `(*)`
(`PALFunctions.vb:346-364`). **Proposed**: capture `(*)` by default (so per-instance
correlation is available even though the rule evaluates `_Total`), with
`--instances _Total` to capture exactly what the rules read and minimize log size.
**Needs maintainer decision** on the default, since `(*)` on `\Process(*)` can be a large
counter set on busy servers.

### 9c. Should recommended counters mirror `applicability` (trigger auto-resolution)?

**Evidence**: `iis-core` auto-loads when its `requires_any` metrics are present
(`iis-core/pack.yaml:7-12`). If the recommended set is the rules' metrics, capturing it will
*by construction* satisfy `requires_any` and trigger auto-resolution — the recommender and
the resolver are duals. **Proposed**: treat this as a self-consistency invariant — assert in
tests that the recommended set for a pack is a superset of that pack's
`applicability.requires_any` IDs, so following the recommendation always re-loads the pack.
**Needs maintainer decision** only if a pack ever recommends counters it does *not* require
for resolution.

### 9d. Single merged template across multiple packs?

**Evidence**: the auto-resolver loads several packs together (`windows-core` + `iis-core`),
and the legacy exporter dedups across all datasources into one list (`PALFunctions.vb:425-433`).
**Proposed**: support `pal packs counters windows-core iis-core` (or `--with`) emitting one
merged, path-deduplicated template. Blocked on plan 008's base-pack model for the *default*
behavior (Section 7b); the merge mechanics (collapse duplicate object+counter to one `(*)`
line) are settled by the legacy precedent. **Needs maintainer decision** on whether merge is
default or opt-in.

### 9e. Localization of counter names (non-English Windows)

**Evidence**: PerfMon counter names are localized; `logman` on a German host wants
`\Prozessor(*)\Prozessorzeit (%)`, not `\Processor(*)\% Processor Time`. Legacy solved this
with `CounterLang.xml` (per-locale `<CounterName enUS=… deDE=… frFR=…>` rows). PAL-X has **no
such table** — `BuildDefault` is English-only (`MetricAliasRegistry.cs:10-102`). **Proposed**:
emit English paths for Phase 1 (correct for English Windows and for any host where the capture
is later analyzed by counter *index* rather than name); defer localized export until there is a
ported `CounterLang.xml` equivalent. **Needs maintainer decision** on whether non-English
capture is a Phase-1/Phase-2 requirement; it is a substantial data-porting task on its own.

### 9f. Where does the canonical display-path live — derived, or stored?

**Evidence**: today the literal path exists only implicitly inside the `BuildDefault` regex.
**Proposed**: derive it (Section 4 step 3) rather than storing a second source of truth, so
the alias table stays the single owner of the path↔ID mapping. **Needs maintainer decision**
if the de-regex grammar ever diverges (e.g. a future alias uses alternation like the SQL
`(SQLServer|MSSQL\$…)` entry at `MetricAliasRegistry.cs:63`, which has no single literal
form — the recommender must pick the `SQLServer:` default and let `capture.instance_overrides`
or a `--sql-instance` flag add named instances, exactly as legacy prompted at
`PALFunctions.vb:317-327`).

---

## 10. Non-Goals

Out of scope for the counter-recommender work; none should be folded in without a separate
ADR.

- **Running the capture.** The recommender emits text; it never invokes `logman`, starts a
  Data Collector Set, or schedules collection. Operators run the emitted command themselves.

- **Live-machine counter discovery.** No enumeration of counters present on the current host
  (`typeperf -q`, PDH `PdhEnumObjects`). The recommendation is derived purely from pack
  metadata, not from what a given machine exposes.

- **Remote / fleet collection.** No pushing templates to remote machines, no central
  scheduling, no agent. The output is a local command for a single host.

- **Counter-name localization (Phase 1).** English paths only until a `CounterLang.xml`
  equivalent is ported (Q9e). Localized export is a separate data-and-design effort.

- **Rewriting analysis to consume the template.** The recommender is the *capture-time*
  inverse of analysis; it does not change how `pal analyze`, the collectors, or the rule
  engine work. The loop closes only because the emitted `.blg`/`.csv` is already a valid
  PAL-X input.

- **Threshold/interval tuning advice.** The recommender says *what* to capture, not *how
  long* or *under what load*. Capture-duration guidance (legacy had time-range heuristics via
  `relog -q`, `frmPALExecutionWizard.vb:107-115`) is a separate onboarding concern.
