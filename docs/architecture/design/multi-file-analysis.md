# Multi-File / Batch & Cross-Run Aggregate Analysis Design

> **Status**: spike / pre-ADR
> **Author**: design spike (plan 011), 2026-06-13
> **Next step**: maintainer acceptance of the recommended scope promotes batch mode to an
> implementation plan, and (separately) the merge-mode schema change to an ADR under
> `docs/architecture/adr/000N-multi-file-merge.md`

---

## 1. Current State

Verified against the codebase at commit `208e140`.

### Single input only — `pal analyze`

`AnalyzeSettings` exposes exactly one input option:

```csharp
// dotnet/src/Pal.Cli/Commands/AnalyzeCommand.cs:14-16
[CommandOption("--input <path>")]
[Description("Path to input dataset (CSV or BLG)")]
public required string Input { get; init; }
```

Validation treats it as a single existing file:

```csharp
// AnalyzeCommand.cs:106-107
if (!File.Exists(settings.Input))
    return ValidationResult.Error($"Input file not found: {settings.Input}");
```

There is no `--inputs`, `--input-glob`, or `--input-dir`, and no `Directory.GetFiles` call —
a grep for `input-glob|--inputs|Directory.GetFiles` over `AnalyzeCommand.cs` returns no
matches. To analyze N files today, an operator scripts a shell loop over `pal analyze` and
then correlates N separate reports by hand. The legacy PAL v2 tool accepted a
comma-separated list of logs in one invocation; PAL-X has not yet ported that affordance.

### The repeatable-option pattern already exists in the same settings class

`--pack` and `--pack-dir` are already modelled as repeatable string arrays in the same
`AnalyzeSettings`:

```csharp
// AnalyzeCommand.cs:26-32
[CommandOption("--pack <pack-id>")]
public string[] Packs { get; init; } = [];

[CommandOption("--pack-dir <path>")]
public string[] PackDirs { get; init; } = [];
```

Spectre.Console.Cli collects repeated `--pack` flags into the array automatically. So a
repeatable `--input` (or a glob option resolved via `Directory.GetFiles`) is **idiomatic to
copy** — the cheap tier is genuinely small.

### Output naming derives from the single input stem

```csharp
// AnalyzeCommand.cs:185-189
Directory.CreateDirectory(settings.Output);
string stem = settings.ReportName ?? Path.GetFileNameWithoutExtension(settings.Input);
string jsonPath = Path.Combine(settings.Output, $"{stem}.pal-report.json");
string htmlPath = Path.Combine(settings.Output, $"{stem}.pal-report.html");
string mdPath   = Path.Combine(settings.Output, $"{stem}.pal-report.md");
```

The artifact name is `<input-stem>.pal-report.{json,html,md}` directly under `--output`
(matching `CLAUDE.md` → "CLI output naming"). With a single input there is no collision
risk; with a batch, two inputs that share a stem (`web-01/perf.csv` and `web-02/perf.csv`,
or `--report-name` supplied once for many inputs) would clobber each other. Any batch design
must resolve this.

### The report schema reserves a multi-source field — always `1` today

```jsonc
// dotnet/schemas/pal.report.v1.json:61-73
"Input": {
  "type": "object",
  "required": ["source_type", "source_path", "source_count", "collector", "collector_version"],
  "properties": {
    "source_type":       { "type": "string", "enum": ["blg", "csv"] },
    "source_path":       { "type": "string" },
    "source_count":      { "type": "integer", "minimum": 1 },
    "collector":         { "type": "string" },
    "collector_version": { "type": "string" }
  }
}
```

`source_count` is **required**, `integer, minimum: 1`. There is exactly one `source_path`
(singular string) and no `source_paths[]` array. The writer hardcodes the reserved value:

```csharp
// dotnet/src/Pal.Reporting/Json/JsonReportWriter.cs:83-85
source_type  = sourceType,
source_path  = Path.GetFileName(input.InputPath),
source_count = 1,
```

So the schema *anticipates* multi-source input (`source_count` is not boolean and not fixed
to `1` in the schema) but the writer never emits anything but `1`, and there is no place to
list more than one path. The `source_type` enum is also closed to `["blg", "csv"]` — a merged
report mixing a BLG and a CSV would not have a single valid `source_type`.

### Findings carry no source attribution

