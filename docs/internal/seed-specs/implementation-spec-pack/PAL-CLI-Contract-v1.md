# PAL CLI Contract v1

## Purpose

This document defines the command-line interface contract for PAL 2026 Phase 1.

The CLI is the primary user-facing surface in Phase 1.
It must be:
- scriptable
- deterministic
- Windows-friendly
- clear when things fail
- stable enough for support workflows and automation

Executable name used below:
- `pal`

---

## Command overview

Phase 1 commands:

- `pal analyze`
- `pal validate-pack`
- `pal inspect-dataset`
- `pal list-packs`
- `pal version`
- `pal help`

---

## Global behavior

### Exit codes

- `0` success
- `1` general failure
- `2` invalid arguments
- `3` input/collector failure
- `4` pack validation failure
- `5` analysis execution failure
- `6` report generation failure

### Global options

These should work on all commands where relevant.

```text
--verbose
--log-file <path>
--no-color
--working-dir <path>
--help
```

### Output conventions
- concise human-readable output to stdout
- errors to stderr
- machine-readable artifacts written to files, not stdout, unless later extended

---

## `pal analyze`

Analyze one input dataset and generate report artifacts.

### Usage

```text
pal analyze --input <path> --output <dir> [options]
```

### Required options

```text
--input <path>
--output <dir>
```

### Optional options

```text
--format <auto|blg|csv>
--pack <pack-id>                 (repeatable)
--pack-dir <path>                (repeatable)
--auto-resolve-packs
--html                           (default true)
--json                           (default true)
--html-only
--json-only
--include-charts
--chart-limit <n>
--fail-on-warning
--machine-name <name>
--time-zone <tz>
--report-name <name>
```

### Option behavior

#### `--format`
Default: `auto`

Allowed:
- `auto`
- `blg`
- `csv`

#### `--pack`
Repeatable.
Explicitly load one or more pack IDs.

Example:
```text
--pack windows-core --pack iis-core
```

#### `--pack-dir`
Repeatable.
Additional search path for packs.

#### `--auto-resolve-packs`
If set, engine attempts to load applicable packs based on dataset content.
If no `--pack` is specified, Phase 1 should still auto-load `windows-core` by default.

#### `--html`, `--json`
Control output artifact types.

#### `--html-only`, `--json-only`
Convenience flags.
Mutually exclusive.

#### `--include-charts`
Emit chart artifacts and link them in HTML/JSON if findings reference charts.

#### `--chart-limit`
Default recommended value: `20`

#### `--fail-on-warning`
If any warning is produced, return exit code `1` even if analysis otherwise succeeds.

#### `--machine-name`
Override machine name when source metadata is missing or misleading.

#### `--time-zone`
Override or assign the logical source time zone.

#### `--report-name`
Base name for generated artifact files.

---

## `pal analyze` examples

### Basic BLG analysis
```text
pal analyze --input C:\logs\server01.blg --output C:\out
```

### Explicit pack selection
```text
pal analyze --input C:\logs\sql01.blg --output C:\out --pack windows-core --pack sql-host-core
```

### CSV analysis with charts
```text
pal analyze --input C:\logs\sample.csv --format csv --output C:\out --include-charts
```

### Additional pack directory
```text
pal analyze --input C:\logs\web01.blg --output C:\out --pack-dir C:\pal-packs --auto-resolve-packs
```

---

## `pal analyze` console output example

```text
PAL 2026.1.0
Input: C:\logs\server01.blg
Collector: BLG
Output: C:\out

Importing dataset...
Normalizing counters...
Resolved packs: windows-core, sql-host-core
Executing 24 rules...
Findings: 5 (1 critical, 3 warning, 1 informational)

Wrote:
- C:\out\server01.pal-report.json
- C:\out\server01.pal-report.html

Completed in 18.3s
```

---

## `pal analyze` error example

```text
ERROR: Pack validation failed for 'sql-host-core'
Reason: Recommendation ID 'collect-waits' referenced by rule 'low-ple' was not found.
```

