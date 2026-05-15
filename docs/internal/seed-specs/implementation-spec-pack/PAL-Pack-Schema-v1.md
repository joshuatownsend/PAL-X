# PAL Pack Schema v1

## Purpose

This document defines **pack schema v1** for PAL 2026.

A pack is a versioned content bundle that describes:
- what product or workload it applies to
- how to map raw counters into canonical metrics
- what rules to evaluate
- how to explain findings
- what recommendations to provide

Packs are content, not code.

---

## Design goals

- human-readable
- versionable in Git
- strict enough for automated validation
- expressive enough for Phase 1 rules
- safe to load without arbitrary code execution
- portable across CLI and future API/service runtimes

---

## File layout

Each pack lives in its own directory.

Example:

```text
/windows-core
  pack.yaml
  /rules
    cpu.yaml
    memory.yaml
    disk.yaml
  /content
    recommendations.yaml
    references.yaml
```

Minimum required file:
- `pack.yaml`

Phase 1 may allow all content inline inside `pack.yaml` for simplicity, but the engine must also support external rule files under `/rules`.

---

## Top-level schema

```yaml
schema_version: "pal.pack/v1"
pack:
  id: "windows-core"
  name: "Windows Core"
  version: "1.0.0"
  description: "Core Windows server health rules"
  publisher: "PAL Team"
  license: "MIT"
  homepage: "https://example.org/pal/packs/windows-core"
  tags: ["windows", "os", "core"]
  target_products:
    - product: "windows-server"
      versions: ["2016", "2019", "2022", "2025"]
  applicability:
    requires_any_metrics:
      - "windows.processor.% processor time[instance=_total]"
      - "windows.memory.available mbytes"
  aliases:
    - from: "\\Processor(_Total)\\% Processor Time"
      to: "windows.processor.% processor time[instance=_total]"
  rules:
    - file: "rules/cpu.yaml"
    - file: "rules/memory.yaml"
```

---

## Top-level fields

### `schema_version`
Required string.

Allowed value in Phase 1:
- `pal.pack/v1`

### `pack`
Required object.

Fields:
- `id` required, stable machine-readable identifier
- `name` required
- `version` required, semver string
- `description` required
- `publisher` optional
- `license` optional
- `homepage` optional
- `tags` optional array of strings
- `target_products` optional array
- `applicability` optional object
- `aliases` optional array
- `rules` required array

---

## Pack ID rules

`pack.id` must:
- be lowercase
- use letters, digits, and hyphens only
- be unique within a run
- not change once published unless intentionally forked

Examples:
- `windows-core`
- `iis-core`
- `sql-host-core`

---

## Versioning

Use semantic versioning.

### Patch
For:
- typos
- clarification text
- non-semantic content fixes

### Minor
For:
- new rules
- new aliases
- non-breaking metadata additions

### Major
For:
- changed rule semantics
- removed rules
- schema-breaking content changes

---

## Target products

Used for metadata and future compatibility logic.

Schema:

```yaml
target_products:
  - product: "sql-server"
    versions: ["2017", "2019", "2022"]
    editions: ["standard", "enterprise"]
```

Fields:
- `product` required
- `versions` optional array of strings
- `editions` optional array of strings

Phase 1 does not block execution on product mismatch.
This is advisory unless later enhanced.

---

## Applicability

Applicability determines whether a pack should be auto-loaded.

Schema:

```yaml
applicability:
  requires_any_metrics:
    - "windows.processor.% processor time[instance=_total]"
    - "iis.web service.current connections[instance=_total]"
  requires_all_metrics:
    - "windows.system.processor queue length"
  excludes_metrics:
    - "linux.cpu.utilization"
```

Fields:
- `requires_any_metrics` optional string array
- `requires_all_metrics` optional string array
- `excludes_metrics` optional string array

Validation rules:
- all referenced metrics must be canonical metric strings
- empty arrays are not allowed

---

## Aliases

Aliases map raw counter identities to canonical metrics.

Schema:

```yaml
aliases:
  - from: "\\Memory\\Available MBytes"
    to: "windows.memory.available mbytes"
  - from: "\\Processor(_Total)\\% Processor Time"
    to: "windows.processor.% processor time[instance=_total]"
```

Fields:
- `from` required
- `to` required

Rules:
- `from` must be unique within a pack
- `to` must resolve to a canonical metric
- alias precedence is deterministic:
  1. exact match
  2. normalized exact match
  3. wildcard alias if future schema supports it; out of scope in Phase 1

---

## Rule references

In `pack.rules`, each entry is either:
- an inline rule object
- a file reference

### File reference
```yaml
rules:
  - file: "rules/cpu.yaml"
```

### Inline rule
```yaml
rules:
  - id: "high-cpu"
    name: "High CPU Saturation"
    ...
```

Recommended Phase 1 practice:
- one rule per file for maintainability

---

## Rule schema

```yaml
id: "high-cpu-sustained"
name: "High CPU Saturation"
description: "Detects sustained high total CPU utilization."
category: "cpu"
severity: "warning"
applies_when:
  all_metrics_present:
    - "windows.processor.% processor time[instance=_total]"
required_metrics:
  - "windows.processor.% processor time[instance=_total]"
conditions:
  operator: "and"
  items:
    - expr: "avg(metric('windows.processor.% processor time[instance=_total]')) >= 80"
    - expr: "percent_time_over(metric('windows.processor.% processor time[instance=_total]'), 90) >= 20"
finding:
  title: "Sustained high CPU utilization"
  summary: "CPU remained elevated for a meaningful portion of the capture."
  explanation: >
    The total processor time was above expected operating range for
    a sustained duration, which can indicate CPU saturation, undersized
    compute allocation, or workload spikes.
  recommendations:
    - "capture-process-cpu"
    - "review-workload-patterns"
evidence:
  include_metrics:
    - "windows.processor.% processor time[instance=_total]"
```

---

## Rule fields

### `id`
Required.
Unique within the pack.

Rules:
- lowercase kebab-case
- stable identifier
- max 80 chars recommended

### `name`
Required human-readable title.

### `description`
Required short description.

### `category`
Required.

Allowed Phase 1 values:
- `cpu`
- `memory`
- `disk`
- `network`
- `process`
- `iis`
- `sql`
- `system`
- `collection`
- `pack-validation`

### `severity`
Required default severity.

Allowed Phase 1 values:
- `critical`
- `warning`
- `informational`

### `applies_when`
Optional finer-grained applicability object.

Schema:
```yaml
applies_when:
  all_metrics_present:
    - "windows.memory.available mbytes"
  any_metrics_present:
    - "windows.paging.pages/sec"
  no_metrics_present:
    - "linux.memory.free"
```

### `required_metrics`
Optional array.
If omitted, engine infers required metrics from expressions where possible.
Best practice is to declare explicitly.

### `conditions`
Required object.

---

## Condition grammar

Phase 1 supports structured boolean composition over expression strings.

### Single condition
```yaml
conditions:
  expr: "avg(metric('windows.memory.available mbytes')) < 500"
```

### Boolean combination
```yaml
conditions:
  operator: "and"
  items:
    - expr: "avg(metric('windows.memory.available mbytes')) < 500"
    - expr: "percent_time_over(metric('windows.paging.pages/sec'), 50) >= 10"
```

Allowed `operator` values:
- `and`
- `or`

Nested groups are allowed.

### Expression functions
Allowed functions in Phase 1:
- `metric('canonical.metric')`
- `avg(...)`
- `min(...)`
- `max(...)`
- `median(...)`
- `p90(...)`
- `p95(...)`
- `p99(...)`
- `latest(...)`
- `duration_over(series, threshold)`
- `percent_time_over(series, threshold)`

Disallowed in Phase 1:
- arbitrary code
- custom functions
- filesystem or network access
- regex execution

---

## Finding template

Schema:

```yaml
finding:
  title: "Sustained high CPU utilization"
  summary: "CPU remained elevated for a meaningful portion of the capture."
  explanation: >
    The capture shows sustained CPU demand rather than a short spike.
  recommendation_behavior: "always"
  recommendations:
    - "capture-process-cpu"
    - "review-workload-patterns"
```