```jsonc
// pal.report.v1.json:158-186
"Finding": {
  "required": ["finding_id", "pack_id", "rule_id", "severity", "category",
               "title", "summary", "explanation", "evidence", "recommendations"],
  ...
}
```

The `finding_id` is `SHA-256(rule_id || canonical_metric_id || window_start || window_end)`
(schema:164) and evidence references `series_id` / `canonical_metric` (schema:201-213). None
of these carry a *source file* reference. With one input that is fine — every finding came
from the only file. In a merged report this is the central gap: nothing in the finding or its
evidence says *which server / which input* tripped the rule.

### Collectors are single-file by contract — merge is not a collector concern

`docs/architecture/design/collector-extensibility.md` §6 ("Non-Goals") states explicitly:

> **Multi-file / directory collectors**: The current contract takes a single `filePath`.
> Collectors that aggregate across a directory of files (e.g., a directory of per-hour
> BLG files) need a different API shape.

The `IDatasetCollector.Collect(string filePath, ...)` signature is one file in, one
`CollectResult` out. So merge mode does **not** belong in the collector layer; it belongs
*above* the collector — in the analysis runner / engine that would union N `Dataset`s before
rule evaluation. This spike honors that boundary.

### The analysis pipeline is strictly one-file → one-report

`AnalysisRunner.Run` is linear and single-dataset:

```csharp
// dotnet/src/Pal.Application/Analysis/AnalysisRunner.cs:13-32
var registry  = MetricAliasRegistry.BuildDefault();
var collector = CollectorFactory.Create(request.InputFormat, registry);
...
var collectResult = collector.Collect(request.InputPath, request.MachineName, request.TimeZone);
var dataset       = collectResult.Dataset with { HostContext = hostCtx };
...
var resolveResult = resolver.Resolve(request.PackIds, request.PackDirs, request.AutoResolvePacks, presentMetrics);
...
var engine        = new RuleEngine();
var engineResult  = engine.Run(resolveResult.Packs, dataset);
```

Two structural facts fall out of this:

- **Batch mode** loops *around* this method: call `AnalysisRunner.Run` once per input,
  write one report per result. No change to the runner, engine, collectors, or schema.
- **Merge mode** inserts *inside* this method: build N `CollectResult`s, **union the
  `Dataset`s** between line 21 (collect) and line 31 (rule eval), then run the engine once
  over the merged dataset. That union is new code and the result no longer fits the
  single-source report shape.

### The API job model is one upload → one job → one report

```csharp
// dotnet/src/Pal.Persistence/Entities/AnalysisJobEntity.cs:7,21
public Guid UploadId { get; set; }
...
public UploadEntity Upload { get; set; } = null!;
```

`AnalysisJobEntity.UploadId` is a single scalar FK to one `UploadEntity`; a job cannot
reference more than one upload. Conversely `UploadEntity.AnalysisJobs`
(`UploadEntity.cs:14`) is a collection — many jobs can target the *same* upload, but no job
spans many uploads. Merge mode on the API path therefore needs either a multi-upload job
(`UploadId` → a join table) or a job that references several uploads — a persistence/schema
change beyond this spike.

### Cross-run analysis already exists (compare / trends) — but on *reports*, not *datasets*

`CompareRunner.Run` (`dotnet/src/Pal.Application/Compare/CompareRunner.cs:17-25`) takes two
**already-completed** jobs' findings JSON, indexes each set by correlation key, and emits
diffs (`resolved` / `severity_changed` / `unchanged` / `new`). `TrendAnalyzer`
(`dotnet/src/Pal.Application/Trends/TrendAnalyzer.cs`) tracks a metric across a series of
completed jobs over time. Both operate on the *output* of independent single-file runs — they
correlate findings after the fact. Neither unions raw datasets before rule evaluation. This
matters for merge mode's justification (see Open Questions §7) — some "analyze these
together" use cases may already be served by compare/trends without a new merge engine.

---

## 2. Two Problems Hiding Under "Multi-File"

"Multi-file analysis" is not one feature. It is two products with very different cost and
risk, and the maintainer must pick scope before any implementation plan is written.