---

## `pal validate-pack`

Validate one pack or a directory of packs.

### Usage

```text
pal validate-pack --path <path>
```

### Required options

```text
--path <path>
```

### Optional options

```text
--strict
--json-output <path>
```

### Behavior
- validates schema compliance
- validates file references
- validates expressions
- validates duplicate IDs and unresolved references
- returns exit code `4` on validation failure

### Example

```text
pal validate-pack --path C:\pal\packs\windows-core
```

### Example output

```text
Pack: windows-core
Schema: pal.pack/v1
Rules: 8
Status: valid
Warnings: 1
```

---

## `pal inspect-dataset`

Import and inspect a dataset without running rules.

### Usage

```text
pal inspect-dataset --input <path> [options]
```

### Required options
```text
--input <path>
```

### Optional options
```text
--format <auto|blg|csv>
--output <path>
--machine-name <name>
--time-zone <tz>
```

### Behavior
Emits a summary of:
- source type
- machine name
- capture time range
- inferred sample interval
- series count
- top canonical metrics
- gaps and import warnings

If `--output` is provided, write JSON inspection metadata.

### Example

```text
pal inspect-dataset --input C:\logs\server01.blg
```

### Example output

```text
Dataset inspection
Source type: blg
Machine name: SERVER01
Time range: 2026-04-21T18:00:00Z to 2026-04-21T19:00:00Z
Sample interval: 15s
Series count: 86
Gap count: 2
Top metrics:
- windows.processor.% processor time[instance=_total]
- windows.memory.available mbytes
- windows.logicaldisk.avg. disk sec/read[instance=c:]
```

---

## `pal list-packs`

List all packs available on the default and user-supplied search paths.

### Usage

```text
pal list-packs [options]
```

### Optional options

```text
--pack-dir <path>     (repeatable)
--json-output <path>
```

### Example output

```text
Available packs
- windows-core      1.0.0
- iis-core          1.0.0
- sql-host-core     1.0.0
```

---

## `pal version`

### Usage

```text
pal version
```

### Output

```text
PAL 2026.1.0
Runtime: .NET 8.0
```

---

## `pal help`

### Usage

```text
pal help
pal help analyze
```

---

## Output file naming

Default artifact file names should be:

```text
<report-name>.pal-report.json
<report-name>.pal-report.html
```

If `--report-name` is omitted, derive from input file stem.

Examples:
- input `server01.blg` -> `server01.pal-report.json`
- input `web01.csv` -> `web01.pal-report.html`

Charts, if enabled:

```text
<output>\charts\<report-name>-<chart-id>.svg
```

---

## Pack search order

When resolving packs, search in this order:

1. explicit `--pack-dir` values, in given order
2. built-in pack directory adjacent to executable
3. current working directory `.\packs`

First matching `pack.id` wins unless duplicate versions are detected.
If duplicate pack IDs are found on the search path, fail validation unless future precedence rules are added.

---

## Mutually exclusive and invalid combinations

Invalid:
- `--html-only` with `--json-only`
- `--html-only` with `--html false` if boolean syntax later supported
- unknown pack IDs
- missing input path
- output path equal to input file path

CLI must fail fast with argument error exit code `2`.

---

## Determinism requirements

The CLI must:
- sort findings consistently
- sort packs consistently
- use stable IDs where required inside one run
- not depend on system locale for decimal parsing or formatting
- write UTF-8 without BOM unless there is a specific Windows interoperability reason

Finding sort order:
1. severity descending
2. category ascending
3. rule ID ascending
4. finding ID ascending

---

## Logging

### Default
Human-readable progress only.

### Verbose
Include:
- collector diagnostics
- normalization warnings
- pack resolution details
- per-rule execution counts

### Log file
If `--log-file` is set, write full execution log there in addition to console output.

---

## Future compatibility

Phase 2 may add:
- `pal serve`
- `pal compare`
- `pal collect`
- `pal export`

Phase 1 command names and key options should be chosen to avoid collisions with that future expansion.
