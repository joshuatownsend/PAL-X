# Charts in the HTML Report Design

> **Status**: spike / pre-ADR
> **Author**: design spike (plan 010), 2026-06-13
> **Next step**: maintainer acceptance promotes a chosen embed strategy to an ADR under
> `docs/architecture/adr/000N-charts-in-html-report.md`, and a small follow-up
> implementation plan wires it in.

---

## 1. Current State

The headline finding is stronger than the planning note assumed. PAL-X has a
**deterministic SVG chart renderer with golden-grade tests, but no code anywhere actually
calls it to write a chart file, no report writer references a chart, and the CLI flags that
were meant to drive it are dead.** The expensive, hard part (byte-deterministic SVG) is
done; the wiring is 100% missing — not merely "orphaned files nobody links to."

### The renderer exists and is deterministic

`dotnet/src/Pal.Reporting/Charts/ScottPlotRenderer.cs` exposes two entry points:

```csharp
// ScottPlotRenderer.cs:18  — returns canonicalized SVG as a string (in-memory)
public static string Render(
    string title,
    IReadOnlyList<(DateTimeOffset ts, double value)> series,
    double? warningThreshold = null,
    double? criticalThreshold = null)

// ScottPlotRenderer.cs:36  — renders, then writes the SVG to a file
public static void RenderToFile(
    string title,
    IReadOnlyList<(DateTimeOffset ts, double value)> series,
    string outputPath,
    double? warningThreshold = null,
    double? criticalThreshold = null)
```

`Render` forces `CultureInfo.InvariantCulture` for the duration of the render
(`ScottPlotRenderer.cs:24-33`), draws a single scatter line plus optional dashed warning /
critical horizontal threshold lines from a frozen explicit-hex palette
(`ScottPlotRenderer.cs:13-16`), then pipes the raw SVG through
`SvgCanonicalizer.Canonicalize` (`ScottPlotRenderer.cs:90-91`). The plot size is fixed at
720×360 (`ScottPlotRenderer.cs:9-10`). `RenderToFile` writes with
`new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)` (`ScottPlotRenderer.cs:44`) —
UTF-8 without BOM, per the project convention.

`SvgCanonicalizer` (`dotnet/src/Pal.Reporting/Charts/SvgCanonicalizer.cs`) strips comments
and `<metadata>` blocks (which can embed a ScottPlot version stamp), then rewrites every
`id="..."` definition and `url(#...)` reference to a stable `pal-N` sequence numbered in
document order (`SvgCanonicalizer.cs:24-65`). This is what makes two renders of the same
data **byte-identical** despite SkiaSharp's otherwise-random element ids.

### `RenderToFile` has zero callers — the chart pipeline is not wired

```
$ grep -rn "RenderToFile" dotnet
dotnet/src/Pal.Reporting/Charts/ScottPlotRenderer.cs:36:    public static void RenderToFile(
```

The **only** hit is the definition itself. No production code and no test ever calls
`RenderToFile`. The only caller of `Render` is `ScottPlotRendererTests`
(`dotnet/tests/Pal.Reporting.Tests/ScottPlotRendererTests.cs:21-76`). So **no chart SVG is
ever written to disk by any code path today** — the `<output>/charts/<report-name>-<chart-id>.svg`
location described in `CLAUDE.md` is a planned convention, not a path any running code
produces.

### The CLI flags are defined but never consumed

`AnalyzeSettings` declares both flags:

```csharp
// AnalyzeCommand.cs:74-80
[CommandOption("--include-charts")]
[Description("Emit chart SVG artifacts")]
public bool IncludeCharts { get; init; }

[CommandOption("--chart-limit <n>")]
[Description("Maximum charts to generate (default: 20)")]
public int ChartLimit { get; init; } = 20;
```