| | **Batch mode** | **Merge mode** |
|---|---|---|
| **Intent** | Analyze N inputs, get N independent reports in one invocation | Analyze N inputs into **one** cross-server / time-sliced report |
| **Mechanism** | Loop around `AnalysisRunner.Run` (§1) | Union N `Dataset`s, evaluate rules once over the merged series |
| **Report shape** | N unchanged single-source reports | One report with multiple sources + per-finding attribution |
| **Schema impact** | **None** — `source_count` stays `1` per report | `source_count > 1`, new `source_paths[]`, per-finding source ref, `source_type` enum widened — **schema version bump** |
| **Engine impact** | None | New dataset-union step + finding attribution |
| **API impact** | None (each upload is still its own job) | Multi-upload job model (`UploadId` → join) |
| **New questions** | Output collision, exit-code aggregation, parallelism | Timestamp alignment, time-zone reconciliation, gap/overlap handling, memory ceiling, attribution |
| **Effort** | **S** | **L** |
| **Risk** | LOW | MEDIUM–HIGH |
| **ADR needed?** | No | **Yes** |

**The core recommendation of this spike is to keep these separate.** Conflating them in one
implementation plan is the main hazard this document exists to prevent: batch mode is a cheap,
high-value loop with zero schema risk, and dragging it behind merge mode's schema bump and
union algorithm would delay the easy win for the hard one.

---

## 3. Batch Mode Design (the cheap tier)

**Goal**: `pal analyze` accepts many inputs in one invocation and writes one report per input,
with no schema, engine, or collector change.

### 3.1 CLI surface

Two viable shapes; **recommend the glob option** as the primary, with the repeatable
`--input` retained for explicit lists. Both reuse the established `string[]` pattern from
`--pack` (§1).

```csharp
// proposed addition to AnalyzeSettings — NOT applied in this spike
[CommandOption("--input <path>")]         // already exists; make repeatable
public string[] Input { get; init; } = [];

[CommandOption("--input-glob <pattern>")] // new: portable via Directory.GetFiles
[Description("Glob of input datasets, e.g. captures/*.blg")]
public string? InputGlob { get; init; }
```

Resolution rules:

- `--input-glob "captures/*.blg"` expands via `Directory.GetFiles(dir, pattern)` to a sorted,
  de-duplicated file list. (`Directory.GetFiles` does not currently appear anywhere in
  `AnalyzeCommand.cs` — this is net-new.)
- Repeated `--input a.csv --input b.csv` collects into the array, exactly as `--pack` does.
- Glob + explicit inputs may combine; the union (de-duplicated, stable-sorted by full path)
  is the batch set.
- **Backward compatibility**: a single `--input` with one match is byte-for-byte the same run
  as today — one report, same name, same exit code. Batch behavior only engages when the
  resolved set has > 1 element. This keeps the golden-fixture / `--now` determinism tests
  (`CLAUDE.md` → "Test determinism") unchanged for single-input runs.

> **Needs maintainer decision**: glob-only, repeatable-`--input`-only, or both? A glob is the
> most ergonomic for "a folder of hourly BLGs"; an explicit list is clearer in scripts and CI.
> Recommendation: ship both; they are trivially additive.

### 3.2 Per-input output naming + collision handling

Each input runs the existing pipeline and writes `<stem>.pal-report.{json,html,md}`. The
collision risk is real: two inputs can share a stem (different directories, or the same name
across machines). Resolution, in priority order:

1. **Default (recommended): one subdirectory per input under `--output`.** For input
   `web-01/perf.csv`, write `<output>/perf/perf.pal-report.json`; on stem collision, fall back
   to a disambiguated subdir (`<output>/perf-2/…`). A subdir-per-input also gives each run its
   own `charts/` folder, matching the existing `<output>/charts/<report-name>-<chart-id>.svg`
   convention (`CLAUDE.md` → "CLI output naming") without cross-input chart-name collisions.
2. `--report-name` becomes **invalid with a multi-input set** (it names a single artifact);
   reject it at validation time with a clear error when the resolved set is > 1.
3. The deterministic-stem rule must guarantee a stable, collision-free mapping for golden
   tests — derive the subdir from the relative path when a bare stem repeats.

> **Needs maintainer decision**: subdir-per-input (clean, recommended) vs. flat output with a
> machine/index suffix (`perf.pal-report.json`, `perf-2.pal-report.json`). Subdir is cleaner
> for charts; the flat form is friendlier to a single `ls`.

### 3.3 Aggregate exit code + summary line

`pal analyze` today returns `ExitCodes.GeneralFailure` when `--fail-on-warning` is set and any
warning/critical exists (`AnalyzeCommand.cs:239-240`). Batch must aggregate this across the set:

- Run each input; track each run's worst severity.
- **Exit code = the worst single-run outcome.** If any input fails collection
  (`PlatformNotSupportedException`, parse error), the batch surfaces that input's failure exit
  code but **continues** the remaining inputs (fail-soft) so one bad file does not abort the
  folder. A `--stop-on-error` flag could opt into fail-fast.
- With `--fail-on-warning`, the batch returns `GeneralFailure` if *any* input produced a
  warning/critical finding.
- Emit a **summary line** after the loop, e.g.
  `Batch: 12 inputs — 9 healthy, 2 warning, 1 critical (3 reports flagged)`, so the operator
  does not have to open 12 reports to find the one that matters.

> **Needs maintainer decision**: fail-soft-and-continue (recommended, matches "analyze a folder"
> intent) vs. fail-fast on first error.

### 3.4 What batch mode does *not* touch

No change to `dotnet/schemas/pal.report.v1.json` (every report stays `source_count = 1`), no
change to `AnalysisRunner`, `RuleEngine`, or any collector, and no API change — each upload is
still its own job. Batch is purely a CLI-layer loop + argument resolution.

---

## 4. Merge Mode Design (the deep tier)

**Goal**: analyze N inputs into **one** cross-server (or time-sliced) report. This is where the
hard problems live, and where the schema and API have to move.

### 4.1 Dataset union — the hard part

Merge inserts between `collector.Collect` (`AnalysisRunner.cs:21`) and `engine.Run`
(`AnalysisRunner.cs:32`): collect each input to a `Dataset`, then union into one merged
`Dataset` before rule evaluation. The union must answer:

- **Counter-path union.** Inputs may carry overlapping or disjoint series. Series present in
  some files but not others create ragged coverage; the union must decide whether a missing
  series is a gap, an absence, or an error per input.
- **Timestamp alignment.** Files captured on different machines (or different hourly slices of
  one machine) rarely share a sample grid. The merge must define a common time axis —
  resample/align to a shared interval, or keep per-source series side-by-side under one
  dataset envelope.
- **Time-zone reconciliation.** `Dataset` carries a `time_zone` (schema:81) and collectors
  accept a `timeZone` override (`AnalysisRunner.cs:21`). Two inputs in different zones must be
  normalized to UTC before alignment or the merged window is meaningless. A single merged
  `time_zone` may no longer be representable.
- **Gaps and overlaps.** `Dataset.gap_count` (schema:87) is per-dataset today. Overlapping
  capture windows across files, and gaps within each, both need a defined merge semantics
  (e.g., union of covered intervals; per-source gap accounting).

This is materially more than "concatenate two CSVs" — it is a time-series alignment problem,
and is the single biggest reason merge is an **L** effort, not an **S**.

### 4.2 Rule evaluation over a merged series

Rules are declarative `metric + aggregation + operator + threshold + duration_percent`
(per `CLAUDE.md` → "Declarative comparators"). Over a merged dataset the maintainer must decide
the evaluation unit:

- **Per-source then summarize**: run the rule against each source's series, then report which
  sources tripped (closest to today's semantics; cleanest attribution).
- **Across-source aggregate**: evaluate the rule against a fleet-wide aggregate (e.g.,
  "average CPU across all 8 web servers"). Powerful, but `duration_percent` and the
  host-context-relative thresholds (`host_context.total_physical_memory_mb`,
  `host_context.logical_processor_count`, per `CLAUDE.md`) become ambiguous when servers differ
  in RAM/CPU count. With heterogeneous hosts, the existing rule emits an informational warning
  and skips when host context is unknown (`CLAUDE.md` → "host_context unknown = informational
  finding + rule skipped"); a fleet aggregate would need a defined per-host-context policy.

> **Needs maintainer decision**: per-source-then-summarize (recommended starting point —
> preserves today's rule semantics and gives natural attribution) vs. a true cross-source
> aggregate engine (a larger semantic change to the rule evaluator).

### 4.3 Finding attribution — the schema change

A merged report is useless if a finding does not say *which source* tripped it. Today the
`Finding` has no source field (§1). Merge needs, at minimum:

- **Report-level**: populate `source_count > 1`, add a `source_paths[]` array (the singular
  `source_path` stays as a primary/representative path or is deprecated in favor of the array),
  and widen the `source_type` enum so a mixed BLG+CSV merge is representable (or restrict merge
  to a single homogeneous `source_type`).
- **Per-finding**: add a source reference on the finding (or on each `EvidenceMetric`, which
  already carries `series_id` at schema:205) so each finding cites the originating input(s).
- **`finding_id` impact**: `finding_id` is `SHA-256(rule_id || canonical_metric_id ||
  window_start || window_end)` (schema:164). If two sources trip the same rule on the same
  metric in the same window, their finding IDs collide. The merge must fold a source
  discriminator into the hash, which is a **breaking change to `finding_id` derivation** and
  must be reconciled with compare/trends correlation keys.

**This is ADR-worthy.** It is a `pal.report/v1` → `pal.report/v2` (or a `v1.1` additive)
schema bump plus a `finding_id` semantics change. It must not be smuggled in as an
implementation detail. Per `CLAUDE.md`, `dotnet/schemas/pal.report.v1.json` is the authoritative
report schema; any change to it is a deliberate, versioned decision — out of scope for this
spike and gated behind its own ADR.

### 4.4 API job-model implication

Merge on the API path needs a job that references several uploads. Today
`AnalysisJobEntity.UploadId` (`AnalysisJobEntity.cs:7`) is a single FK. Options:

- Promote `UploadId` to a join entity (`AnalysisJobUploadEntity`) — a migration plus repository
  and worker changes (`AnalysisWorker`, `AnalysisRepository`).
- Or define a "merge job" type that lists upload IDs in `OptionsJson`
  (`AnalysisJobEntity.cs:9`) without a schema migration — cheaper, but weaker referential
  integrity and no cascade/retention guarantees.

Either way it is a persistence-layer change beyond this spike and should ride with the
merge-mode ADR. (CLI merge, by contrast, needs no persistence change — it reads files
directly, like batch.)

---

## 5. Recommended Scope & First Step

**Recommendation: ship batch mode first; treat merge mode as a separate, ADR-gated effort.**

Rationale:

- Batch is **S effort, LOW risk, zero schema change**, and absorbs the most common friction
  today (a folder of per-server or hourly captures), reusing the proven `string[]`
  repeatable-option pattern. It is a near-pure win.
- Merge is **L effort** and carries a time-series alignment problem (§4.1), a `finding_id` /
  report-schema breaking change (§4.3), and an API persistence change (§4.4) — none of which
  should block the cheap win.
- Some "analyze these together" needs may already be met by **compare** and **trends** (§1,
  §7), so merge's incremental value should be validated against those before paying its cost.

### 5.1 Recommended First Step — batch mode only

A scoped outline for a follow-up implementation plan (not executed here).

**Title**: "Batch analysis: many inputs, many reports, one invocation"

**Scope**:

1. Make `--input` repeatable (`string[]`) and add `--input-glob <pattern>` resolved via
   `Directory.GetFiles`, modeled on the `--pack` array (`AnalyzeCommand.cs:26-32`).
   De-duplicate and stable-sort the resolved set.
2. When the resolved set has one element, behave **exactly** as today (same report name, same
   exit code) — guard the golden-fixture / `--now` determinism tests.
3. When > 1: loop `AnalysisRunner.Run` per input; write each report into a per-input
   subdirectory under `--output` with stem-collision disambiguation (§3.2); reject
   `--report-name` for multi-input sets.
4. Aggregate exit code as the worst single-run outcome; fail-soft-and-continue on a single
   input's collection failure; honor `--fail-on-warning` across the whole batch (§3.3).
5. Emit a batch summary line.
6. Tests: a fixture folder with 2–3 inputs (including a deliberate stem collision and one
   intentionally-failing input), asserting per-input report paths, the aggregate exit code, and
   the summary line. Reuse `--now` for byte-identical per-report output.

**What this plan does NOT include**: any dataset union, any `pal.report.v1.json` change
(`source_count` stays `1`), any `AnalysisRunner`/`RuleEngine` change, and any API change. It is
CLI-layer only.

**Estimated effort**: S.

### 5.2 Deferred — merge mode (ADR-gated)

Merge mode is deferred to its own ADR + implementation plan covering: dataset union & alignment
(§4.1), rule-evaluation unit (§4.2), the report-schema bump and `finding_id` change (§4.3), and
the multi-upload API job model (§4.4). It must not be folded into the batch plan.

---

## 6. Non-Goals

Out of scope for this work, not to be folded in without a separate ADR:

- **Changing the single-file collector contract.** `IDatasetCollector.Collect` stays
  one-file-in (per `collector-extensibility.md` §6). Merge unions `Dataset`s *above* the
  collector; it never makes a collector aggregate a directory.
- **Streaming / live multi-server collection.** No live Prometheus scrape, CloudWatch stream,
  or real-time multi-host fan-in. The contract remains synchronous and file-path-based.
- **Re-implementing compare or trends.** Cross-run *finding* correlation already exists
  (`CompareRunner`, `TrendAnalyzer`); merge mode must not duplicate it. Merge unions *raw
  datasets* before rule eval — a distinct concern from diffing finished reports.
- **A numeric fleet "health score."** Per `CLAUDE.md` (ADR 0001) PAL-X uses tri-state status,
  not a 0–100 score; a merged report aggregates tri-state statuses, it does not invent a score.
- **Batch-mode schema changes.** Batch must remain a pure CLI loop; if a proposal for batch
  starts touching `pal.report.v1.json`, it has drifted into merge and needs the merge ADR.

---

## 7. Open Questions

### 7a. Parallel vs. sequential batch execution?

**Current evidence**: `AnalysisRunner.Run` is stateless per call and constructs its own
`MetricAliasRegistry`, collector, resolver, and engine (`AnalysisRunner.cs:13-32`), so runs are
independent and parallelizable in principle. BLG collection is Windows PDH interop (`CLAUDE.md`
→ "BLG ingestion") and may not be thread-safe across concurrent PDH queries.

**Proposed answer**: Start **sequential** for determinism and simple, ordered console output;
add bounded parallelism (`--max-parallel N`) later, guarding BLG concurrency. **Needs maintainer
decision** on whether the folder-of-captures use case is large enough to justify parallelism in
the first cut.

### 7b. Memory ceiling for merge over many large BLGs?

**Current evidence**: Merge holds N `Dataset`s in memory simultaneously to union them (§4.1),
unlike batch which processes one at a time and releases it. A multi-day capture split into 24+
hourly BLGs could exceed a reasonable working set.

**Proposed answer**: Merge needs a memory budget and possibly a streaming/windowed union rather
than naive all-in-memory concatenation. This is a design input to the merge ADR, not the batch
plan. **Needs maintainer decision** on the target input scale (how many files, how large).

### 7c. Do compare/trends already cover the cross-run use cases merge would serve?

**Current evidence**: `CompareRunner.Run` (`CompareRunner.cs:17-25`) diffs two completed jobs'
findings; `TrendAnalyzer` tracks a metric across a sequence of completed jobs. Both correlate
*report outputs* of independent single-file runs.

**Proposed answer**: For "did this regress between two captures of the same server?" compare
already wins, and "how did this metric move across daily captures?" is trends. Merge's unique
value is the *single cross-server snapshot* (8 web servers analyzed as one fleet at one point in
time) and *time-sliced reassembly* (24 hourly BLGs as one continuous day). **Needs maintainer
decision**: enumerate the concrete merge use cases that compare/trends do *not* already serve
before committing to the L effort.

### 7d. Does merge belong in the engine, a new aggregator, or the runner?

**Current evidence**: `AnalysisRunner` (`Pal.Application`) orchestrates collect → resolve → run;
`RuleEngine` (`Pal.Engine`) evaluates rules over one `Dataset`. The union step (§4.1) has no
home today.

**Proposed answer**: The union is an *application-layer* concern (assemble inputs, reconcile
time/zones, attribute sources) feeding a possibly-unchanged `RuleEngine`, or it lives in a new
`Pal.Engine` aggregator if per-source-vs-aggregate evaluation (§4.2) demands engine awareness.
**Needs maintainer decision**, settled in the merge ADR alongside the evaluation-unit choice.

### 7e. How is a heterogeneous merge (mixed `source_type` / mixed host context) represented?

**Current evidence**: `source_type` enum is closed to `["blg", "csv"]` (schema:67) and a single
report carries one `source_type`; host-context thresholds assume one machine's RAM/CPU count
(`CLAUDE.md`).

**Proposed answer**: Either restrict merge to a homogeneous `source_type` and per-host-context
evaluation, or widen the schema and define a multi-host-context policy. **Needs maintainer
decision** — this directly shapes the merge schema bump (§4.3).
