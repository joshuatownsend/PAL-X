---
title: Glossary
description: Terms you'll meet in PAL-X reports, packs, and APIs.
---

# Glossary

Every term in this list shows up in either the report JSON, the pack YAML, the API, or the CLI. They're listed alphabetically.

### Aggregation

A function that reduces a series of metric samples to a single value before applying an operator. The pack schema supports `avg`, `min`, `max`, `p90`, `p95`, `p99`. Schema v1 applies the aggregation across the entire dataset; schema v1.1 adds an optional [window](#window) for rolling-window evaluation. See [ADR 0004](../architecture/adr/0004-schema-v1.1-rolling-windows.md).

### Applicability

The conditions under which a pack should auto-load. Specified at the pack's top level. Three modes: `always`, `requires_any: [<metric-id>, ...]`, `requires_all: [<metric-id>, ...]`. The CLI honors applicability when you pass `--auto-resolve-packs`.

### Baseline

A completed analysis job designated as the reference for future comparisons. Baselines carry a **type** (`machine`, `role`, `workload`, `release`) and an arbitrary JSON **context** (e.g. `{"machine": "WEB-01"}`). Multiple baselines sharing the same `(type, context)` are treated as versions, newest first.

### BLG

Binary log format produced by Windows Performance Monitor. PAL-X ingests `.blg` files natively on Windows via the PDH (Performance Data Helper) API. On Linux/macOS, convert first with `relog -f CSV server.blg -o server.csv` from a Windows machine.

### Category

A high-level grouping for findings — `cpu`, `memory`, `disk`, `network`, `system`, `iis`, `sql`, and so on. Rules belong to a category; the report's per-category status grid summarizes the worst severity in each.

### Comparison

A pairwise diff between two completed jobs. Surfaces which findings improved, worsened, appeared, or disappeared. Often run against a baseline (auto-triggered on job completion via `selectedBaselineId`), but any two jobs can be compared manually via `POST /compare`.

### Condition

The rule clause that decides whether a finding fires. Always declarative: a tuple of `metric`, `instance`, `aggregation`, `operator`, `threshold`, `duration_percent`, optionally `window` (v1.1). No expression DSL. See [ADR 0002](../architecture/adr/0002-declarative-rule-schema.md).

### Correlation

A relationship inferred between two metric trajectories across multiple jobs in the same workspace. The analytics surface flags pairs whose worsening (or appearing) findings co-occur, with a direction marker.

### Critical

The highest severity. Indicates a measured threshold breach severe enough that PAL-X marks the overall status `critical`. See [Severity](#severity).

### Dataset

The in-memory representation of a parsed perfmon capture: timestamps + counter values, normalized to PAL-X's canonical [metric IDs](#metric-id). One CSV or BLG file produces one dataset.

### Diagnostic insight

A rule-based, fully cited inference about a job — produced by `IDiagnosticsService`. Examples: "memory pressure is worsening because all of A, B, and C metrics are degrading and have appeared in correlation pairs together." Every insight cites the rules and metric sources behind it. No black-box inference.

### Duration percent

The fraction of the evaluation window during which a rule's condition must hold for a finding to fire. `duration_percent: 50` with `aggregation: avg` and `operator: gt` over a 10-minute window means the average has to exceed the threshold for at least 5 minutes of the window. Stops single-sample spikes from triggering rules.

### Evidence

The data attached to a finding showing why it fired: the metric, the aggregated value, the trigger details (timestamps and observed values), and the host-context values used to resolve any RAM- or CPU-relative threshold.

### Finding

A single rule that fired against the dataset. Carries `rule_id`, `severity`, `category`, `title`, summary, explanation, evidence, and a list of recommendations. The report's sorted findings list is the primary deliverable.

### Finding ID

A SHA-256-derived content hash uniquely identifying a finding. Same inputs + same packs + same rule version produce the same finding ID — making findings stable across reruns and across machines.

### Host context

Metadata about the source machine: `total_physical_memory_mb`, `logical_processor_count`. Used to resolve thresholds expressed relative to host (e.g. "available memory < 5% of total"). Supplied via `--host-memory-mb` / `--host-cpu-count` flags, an adjacent `host-context.json` sidecar, or auto-discovered in the BLG when present. Unknown values cause RAM- or CPU-relative rules to emit an informational warning and be skipped.

### Informational

The lowest severity level. Doesn't affect overall status. Used for rules that surface noteworthy context (e.g. "RAM-relative rule X was skipped because host context is missing").

### Job

A single analysis run on the hosted API. Carries the upload reference, requested packs, host-context overrides, baseline link (if comparison was requested), and ultimately the report. Identified by a GUID.

### Metric ID

PAL-X's canonical, snake_case identifier for a performance counter. Example: `processor.percent_processor_time` corresponds to the Windows counter `\Processor(_Total)\% Processor Time`. The mapping from canonical IDs to actual counter paths lives in each pack's `metric_aliases` block.

### Operator

The comparator in a condition. Supported: `gt`, `gte`, `lt`, `lte`, `eq`. Applied between the aggregated metric value and the threshold.

### Org

The outer multi-tenancy boundary. Contains workspaces and members. Most API calls scope inside a workspace, but org-level endpoints handle membership and workspace lifecycle.

### Pack

A YAML file describing rules. Identified by `pack_id`, versioned with `version`, optionally signed. Ships in `packs/thresholds/` (`windows-core`, `iis-core`, `sql-host-core`). You can add your own; see the pack authoring guide *(coming soon)*.

### Pack signing

Optional integrity guarantee. Packs can be signed with RSA-PSS-SHA256 (`pal packs sign --pack <dir> --key <privkey.pem>`); the signature is stored adjacent as `pack.yaml.sig`. Verification is enforced by `pal validate-pack --require-signature --trust-key <pubkey.pem>` and by the `PackLoader` when configured with `SignatureRequirement.Required`. See [ADR 0003](../architecture/adr/0003-pack-signing-format.md).

### Recommendation

A remediation hint attached to a rule. Each recommendation carries `priority`, `text`, optional links. A rule references recommendations by ID; the same recommendation object can be reused across multiple rules.

### Report

The artifact produced by an analysis. Three formats: JSON (the canonical form, schema-versioned at `pal.report/v1`), HTML (self-contained, opened in a browser), Markdown (optional, via `--markdown`). All three are written to the output directory using UTF-8 without BOM.

### Report ID

A SHA-256-derived content hash uniquely identifying a report. Same inputs + same packs produce the same report ID.

### Rule

A single threshold check. Identified by `rule_id`, scoped to a `category` and `severity`. A rule has one or more conditions (all must hold for the finding to fire) and references zero or more recommendations.

### Severity

The fired rule's impact level: `critical` > `warning` > `informational`. The report's `overall_status` is the maximum severity present (with `informational` mapped to `healthy`).

### Trends

The analytics surface that tracks metric trajectories across multiple jobs in the same workspace. Each metric is classified as `improving`, `stable`, `worsening`, or `appearing`.

### Window

A schema v1.1 addition to conditions. Specifies a rolling window over which the aggregation is computed before the operator is applied — e.g. "avg over a 5-minute window, sliding by 1 minute". Supported aggregations: `avg`, `min`, `max`, `p90`, `p95`, `p99` (not `trend`). See [ADR 0004](../architecture/adr/0004-schema-v1.1-rolling-windows.md).

### Workspace

The inner multi-tenancy boundary. Carries analysis jobs, packs, alerts, schedules, webhooks. The API enforces workspace isolation via EF Core global query filters and DB-level cascade constraints. Every workspace-scoped route lives under `/api/workspaces/{workspaceId}`.