But `AnalyzeCommand.Execute` (`AnalyzeCommand.cs:115-243`) **never reads
`settings.IncludeCharts` or `settings.ChartLimit`**. The execute body runs the analysis,
writes JSON/HTML/Markdown, and returns — there is no branch on the chart flags and no call
into `ScottPlotRenderer`. Confirmed by grep: across all of `dotnet`, `IncludeCharts` /
`ChartLimit` appear only at their declaration site (`AnalyzeCommand.cs:74-80`). The flags
are inert.

> **Drift note for reviewers.** Plan 010's "Why this matters" framed charts as "written as
> standalone files that nothing links to." The live code is one step earlier than that:
> nothing writes the files in the first place. The drift check
> (`git diff --stat 208e140..HEAD` over the reporting project, `AnalyzeCommand.cs`, and the
> report schema) was **clean** — no in-scope source has changed since the plan was authored;
> the plan's "Current state" prose simply over-stated what existed. This doc records the
> verified reality.

### Neither report writer references a chart

```
$ grep -rniE 'chart|\.svg|<img|data:image' dotnet/src/Pal.Reporting/Html   → no matches
$ grep -rniE 'charts/|<img|data:image'     dotnet/src/Pal.Reporting        → no matches
```

`HtmlReportWriter` (`dotnet/src/Pal.Reporting/Html/HtmlReportWriter.cs`) builds the report
as a single self-contained HTML string: header, summary table, a `RenderFinding` block per
finding with an evidence table (`HtmlReportWriter.cs:103-114`), a warnings list, and a
footer. It emits an `<evidence-table>` of `Metric / Avg / Max / P95 / Condition`
(`HtmlReportWriter.cs:106-111`) but **no `<img>`, no inline `<svg>`, no `charts/` link**.
There is no `<script>` and the only style is one inline `<style>` block
(`HtmlReportWriter.cs:129-153`).

`JsonReportWriter.MapFinding` (`dotnet/src/Pal.Reporting/Json/JsonReportWriter.cs:133-172`)
emits `evidence.metrics` but **no `evidence.charts`** array, even though the schema defines
one (see below).

### The schema already reserves a chart slot — also unpopulated

`dotnet/schemas/pal.report.v1.json` defines `Evidence.charts` and a `ChartRef`:

```jsonc
// pal.report.v1.json:195-198
"charts": { "type": "array", "items": { "$ref": "#/definitions/ChartRef" } }
// pal.report.v1.json:228-236
"ChartRef": {
  "required": ["chart_id", "artifact_path", "title"],
  "properties": {
    "chart_id":      { "type": "string" },
    "artifact_path": { "type": "string" },
    "title":         { "type": "string" }
  }
}
```

and a top-level `Artifacts.chart_paths` array (`pal.report.v1.json:288-291`). Both shapes
are present in the schema but written by **no** code path — the writers never populate
`evidence.charts` or `artifacts.chart_paths`. Notably, `ChartRef.artifact_path` presumes an
**on-disk file**, which is the link model (option C below), not an embed.

### Pipeline map — flag → renderer → file

| Stage | CLI path | API path |
|-------|----------|----------|
| Entry | `AnalyzeCommand.Execute` (`AnalyzeCommand.cs:115`) | `AnalysisWorker.GenerateAndStoreReportsAsync` (`Worker/AnalysisWorker.cs:217`) |
| Analysis | `new AnalysisRunner().Run(...)` (`AnalyzeCommand.cs:142`) | runner result passed in |
| JSON write | `new JsonReportWriter().Write(writeInput)` → file (`AnalyzeCommand.cs:215`) | `JsonReportWriter().WriteToStream(..., jsonMs)` → bytes → storage (`AnalysisWorker.cs:240-243`) |
| HTML write | `HtmlReportWriter.Write(writeInput, htmlPath)` → file (`AnalyzeCommand.cs:221`) | `HtmlReportWriter.WriteToStream(..., htmlMs)` → bytes → storage (`AnalysisWorker.cs:246-249`) |
| Markdown write | `new MarkdownReportWriter().Write(...)` (`AnalyzeCommand.cs:227`) | `MarkdownReportWriter().WriteToStream(...)` (`AnalysisWorker.cs:251-255`) |
| **Chart render** | **(none — `IncludeCharts` ignored, `RenderToFile` never called)** | **(none — worker never references `ScottPlotRenderer`)** |
| **Chart → file** | **(none)** | **(none — reports persist to storage via `WriteReportAsync`; there is no `charts/` dir)** |