Fields:
- `title` required
- `summary` required
- `explanation` required
- `recommendation_behavior` optional
- `recommendations` optional array of recommendation IDs

`recommendation_behavior` allowed values:
- `always`
- `when-triggered` (default behavior)
- `never`

Phase 1 can treat omitted value as `when-triggered`.

---

## Evidence template

Schema:

```yaml
evidence:
  include_metrics:
    - "windows.processor.% processor time[instance=_total]"
  include_statistics:
    - "avg"
    - "max"
    - "p95"
  chart:
    enabled: true
    threshold_lines:
      - label: "warning"
        value: 80
      - label: "critical"
        value: 90
```

Fields:
- `include_metrics` required array
- `include_statistics` optional array
- `chart` optional object

Allowed `include_statistics` values:
- `min`
- `max`
- `avg`
- `median`
- `p90`
- `p95`
- `p99`
- `stddev`
- `missing_sample_count`

---

## Recommendation catalog

Recommendations should be defined centrally and referenced by ID.

Example `content/recommendations.yaml`:

```yaml
recommendations:
  - id: "capture-process-cpu"
    priority: "high"
    text: "Capture process-level CPU counters during the next reproduction."
    rationale: "Total CPU alone does not identify which process is consuming compute."
    next_collection:
      - "\\Process(*)\\% Processor Time"

  - id: "review-workload-patterns"
    priority: "medium"
    text: "Review scheduled jobs or workload spikes during the affected period."
    rationale: "Sustained CPU pressure is often tied to predictable workload windows."
```

Allowed priorities:
- `high`
- `medium`
- `low`

Recommendation IDs must be unique across the pack.

---

## References catalog

Optional external references can be declared.

```yaml
references:
  - id: "ms-doc-cpu"
    title: "General CPU troubleshooting guidance"
    url: "https://example.org/docs/cpu"
```

These are for rendering and traceability only in Phase 1.

---

## Validation rules

A pack is invalid if any of the following is true:
- schema version is unsupported
- required top-level fields missing
- duplicate pack IDs loaded in one run
- duplicate rule IDs within a pack
- invalid severity
- invalid category
- expression parse failure
- referenced recommendation ID does not exist
- referenced metric string is empty or malformed
- file reference missing
- alias target missing or invalid

Warnings, not fatal errors:
- no homepage
- no target_products
- no references catalog
- rules with no recommendations

---

## Example minimal pack

```yaml
schema_version: "pal.pack/v1"
pack:
  id: "windows-core"
  name: "Windows Core"
  version: "1.0.0"
  description: "Core Windows server troubleshooting rules"
  applicability:
    requires_any_metrics:
      - "windows.processor.% processor time[instance=_total]"
  aliases:
    - from: "\\Processor(_Total)\\% Processor Time"
      to: "windows.processor.% processor time[instance=_total]"
  rules:
    - id: "high-cpu-sustained"
      name: "High CPU Saturation"
      description: "Detect sustained high CPU utilization"
      category: "cpu"
      severity: "warning"
      required_metrics:
        - "windows.processor.% processor time[instance=_total]"
      conditions:
        operator: "and"
        items:
          - expr: "avg(metric('windows.processor.% processor time[instance=_total]')) >= 80"
          - expr: "percent_time_over(metric('windows.processor.% processor time[instance=_total]'), 90) >= 20"
      finding:
        title: "Sustained high CPU utilization"
        summary: "CPU remained elevated for a meaningful portion of the capture."
        explanation: "The total processor counter indicates sustained CPU pressure."
        recommendations:
          - "capture-process-cpu"
      evidence:
        include_metrics:
          - "windows.processor.% processor time[instance=_total]"
```

---

## Compatibility policy

Phase 1 engine must support:
- exact `pal.pack/v1`

Phase 2 may add:
- `pal.pack/v1.1`
- migration tooling
- richer expressions
- pack inheritance

No silent coercion across major schema versions.
