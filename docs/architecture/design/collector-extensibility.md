# Collector Extensibility Design

> **Status**: spike / pre-ADR  
> **Author**: design spike (plan 005), 2026-06-13  
> **Next step**: maintainer acceptance promotes a chosen option to an ADR under
> `docs/architecture/adr/000N-collector-extensibility.md`

---

## 1. Current State

### Contract — `IDatasetCollector`

`dotnet/src/Pal.Ingestion/IDatasetCollector.cs` defines the full public contract:

```csharp
public interface IDatasetCollector
{
    bool CanHandle(string filePath);
    CollectResult Collect(string filePath, string? machineName = null, string? timeZone = null);
}

public sealed class CollectResult
{
    public required Dataset Dataset { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required string InputDigest { get; init; }
}
```

The interface is clean and self-contained. `CanHandle` provides a content/extension-based
self-selection hook; `Collect` returns a `Dataset` (the engine's input model) plus an
SHA-256 hex digest of the raw file bytes (`InputDigest`, used for deduplication and
report-ID derivation).

### Factory — `CollectorFactory`

`dotnet/src/Pal.Ingestion/CollectorFactory.cs` dispatches by an externally-supplied
format string, ignoring `CanHandle` entirely:

```csharp
public static IDatasetCollector Create(string format, MetricAliasRegistry registry) =>
    format.Trim().ToLowerInvariant() switch
    {
        "blg" => CreateBlgCollector(registry),
        _ => new CsvCollector(registry)        // CSV is the fallback default
    };
```

This is a hardcoded switch — not a registry.

**Key asymmetry**: The interface promises self-selection (`CanHandle`), but the factory
never asks a collector whether it can handle the file. The `format` string originates
externally (see callers below) and must match a hardcoded branch. If a future format is
not added to the switch, the factory silently routes it to `CsvCollector`, which will
fail with a confusing parse error rather than a "format not supported" message.

### Platform guard pattern

`BlgPlatformGuard` (`dotnet/src/Pal.Ingestion/Blg/BlgPlatformGuard.cs`) is a
non-Windows stand-in: `CanHandle` returns `false`; `Collect` throws
`PlatformNotSupportedException` with a `relog -f CSV` hint. The factory substitutes it
for `BlgCollector` when `OperatingSystem.IsWindows()` is false. This is a useful pattern
but currently applies only to BLG.

### Reference implementations

| Collector | File | `CanHandle` trigger | Platform | Registry used? |
|-----------|------|---------------------|----------|----------------|
| `CsvCollector` | `Csv/CsvCollector.cs` | `.csv` extension | All | yes — `_registry.Resolve(counterPath)` |
| `BlgCollector` | `Blg/BlgCollector.cs` | `.blg` extension | Windows-only (PDH) | yes — same |
| `BlgPlatformGuard` | `Blg/BlgPlatformGuard.cs` | never (returns false) | All (throws) | no |

Both real collectors call `_registry.Resolve(path)` to map raw counter paths to
canonical snake_case metric IDs. They fall back to `"unknown." + SanitizePath(path)` for
unmapped counters.

### Callers of `CollectorFactory.Create`

`git grep -n "CollectorFactory.Create"` reveals three call sites:

| File | `format` source |
|------|----------------|
| `dotnet/src/Pal.Application/Analysis/AnalysisRunner.cs:14` | `request.InputFormat` — a string from `AnalysisRunRequest` |
| `dotnet/src/Pal.Cli/Commands/InspectDatasetCommand.cs:42` | `--format` option (default `"auto"`), expanded to file extension via `Path.GetExtension(...).TrimStart('.').ToLowerInvariant()` |
| `dotnet/tests/Pal.Ingestion.Tests/CsvCollectorTests.cs:82,90` | literal `"csv"` / `"blg"` in tests |

`AnalysisRunRequest.InputFormat` itself is populated from two entry points:

- **CLI** (`AnalyzeCommand.cs:130-145`): `--format auto` (the default) resolves to the
  file extension. Explicit values like `--format csv` pass the string verbatim.
- **API** (`AnalysisWorker.cs:101`, `UploadEndpoints.cs:20-21`): `upload.SourceType`
  persisted in `UploadEntity.SourceType` (PostgreSQL column `source_type`). On upload,
  `sourceType` defaults to the file extension when the `sourceType` form field is absent
  (`UploadEndpoints.cs:20-21`).

So the `format` string that reaches `CollectorFactory.Create` is always a file-extension
string (`csv`, `blg`) in normal usage.

### Report output — `source_type` and `collector` fields

`JsonReportWriter.cs:56,83-87` derives both output fields from the file extension of the
input path, not from the collector itself:

```csharp
// JsonReportWriter.cs lines 56 and 83-87
string sourceType = Path.GetExtension(input.InputPath).TrimStart('.').ToLowerInvariant();
// ...
source_type = sourceType,
collector = $"Pal.Collectors.{char.ToUpperInvariant(sourceType[0])}{sourceType[1..]}",
collector_version = "1.0.0"
```

This produces `"Pal.Collectors.Csv"` or `"Pal.Collectors.Blg"` by convention, but the
collector class has no property that declares its own format ID. If a third collector is
added, the report fields will only be correct if the input file happens to use the right
extension — a brittle implicit contract.

---

## 2. Goal

Adding a new input format (e.g., a `JsonCollector` for a documented JSON shape such as
Windows Performance Monitor JSON export) should cost:

- **One new class** implementing `IDatasetCollector` (with constructor injection of
  `MetricAliasRegistry`).
- **One registration point** — a single place in the ingestion layer where the factory or
  registry learns about the new collector.
- **Tests modeled on `CsvCollectorTests`** — unit tests with a fixture file and
  `CollectorFactory.Create("json", ...)` (or DI resolution) returning the new collector.
- **Zero changes** to caller code in `Pal.Application`, `Pal.Cli`, or `Pal.Api` for the
  common path. The format string `"json"` should just work.

A concrete target: a `JsonCollector` that reads a JSON array of counter samples in a
documented schema (format ID `"json"`), returning a `CollectResult` with the same
`Dataset` model as the CSV and BLG collectors. This target is used as a test case in
the option evaluations below.

---

## 3. Design Options

### Option A — Extend the static switch (lowest change)

Extend the `format` switch in `CollectorFactory.Create` with an additional branch per
format:

```csharp
"json" => new JsonCollector(registry),
"blg"  => CreateBlgCollector(registry),
_      => new CsvCollector(registry)
```

**Pros**:
- Zero infrastructure change. Adding a collector is a one-liner in a single file.
- Compile-time complete: every recognized format is visible at a glance.
- No DI plumbing needed for the CLI, which constructs collectors directly.

**Cons**:
- The collector switch lives in `Pal.Ingestion` but callers in `Pal.Application` and the
  CLI must know and supply the right format string — no central validation that the string
  is valid.
- The BLG platform guard is a one-off; if a future collector also has a platform
  constraint, the guard logic must be duplicated per-format in the switch.
- `CanHandle` remains unused by the factory — the asymmetry persists and may confuse
  contributors.
- The `collector` field in the JSON report is derived from the file extension, not the
  factory branch — the coupling is still implicit.

**Verdict**: Sufficient for one or two additional formats added by the maintainer team.
Not appropriate if a third-party or community contribution model is desired.

### Option B — DI-based registry with `CanHandle` selection (most extensible)

Register all collectors with the DI container as `IEnumerable<IDatasetCollector>`. A
`CollectorResolver` service iterates the registered collectors and calls `CanHandle` to
select one:

```csharp
public sealed class CollectorResolver(IEnumerable<IDatasetCollector> collectors)
{
    public IDatasetCollector Resolve(string filePath)
    {
        return collectors.FirstOrDefault(c => c.CanHandle(filePath))
            ?? throw new NotSupportedException($"No collector can handle: {filePath}");
    }
}
```

`CanHandle` implementations use the file extension (or content sniffing if desired).
The format string becomes optional — the file path alone is sufficient for selection.

**Pros**:
- `CanHandle` is finally used; the interface asymmetry is resolved.
- Adding a collector is adding a DI registration — no change to any existing source file.
- Platform guards compose naturally as decorator collectors or conditional registrations.
- Lays the groundwork for a future out-of-tree plugin model (assembly scanning).
- The `AnalysisRunner` can be injected with `CollectorResolver` rather than calling
  `CollectorFactory.Create`, eliminating the factory entirely from the API path.

**Cons**:
- The CLI (`Pal.Cli`) currently constructs `AnalysisRunner` and its dependencies
  manually, without a DI container. Introducing DI here requires either wiring up a
  `ServiceCollection` in the CLI's `Program.cs` or keeping a parallel static path.
- Ordering of `CanHandle` calls matters if two collectors can handle the same extension
  — requires a documented priority.
- Adds a layer of indirection where the static switch was self-evidently complete.

**Verdict**: Right design for the API path. Requires a decision about CLI DI (see open
questions).

### Option C — Hybrid: DI registry for the API, static extension for the CLI (recommended)

Keep the static `CollectorFactory` switch for the CLI path but replace it with an open
list instead of a closed switch. For the API, move to a DI-registered list consumed by
`CollectorResolver`. `AnalysisRunner` accepts `IDatasetCollector` directly (injected) for
the API path; the CLI constructs the collector via the factory before passing the runner.

Concretely:

1. **Open the factory** — change `CollectorFactory` to a `CollectorRegistry` with a
   static list of `(string formatId, Func<MetricAliasRegistry, IDatasetCollector>)`
   entries. `CollectorRegistry.Create("json", registry)` resolves from the list; an
   unknown `formatId` throws `NotSupportedException` (not a silent CSV fallback).
2. **Wire DI in the API** — register each collector factory entry as an
   `IDatasetCollector` in `Pal.Api/Program.cs`. `CollectorResolver` uses
   `IEnumerable<IDatasetCollector>` and `CanHandle`.
3. **`AnalysisRunRequest` grows optional `InputPath` resolution** — when `AnalysisRunner`
   receives an `IDatasetCollector` from DI, it skips the factory call entirely. This is
   a compile-time seam, not a runtime branch.

**Pros**:
- No CLI DI changes required in this phase.
- The API path is clean and injectable — `AnalysisWorker` benefits immediately.
- Format string validation is explicit (no silent fallback to CSV).
- `CanHandle` is exercised in the API path; the CLI still passes an explicit format string.
- Adding a collector is still a single class + one registration in `CollectorRegistry`
  entries + one DI registration in the API — three lines total across two files.

**Cons**:
- Two parallel selection mechanisms (factory for CLI, DI for API) until the CLI grows
  its own DI bootstrapping in a future phase.
- The factory and resolver must stay in sync on format IDs — a mismatch would cause the
  CLI and API to behave differently.

**Recommended**: Option C. It moves the design forward without requiring the CLI to adopt
DI in this phase, keeps the silent-fallback hazard eliminated, and aligns the API path
with testable, injectable components. The CLI can migrate to DI when `Pal.Cli` picks up a
`ServiceCollection` (likely with Phase 2).

---

## 4. Open Questions

### 4a. Selection by file extension vs. content sniffing vs. explicit `format`?

**Current evidence**: Both `CsvCollector.CanHandle` and `BlgCollector.CanHandle` use file
extension only (`Path.GetExtension(...).Equals(".csv" | ".blg")`). The CLI's `--format
auto` also resolves to extension. Content sniffing is not used anywhere.

**Proposed answer**: Extension-based selection is sufficient for Phase 1 formats. Content
sniffing (reading the first N bytes) should be reserved for formats where extension is
unreliable (e.g., `.json` is too broad if the project gains multiple JSON sub-formats).
Maintain extension-based `CanHandle` as the default; document how to override for
ambiguous extensions.

**Needs maintainer decision** if a format is introduced with no reliable extension
(e.g., stdin streaming, HTTP fetch).

### 4b. How does a collector declare the `source_type`/`collector` fields in the report?

**Current evidence**: `JsonReportWriter.cs:56` derives `source_type` from the input file
extension, not from the collector. The `collector` field is constructed by string
capitalisation of `sourceType` (line 86). There is no property on `IDatasetCollector`
declaring a format ID.

**Proposed answer**: Add a `string FormatId { get; }` property to `IDatasetCollector`.
The `JsonReportWriter` (and any future report writers) should read the format ID from the
collector rather than the file extension. This eliminates the implicit contract and makes
the report correct even if the file extension is non-standard. For the static-factory
path, the factory can expose the same `FormatId` per entry.

**This is a small, low-risk interface addition** and should be part of the first
collector follow-up plan.

### 4c. Platform-guarded collectors — is the pattern generalized?

**Current evidence**: `BlgPlatformGuard` is hand-coded in the factory's
`CreateBlgCollector` private method. No general mechanism exists for "this collector is
Windows-only."

**Proposed answer**: The `CollectorRegistry` (Option C) should accept an optional
platform predicate alongside the factory function:

```csharp
registry.Register("blg",
    factory:   r => new BlgCollector(r),
    platformOk: () => OperatingSystem.IsWindows(),
    fallback:   _ => new BlgPlatformGuard());
```

This makes every platform-guarded collector self-describing. The pattern is already
correct in spirit; it only needs to be lifted out of `CollectorFactory`'s private helper
and into the registry's contract.

**Needs maintainer decision** on whether the platform check belongs in the registry entry
(as above) or in `CanHandle` itself (e.g., `BlgCollector.CanHandle` returns `false` on
non-Windows, and the registry selects the guard as a lower-priority fallback).

### 4d. Third-party / out-of-tree collectors, or in-repo only?

**Current evidence**: All collectors are compiled into `Pal.Ingestion`. There is no
assembly-scanning, plugin directory, or stable public interface marker.

**Proposed answer**: In-repo only for Phase 1 and Phase 2. The `IDatasetCollector`
interface is already a natural public seam but is not versioned or decorated with a
stability guarantee. If an out-of-tree plugin model becomes a requirement, the right
mechanism is a separate `Pal.Ingestion.Extensibility` package with a stable versioned
interface and MEF or `AssemblyLoadContext` scanning — this is a substantial scope
increase. See Non-Goals (section 6).

**Needs maintainer decision** if community-contributed collectors are a product goal within
the next two phases.

### 4e. `MetricAliasRegistry` source for a new collector — can a collector contribute aliases?

**Current evidence**: `MetricAliasRegistry.BuildDefault()` is called in two places —
`AnalysisRunner.cs:13` (one call per job) and each CLI command that needs a collector.
`MetricAliasRegistry.AddFromPack` already allows a pack to contribute aliases at
analysis time. Neither `IDatasetCollector` nor `CollectorFactory` exposes a mechanism for
a collector to contribute aliases before `BuildDefault` is called.

**Proposed answer**: Collectors should not be responsible for alias registration. Aliases
belong in packs (using `metric_aliases:` in `pack.yaml`), which is the existing pattern
for format-specific counter paths. A `JsonCollector` targeting a well-known JSON export
format would ship with a companion pack that covers its counter naming conventions.

If a collector genuinely needs to contribute aliases that cannot be expressed in a pack
(e.g., a collector that discovers counter names dynamically from the file's metadata),
`IDatasetCollector` should expose:

```csharp
void ContributeAliases(MetricAliasRegistry registry);  // optional; default = no-op
```

This is low-cost to add but should be deferred until a concrete case arises.

**Proposed answer**: Keep alias registration in packs; defer the `ContributeAliases`
hook to the first follow-up plan that actually needs it.

---

## 5. Recommended First Step

This is a scoped outline for a follow-up implementation plan (not executed here).

**Title**: "Implement `JsonCollector` under Option C skeleton"

**Scope**:

1. Add `string FormatId { get; }` to `IDatasetCollector` (answers Q4b). Implement
   `FormatId = "csv"` in `CsvCollector` and `FormatId = "blg"` in `BlgCollector` /
   `BlgPlatformGuard`. Update `JsonReportWriter` to read from the collector rather than
   the file extension.

2. Replace `CollectorFactory` (closed switch, silent CSV fallback) with a
   `CollectorRegistry` that:
   - Stores `(string formatId, Func<MetricAliasRegistry, IDatasetCollector> factory,
     Func<bool>? platformOk, Func<MetricAliasRegistry, IDatasetCollector>? fallback)` entries.
   - Throws `NotSupportedException` for unrecognized format IDs.
   - Keeps the same public surface: `CollectorRegistry.Create(string format,
     MetricAliasRegistry registry)`.

3. Implement `JsonCollector` for a minimal documented JSON shape — e.g.:
   ```json
   {
     "machine": "WEB-01",
     "samples": [
       { "timestamp": "2026-01-01T00:00:00Z", "counter": "\\Server\\% Processor Time", "value": 42.1 },
       ...
     ]
   }
   ```
   Format ID: `"json"`. Extension: `.json`. Platform: all.

4. Register in `CollectorRegistry`; wire as DI registration in `Pal.Api/Program.cs` for
   the API path.

5. Write `JsonCollectorTests` modeled on `CsvCollectorTests`, with a fixture file under
   `fixtures/json-sample/input.json`.

**What this plan does NOT include**:
- CLI DI migration.
- Content sniffing.
- `ContributeAliases` hook.
- Platform guard generalization (deferred to when a second platform-guarded format lands).

**Estimated effort**: S-M (the interface change and registry replacement are the most
mechanical parts; the collector itself is modest given the JSON shape is controlled by us).

---

## 6. Non-Goals

The following are explicitly out of scope for the collector extensibility work and should
not be folded in without a separate ADR:

- **Dynamic assembly loading / plugin marketplace**: MEF, `AssemblyLoadContext`-based
  scanning, or NuGet-distributed collector packages. These require a stable versioned
  public API, security sandboxing considerations, and a distribution mechanism that does
  not exist in Phase 1.

- **Streaming / real-time collectors**: The `Collect` contract is synchronous and
  file-path-based. Adapting it for live Prometheus scrapes or CloudWatch streaming would
  require a different async interface (`IAsyncDatasetCollector`). This is a separate
  design problem.

- **Multi-file / directory collectors**: The current contract takes a single `filePath`.
  Collectors that aggregate across a directory of files (e.g., a directory of per-hour
  BLG files) need a different API shape.

- **Schema-coupled metric normalization**: Collectors today delegate normalization to
  `MetricAliasRegistry`. A collector that intrinsically knows its own metric schema (e.g.,
  a Prometheus scrape has canonical names already) does not need the registry at all. That
  tension is deferred until a concrete case presents itself.
