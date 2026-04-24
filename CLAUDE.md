# PAL-X — Claude Code Conventions

## Phase 1 is .NET-only

There is no JavaScript, TypeScript, or Node.js in Phase 1. Do not create `package.json`, `pnpm-workspace.yaml`, `turbo.json`, `apps/`, `services/`, or `packages/` directories. These land with Phase 2 when a real web consumer exists.

## The `legacy/` directory is read-only reference

`legacy/pal-v2` is a submodule containing the original PAL v2 PowerShell tool. Do not modify it. It exists solely to inform port decisions.

## Pack schema source of truth

`dotnet/schemas/pal.pack.v1.json` is the authoritative pack schema — it supersedes `docs/PAL-2026-Implementation-Spec-Pack/PAL-Pack-Schema-v1.md` (the seeded doc is ChatGPT-generated and was revised). Similarly, `dotnet/schemas/pal.report.v1.json` supersedes the seeded report schema doc.

## Key deviations from seeded docs

See `docs/architecture/adr/0001-deviations-from-seed-docs.md` for all 12 ratified deviations. The most important:

- **No numeric health score**: Use tri-state status (critical/warning/healthy), not a 0-100 additive score.
- **Declarative comparators**: No expression DSL or parser. Every rule condition uses `metric` + `aggregation` + `operator` + `threshold` + `duration_percent`.
- **snake_case metric IDs**: All canonical metric IDs use snake_case (e.g., `processor.percent_processor_time`). Legacy counter paths live in `metric_aliases` in each pack.
- **`host_context` in schema v1**: RAM-relative and CPU-count-relative thresholds use `host_context.total_physical_memory_mb` / `host_context.logical_processor_count` — not deferred.
- **Spectre.Console.Cli** (not System.CommandLine which is still beta).
- **ScottPlot** for chart SVGs (not hand-rolled renderer).
- **Content-hash IDs**: finding_id and report_id are SHA-256-based, not ULID.

## UTF-8 without BOM

All JSON and HTML artifacts use `new UTF8Encoding(false)`. Never use bare `Encoding.UTF8` for writing report files.

## Finding sort order

severity desc → category asc → rule_id asc → finding_id asc. The `RuleEngine` must enforce this on every run.

## `host_context` unknown = informational finding + rule skipped

If a rule references `host_context.total_physical_memory_mb` or `host_context.logical_processor_count` and the value is unknown, emit an informational warning and skip the rule. Do not fail the run.

## CLI output naming

`<input-stem>.pal-report.json` and `<input-stem>.pal-report.html`. Charts go in `<output>/charts/<report-name>-<chart-id>.svg`.

## BLG stub

`BlgCollectorStub` throws `NotSupportedException` with the message: `BLG import is not supported in Phase 1. Convert your log first: relog -f CSV "<input>" -o "<stem>.csv"`. Phase 1.5 adds PDH interop.

## Test determinism

Golden fixture tests use `--now <ISO>` to override `generated_at_utc` so the output is byte-identical across runs. ScottPlot SVG tests assert byte-identical output on two renders of the same data.