### Does the API/server path produce charts? No.

`AnalysisWorker.GenerateAndStoreReportsAsync` (`dotnet/src/Pal.Api/Worker/AnalysisWorker.cs:217-256`)
generates exactly three artifacts — JSON, HTML, Markdown — entirely **in memory** via the
`WriteToStream` overloads, then persists each `byte[]` through
`_storage.WriteReportAsync(jobId, format, bytes, ct)` and records it with
`_analysisRepo.SaveReportAsync` (`AnalysisWorker.cs:242-255`). It never references
`ScottPlotRenderer`, never writes a `charts/` directory, and the user retrieves a report by
`GET /analysis/{id}/report?format=html|json|markdown`
(`dotnet/src/Pal.Api/Endpoints/AnalysisEndpoints.cs:71-91`), which streams the single stored
report blob. The Blazor UI links to that endpoint
(`dotnet/src/Pal.Api/Components/Pages/JobDetail.razor:82-84`). **There is no on-disk
sibling directory the browser could resolve a relative `charts/...` URL against.** This is
the single most important constraint for the embed-vs-link decision.

---

## 2. Goal

Make the HTML report **glance-able**: a stakeholder opening a PAL-X HTML report should see
the trend line and threshold bands for each triggering metric, not just a table of numbers.
Concretely, a successful implementation:

- Renders a chart per triggering metric of each finding (the warning/critical threshold
  lines that fired are drawn on the chart) and shows it next to that finding's evidence
  table in the HTML report.
- Works identically for **both** delivery paths: the CLI's on-disk HTML file and the API's
  stored-blob HTML download. A user who downloads the HTML and opens it from their Downloads
  folder — with no adjacent `charts/` directory — still sees every chart.
- Stays **byte-deterministic** under a fixed `--now`, reusing the existing
  `SvgCanonicalizer` so the chart bytes are stable.
- Costs the follow-up plan roughly: wire the renderer into the writer's per-finding loop,
  thread the series data + thresholds into the writer's input, and (one time, intentionally)
  regenerate the JSON golden fixtures.

A concrete target used to evaluate the options below: the `cpu-pressure` fixture, whose
`high-cpu-sustained` warning finding should render a CPU `% Processor Time` line with a
dashed warning line at the rule's threshold.

---

## 3. Design Options — embed vs. link

The chart SVG can reach the browser three ways. The deciding axis is the **API download
path**, which has no `charts/` directory (see §1).

### Option A — inline `<svg>` in the HTML body

Drop the canonicalized SVG markup directly into the HTML at the finding's location:

```html
<div class="finding sev-warning">
  ...evidence table...
  <svg viewBox="0 0 720 360" ...> ...chart... </svg>
</div>
```

**Pros**
- Fully self-contained: one HTML file, works for CLI-on-disk and API-download identically.
- No base64 inflation — the SVG bytes appear once, verbatim.
- SVG scales crisply and is themeable/inspectable in the DOM.
- The bytes already flow through `SvgCanonicalizer`, so determinism is inherited.

**Cons**
- The SVG's own `id="pal-N"` values (post-canonicalization) and any `url(#pal-N)`
  references live in the same DOM as the rest of the report. With multiple inlined charts,
  **ids collide** — every chart restarts its id counter at `pal-0`. The writer must
  namespace ids per chart (e.g. prefix with the `chart_id`) or wrap each chart in an
  `<svg>` with its own coordinate scope. This is the one real wrinkle.
- The SVG ScottPlot emits is prefixed with an XML declaration
  (`ScottPlotRendererTests.cs:61` notes "ScottPlot prepends an XML declaration before
  `<svg`"); an inline embed must strip that leading `<?xml ...?>` since it is illegal
  mid-HTML-body.
- **Sanitization**: ScottPlot output is trusted (we generate it), but if charts are inlined
  the writer should still assert the fragment contains no `<script>`/`<foreignObject>` and
  no event handlers, so a future untrusted input can't smuggle markup. Cheap to add as a
  guard.

### Option B — `data:image/svg+xml;base64` in an `<img>`

Base64-encode the SVG and reference it from an `<img>`:

```html
<img alt="CPU % Processor Time" src="data:image/svg+xml;base64,PHN2ZyB...">
```

**Pros**
- Self-contained; works for both delivery paths.
- **No id-collision risk** — the SVG is an isolated document inside the `<img>`, so its
  `pal-N` ids never touch the host DOM. This sidesteps Option A's only real wrinkle.
- Simpler writer code: encode and emit one attribute; no fragment surgery.

**Cons**
- ~33% size inflation from base64. For the default `--chart-limit 20` this is modest
  (each 720×360 line SVG is small), but it compounds on large reports.
- `<img>`-hosted SVG cannot inherit page CSS and is not DOM-inspectable — fine for a static
  chart, but less flexible than inline.
- The data URI must be built from the **canonicalized** bytes for determinism; the XML
  declaration is legal inside a data URI so no stripping is needed.

### Option C — relative `<img src="charts/<name>.svg">` link

Write each SVG to `<output>/charts/...` (the `CLAUDE.md` convention, matching
`ChartRef.artifact_path` in the schema) and link it.

**Pros**
- Smallest HTML; charts cached as separate files; matches the existing (unused)
  `ChartRef.artifact_path` / `Artifacts.chart_paths` schema slots.
- The CLI already has an `<output>` directory to write a `charts/` subdir into.

**Cons**
- **Breaks on the API download path.** The API stores the HTML as a single blob and serves
  it via `GET /analysis/{id}/report?format=html` (`AnalysisEndpoints.cs:71-91`); there is
  no adjacent `charts/` directory, and no endpoint that would serve one. A user who
  downloads the HTML sees broken image icons. This is disqualifying for the API path
  (which is the primary product surface), and a report that renders differently depending on
  how it was produced is exactly the inconsistency to avoid.
- Even for the CLI, a relative link breaks the moment the user emails or moves the `.html`
  without the `charts/` folder — the same fragility the legacy single-file report avoided.

### Recommendation

**Option B (`data:image/svg+xml;base64` in `<img>`)** is the recommended default.

It is the only option that is self-contained across both delivery paths *and* free of the
id-collision hazard, at the cost of a modest, bounded size increase that `--chart-limit`
already caps. Option A is a reasonable alternative if the size budget proves tight (base64
overhead matters) and the team is willing to namespace per-chart ids; the canonicalizer
would need a small extension to accept an id prefix. **Option C is rejected** because it
silently breaks the API download — PAL-X's primary surface. The schema's
`ChartRef.artifact_path` / `Artifacts.chart_paths` fields are link-shaped and should be
treated as **vestigial** under options A/B; the follow-up plan should decide whether to
repurpose `ChartRef` to carry an embed reference (e.g. `chart_id` + `title`, dropping
`artifact_path`) or leave the JSON report chart-free and embed only in HTML. **(Needs
maintainer decision — see Open Questions.)**

---

## 4. Chart Selection & Placement

### Which charts

One chart **per triggering metric of each finding** — i.e. per `EvidenceMetric` on a
finding, the metric named by `EvidenceMetric.CanonicalMetric` is plotted. The finding's
HTML already loops over `f.EvidenceMetrics` to build the evidence table
(`HtmlReportWriter.cs:107-111`); the chart for each row is rendered from the same series.
This keeps the visual tightly coupled to the evidence that explains it, rather than a
disconnected top-level gallery.

Threshold lines come from the rule that fired: the warning/critical thresholds passed to
`ScottPlotRenderer.Render(..., warningThreshold, criticalThreshold)` should be the
numeric thresholds of the triggering condition, so the chart visually shows the breach.

> **Data-availability gap (needs implementation attention).** The writer's current input,
> `JsonReportWriter.WriteInput`, does **not** currently carry the per-metric sample series
> or the rule thresholds in a form the chart renderer can consume. `EvidenceMetric` carries
> `Statistics` and `TriggerDetails` (`HtmlReportWriter.cs:108-111`), and the raw samples
> live on `input.Dataset.Series` keyed by `series_id` (`JsonReportWriter.cs:174-185`). The
> follow-up plan must join `EvidenceMetric.SeriesId` → the dataset's `TimeSeries` samples to
> build the `(DateTimeOffset ts, double value)` list `Render` expects, and surface the rule
> thresholds (available in `TriggerDetails.ExpectedValue` /
> `TriggerDetail.expected_value`, `pal.report.v1.json:223`). This is a wiring task, not a new
> data source — but it is the bulk of the implementation effort.

### Placement

Inside each `RenderFinding` block (`HtmlReportWriter.cs:93-124`), after the evidence table
and before the recommendations. Each chart sits beside the metric row that produced it.

### Limit behaviour

Respect `--chart-limit` (default 20, `AnalyzeCommand.cs:80`). The cap applies to the **total
number of charts in the report**, counted in finding order. When the cap is hit, remaining
findings render their evidence tables without charts (no truncation of the findings
themselves). For the API path, which has no CLI flag, a fixed default of 20 should apply
(or a config value); the worker would pass the same limit into the writer input.

### Deterministic ordering & identity

Findings are already emitted in the canonical sort order the engine enforces
(severity desc → category asc → rule_id asc → finding_id asc, per `CLAUDE.md`). Charts
inherit that order. Each chart needs a stable `chart_id`; the natural, collision-free choice
is `"{finding_id}-{series_id}"` (or a short hash of it), which is deterministic given the
deterministic finding/series ids. This `chart_id` also becomes the per-chart id namespace
prefix that Option A would need, and the `ChartRef.chart_id` if the JSON report later
carries chart refs.

### Default-on vs. opt-in

`--include-charts` is opt-in today (and inert). **Recommendation: make HTML chart embedding
default-on** once it is self-contained (Option B), because the whole value is
glance-ability and a self-contained embed has no orphaned-file downside. Keep a
`--no-charts` (or repurpose `--include-charts` to its inverse) escape hatch for users who
want the smaller HTML. **(Needs maintainer decision — see Open Questions.)** Whatever is
chosen, the API path needs an equivalent toggle or a fixed default since it has no CLI flags.

---

## 5. Determinism

Determinism is a hard constraint, and the good news is the hardest part is already solved.

### The chart bytes are already canonical

Every chart goes through `SvgCanonicalizer.Canonicalize` inside `Render`
(`ScottPlotRenderer.cs:90-91`), which strips version-stamped comments/metadata and rewrites
ids to a stable `pal-N` sequence. The renderer also pins `InvariantCulture`
(`ScottPlotRenderer.cs:24-33`) so decimal formatting can't drift by locale — exactly what
`ScottPlotRendererTests.Render_NumberFormatting_UsesInvariantCulture` asserts
(`ScottPlotRendererTests.cs:30-47`). And
`ScottPlotRendererTests.Render_ProducesByteIdenticalOutput_OnTwoRenders`
(`ScottPlotRendererTests.cs:18-27`) already proves two renders of the same series are
byte-identical. **The embedded bytes must be these canonical bytes** — the writer calls
`Render` (string) and embeds its result; it must never call into ScottPlot directly or
bypass the canonicalizer.

For **Option A**, the per-chart id prefix is a determinism input too: as long as the prefix
derives from the deterministic `chart_id`, the inlined ids stay stable. For **Option B**,
base64 of identical bytes is identical bytes — no extra concern.

### What golden fixtures actually exist (correcting the plan)

The plan's Step 4 anticipates "golden HTML fixtures" in `Pal.Reporting.Tests`. Verified
reality:

- **`Pal.Reporting.Tests` has no golden/byte-identical *report* fixtures.**
  `HtmlReportWriterTests` uses **substring** assertions (`Assert.Contains`,
  `HtmlReportWriterTests.cs:74-109`) and a BOM check — not a byte-for-byte golden file. The
  only byte-identical assertion in that project is the SVG self-consistency test noted above.
- **The byte-level report golden fixtures live in `Pal.Cli.Tests`**, not the reporting
  project: `GoldenFixtureTests.AssertMatchesGolden`
  (`dotnet/tests/Pal.Cli.Tests/GoldenFixtureTests.cs:212-255`) compares a freshly written
  report against `fixtures/<name>/golden.pal-report.json`. These goldens are **JSON-only**
  (one per fixture: `healthy-server`, `cpu-pressure`, `disk-latency`, `memory-pressure`,
  `GoldenFixtureTests.cs:184-210`). There is **no `golden.pal-report.html`** anywhere in the
  repo.
- The JSON comparison **masks** machine/OS/path-variant fields before comparing
  (`MaskEngineFields`, `GoldenFixtureTests.cs:258-271`): `report_id`, `dataset_id`,
  `engine.version/host_os/runtime`, and `artifacts.json_report_path`.

### Implication for the follow-up plan

- **If charts are embedded in HTML only (Option B, JSON unchanged):** the existing JSON
  goldens are **unaffected** — they assert nothing about HTML, and `MapFinding` would not
  gain a `charts` array. No golden regeneration is needed at all. This is a meaningful
  simplification the plan didn't foresee, and it argues for keeping charts out of the JSON
  report (embed in HTML, leave `evidence.charts` empty).
- **If the JSON report also gains `evidence.charts` (e.g. populating `ChartRef`):** the four
  `golden.pal-report.json` files **must be regenerated once, intentionally**, and that diff
  reviewed as a deliberate change — **not mistaken for drift.** Flag it prominently in the
  follow-up PR.
- **If an HTML golden is introduced** to lock chart embedding byte-for-byte (recommended, to
  protect determinism), it is a **new** fixture, not a regeneration — add a
  `golden.pal-report.html` per fixture and an `AssertMatchesGolden`-style HTML comparison.
  Because the embedded SVG bytes are canonical and the report is generated under a fixed
  `--now`/`GeneratedAt`, a byte-identical HTML golden is achievable; it would need the same
  masking treatment for any machine-variant fields the HTML surfaces.

---

## 6. Robust Stats (optional, separable add-on)

Legacy PAL surfaced outlier-trimmed means (10% / 20% / 30%) so a reviewer could see the
"typical" average under spiky data. PAL-X does not:

```
$ grep -niE 'outlier|excl_|trimmed' dotnet/schemas/pal.report.v1.json   → no matches
```

The `Statistics` object today carries `count/min/max/avg/median/p90/p95/p99/stddev/
trend_per_hour/missing_sample_count` (`pal.report.v1.json:265-281`), mirrored by the
`SeriesStatistics` model (`dotnet/src/Pal.Engine/Model/SeriesStatistics.cs:3-16`) and
computed in `SeriesStatisticsCalculator` (`dotnet/src/Pal.Engine/Statistics/
SeriesStatisticsCalculator.cs:37-43`).

**This is fully separable from the chart work** and should be its own plan. Adding
`avg_excl_10pct` / `avg_excl_20pct` / `avg_excl_30pct` touches a wider blast radius than the
charts:

1. `SeriesStatistics` model — three new `double` properties.
2. `SeriesStatisticsCalculator` — compute trimmed means (sort, drop the top *k*%, average).
3. `pal.report.v1.json` `Statistics` definition — three new optional properties.
4. **All three writers**, which each render an evidence table today:
   - `JsonReportWriter.MapStats` (`JsonReportWriter.cs:187-200`),
   - `HtmlReportWriter` evidence table (`HtmlReportWriter.cs:106-111`),
   - `MarkdownReportWriter` evidence table (`MarkdownReportWriter.cs:81-87`).
5. The four `golden.pal-report.json` fixtures — **regenerate once, intentionally** (the new
   stats fields would appear in every series' `statistics`).

Because step 5 alone is a deliberate golden regeneration and steps 1–4 touch the engine,
schema, and every writer, this add-on is best landed **after** the chart work, as a clean
independent change. It is explicitly out of scope for the chart spike and listed here only
so the chart plan does not accidentally entangle it.

---

## 7. Recommended First Step

A scoped outline for a follow-up implementation plan (not executed here).

**Title**: "Embed deterministic finding charts in the HTML report (Option B)"

**Scope**:

1. **Thread chart inputs into the writer.** Extend `JsonReportWriter.WriteInput` (the shared
   input for all writers) — or pass a precomputed per-finding chart model — so the HTML
   writer can, for each `EvidenceMetric`, obtain (a) the metric's sample series joined from
   `input.Dataset.Series` via `SeriesId`, and (b) the warning/critical thresholds from the
   triggering rule. This join is the core of the work.

2. **Render + embed in `HtmlReportWriter.RenderFinding`.** For each evidence metric, call
   `ScottPlotRenderer.Render(title, series, warningThreshold, criticalThreshold)`,
   base64-encode the canonical SVG, and emit
   `<img class="chart" alt="{metric}" src="data:image/svg+xml;base64,...">` after the
   evidence table. Assign each chart a deterministic `chart_id = "{finding_id}-{series_id}"`.

3. **Honor the limit.** Stop emitting charts once the running count reaches `--chart-limit`
   (CLI) or a fixed default (API). Findings beyond the cap keep their tables.

4. **Wire both paths.** CLI: read `settings.IncludeCharts` / default-on decision in
   `AnalyzeCommand.Execute` (currently ignored) and pass the limit through. API:
   `AnalysisWorker.GenerateAndStoreReportsAsync` passes the same limit/toggle into the
   writer input — no `charts/` directory, no new endpoint, because the SVG is embedded.

5. **Lock determinism with a new HTML golden.** Add a `golden.pal-report.html` per existing
   fixture and an `AssertMatchesGolden`-style byte comparison (modeled on
   `GoldenFixtureTests.AssertMatchesGolden`, `GoldenFixtureTests.cs:212-255`), with masking
   for any machine-variant fields. If JSON `evidence.charts` is left empty, the existing
   JSON goldens need **no** change.

6. **Decide the JSON-report chart contract.** Either leave `evidence.charts` /
   `artifacts.chart_paths` empty (simplest, recommended) or populate `ChartRef` with embed
   metadata — and regenerate the JSON goldens if so.

**What this plan does NOT include**:
- The robust-stats add-on (§6) — separate plan.
- Interactive/JS charts, zoom/pan, or any change to `ScottPlotRenderer`'s rendering.
- A `charts/` directory or any new API endpoint (Option B needs neither).
- Reviving Option C's link model.

**Estimated effort**: S–M. The renderer and canonicalizer are done; the work is the
series/threshold join into the writer input and a one-time golden fixture addition.

---

## 8. Open Questions

### 8a. Default-on or opt-in for HTML charts?

**Current evidence**: `--include-charts` is opt-in and **inert** (`AnalyzeCommand.cs:74-76`,
never read in `Execute`). A self-contained embed (Option B) has no orphaned-file downside,
which is the usual reason to keep chart generation opt-in.

**Proposed answer**: default-on for HTML, with a `--no-charts` escape hatch and an
equivalent API toggle/default. The flag semantics need a deliberate choice (repurpose
`--include-charts`, or invert to `--no-charts`).

**Needs maintainer decision.**

### 8b. Should the JSON/Markdown reports carry chart references too, or HTML only?

**Current evidence**: The schema already has `Evidence.charts` / `ChartRef`
(`pal.report.v1.json:195-236`) and `Artifacts.chart_paths` (`pal.report.v1.json:288-291`),
but both are link-shaped (`artifact_path`) and written by nothing. Markdown viewers often
don't render embedded SVG at all.

**Proposed answer**: Embed in **HTML only** for the first cut; leave `evidence.charts` empty
so the JSON goldens stay untouched (§5). Repurposing `ChartRef` to an embed-friendly shape,
or emitting a PNG for Markdown, are follow-ups.

**Needs maintainer decision** on whether the JSON report should advertise charts at all.

### 8c. PNG fallback for Markdown / email clients?

**Current evidence**: `MarkdownReportWriter` emits a GFM evidence table
(`MarkdownReportWriter.cs:81-87`) and is delivered via `--markdown`, `?format=markdown`, and
`pal remote report --format markdown`. GFM has no reliable inline-SVG support, and many
email clients strip SVG.

**Proposed answer**: Out of scope for the first chart cut (Markdown stays table-only). If
Markdown charts become a requirement, render PNG via ScottPlot's raster export and embed a
`data:image/png;base64` URI — but PNG determinism (no canonicalizer for raster) needs its
own investigation before committing.

**Needs maintainer decision** if Markdown/email chart parity is a product goal.

### 8d. Chart size budget for large reports?

**Current evidence**: `--chart-limit` defaults to 20 (`AnalyzeCommand.cs:80`). Each 720×360
line SVG is small, but base64 (Option B) adds ~33%, and the API stores the whole HTML as a
single blob (`AnalysisWorker.cs:246-249`) — `SaveReportAsync` records the byte length, so an
oversized report is observable but uncapped.

**Proposed answer**: Keep the 20-chart cap as the size guardrail for the first cut; if
reports balloon, lower the default or switch large reports to Option A (inline, no base64
overhead). Add a soft byte-size warning if a report exceeds a threshold.

**Needs maintainer decision** on a hard size cap vs. the chart-count cap as the sole lever.

### 8e. Inline (A) vs. data-URI (B) — final call?

**Current evidence**: Both are self-contained and work across delivery paths. A is smaller
but risks `pal-N` id collisions across multiple inlined charts (the canonicalizer restarts
its counter per render, `SvgCanonicalizer.cs:33-41`) and needs the XML declaration stripped;
B is collision-free and simpler but ~33% larger.

**Proposed answer**: B for the first cut (simplicity + no id-collision risk); revisit A if
size becomes a problem, adding an id-prefix parameter to `SvgCanonicalizer`.

**Needs maintainer decision** if the size budget is tight from day one.

---

## 9. Non-Goals

The following are explicitly out of scope for the chart-embedding work and should not be
folded in without a separate ADR:

- **Interactive / JavaScript charts.** No client-side charting library, no `<script>`, no
  zoom/pan/tooltip/brush. The report stays a static, self-contained document (it has no
  `<script>` today, `HtmlReportWriter.cs:30-88`). Interactivity is a Phase 2 web-consumer
  concern, not a report-writer concern.

- **Changing the ScottPlot rendering itself.** Plot dimensions, palette, axis style, and the
  canonicalization rules (`ScottPlotRenderer.cs:9-91`, `SvgCanonicalizer.cs`) are frozen and
  golden-tested; this work consumes the renderer's output, it does not modify the renderer.

- **A `charts/` directory or chart-serving endpoint.** Option B (and A) embed bytes; there is
  deliberately no on-disk artifact and no new API route. Reviving Option C's link model is a
  separate decision with the API-download breakage it implies.

- **The robust-stats trimmed-means add-on (§6).** Independent change set touching the engine,
  schema, and all three writers plus a JSON golden regeneration — its own plan.

- **Per-series charts for non-triggering metrics / a full dataset gallery.** Only triggering
  evidence metrics get charts in the first cut; a top-level "all counters" gallery is a
  larger UX decision deferred until the per-finding charts prove their value.

- **PNG/raster output and Markdown/email chart parity.** SVG-only, HTML-only for the first
  cut (see Q8c).
