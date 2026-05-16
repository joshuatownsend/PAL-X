---
title: Changelog
description: Release notes per phase — what shipped, what changed, what was deferred.
---

# Changelog

PAL-X is organised into phases. Each phase represents a coherent slice of capability. Within a phase, point releases are versioned independently and announced in this changelog.

For per-PR detail, see the commit log on GitHub.

## Unreleased

Work in progress on `main`. Major surfaces:

- **Documentation site** (this site) — comprehensive user-facing documentation hosted via GitHub Pages, built by DocFX. Replaces the README-as-documentation pattern that came before.
- **Markdown reports** — `MarkdownReportWriter` produces GFM-flavoured renderings of analysis output for PR comments and chatops.
- **Pack signing** — RSA-PSS-SHA256 over `pack.yaml` bytes with a sidecar; `--require-signature` and `--trust-key` enforcement.
- **Schema v1.1** — pack schema gains optional `window:` block on conditions for rolling-window aggregations.

## Phase 4 — Continuous monitoring (shipped 2026-04-29)

Phase 4 turned PAL-X from a one-shot analyser into a continuous-monitoring platform.

### Added

- **Alerting** — `PolicyEvaluator` emits alerts from findings on completed jobs. v1 policy: any critical finding → alert; warning fires in 3 of last 5 runs → alert (escalated to critical).
- **Alert lifecycle** — open / acknowledged / resolved states + orthogonal snooze. Max 30-day snooze.
- **Webhooks** — `NotificationService` posts JSON to subscribed sinks on `alert.{created,escalated,acknowledged,resolved}` events. HMAC-SHA256 signing via `X-PAL-Signature` header.
- **Schedules** — `ScheduledIngestionWorker` polls a directory at a configured interval, submits new files as analysis jobs.
- **Test webhook endpoint** — `POST /webhooks/{id}/test` for delivery verification.
- **Audit events** — `WorkspaceAuditEventEntity` records mutations for compliance.

### Changed

- **Schema** — `pal.pack/v1.1` adds the optional `window:` block (ADR 0004).

### Known gaps

- No `analysis.completed` webhook event (only alert events fire). Use polling for job completion.
- No per-rule alert suppression / maintenance windows.
- Schedule failures don't fire alerts (logged only).

## Phase 3 — Comparisons, trends, correlations, diagnostics (shipped 2026-04-28)

Phase 3 added the analytical surfaces that operate across multiple jobs.

### Added

- **Baselines** — designate any completed job as a baseline. Implicit versioning via `(type, contextJson)` — `machine | role | workload | release`.
- **Compare** — `CompareRunner` diffs two jobs into appearing / resolved / unchanged / worsening / improving categories. Manual via `POST /compare`, automatic via `selectedBaselineId` on submit.
- **Trends** — `TrendAnalyzer` rolls up per-rule trajectories across the last N completed jobs in a workspace.
- **Correlations** — `CorrelationAnalyzer` finds metric pairs that co-vary across the same window; descriptive not causal.
- **Guided diagnostics** — `DiagnosticsService` produces per-job rule-cited insights combining findings, trends, and correlations.

### Changed

- **Reports** — `series_index` and per-finding statistics expanded to support correlation lookup.

## Phase 2 — Multi-tenancy, API, UI (shipped 2026-04-27)

Phase 2 lifted PAL-X from a CLI tool to a service.

### Added

- **HTTP API** — ASP.NET Core minimal API. 50+ endpoints. Workspace-scoped data plane under `/api/workspaces/{workspaceId}/...`.
- **Multi-tenancy** — Org → Workspace hierarchy. `TenantResolutionEndpointFilter` enforces membership; EF global query filters scope every read by `WorkspaceId`; DB-level cascades reject orphans.
- **Identity** — ASP.NET Core Identity with cookie auth for the UI, API key (SHA-256-hashed) for automation. Three roles: Admin / Analyst / Viewer.
- **Background workers** — `AnalysisWorker` (in-process Channel), `RetentionWorker` (daily), `ScheduledIngestionWorker` (every 30s tick).
- **Blazor UI** — `/jobs`, `/packs`, `/baselines`, `/compare`, `/trends`, `/correlations`, `/alerts`, `/webhooks`. Same authorisation pipeline as the API.
- **Postgres persistence** — EF Core 8 + `postgres:16-alpine`. Automatic migrations on startup.

## Phase 1.5 — Pack signing, BLG ingestion, dataset export (shipped 2026-04-27)

Filling gaps in the Phase 1 surface.

### Added

- **BLG ingestion** — `BlgCollector` reads Windows perfmon binary logs via PDH interop. Windows-only; `PlatformNotSupportedException` with `relog -f CSV` message on Linux/macOS.
- **Pack signing** — `pal packs sign` produces a `pack.yaml.sig` sidecar (RSA-PSS-SHA256 over raw bytes). `--require-signature` / `--trust-key` on load.
- **Dataset export** — submit with `includeDataset: true` to persist a gzipped JSON dataset alongside the report.

## Phase 1 — Core analyzer (shipped 2026-04-23)

The MVP: a CLI that ingests Windows perfmon CSV and emits JSON/HTML reports.

### Initial release

- `Pal.Engine` — declarative rule engine, statistics, status classifier.
- `Pal.Ingestion` — `CsvCollector` (cross-platform).
- `Pal.Packs` — YAML pack loader, validator.
- `Pal.Reporting` — `JsonReportWriter`, `HtmlReportWriter`, ScottPlot SVG charts.
- `Pal.Cli` — `pal analyze`, `pal validate-pack`, `pal inspect-dataset`, `pal list-packs`.
- Three shipped packs: `windows-core`, `iis-core`, `sql-host-core`.
- Pack schema `pal.pack/v1`; report schema `pal.report/v1`.

## Versioning policy

PAL-X uses sequential phase numbers for major capability slices, and conventional semver for minor and patch releases within each phase. The current build identifies itself in `GET /health`:

```json
{ "status": "ok", "version": "2026.2.0" }
```

The version string is a date-based `YYYY.MAJOR.MINOR`. Today's `2026.2.0` corresponds to Phase 4 v1.

## Related

- **[Architecture — Schema evolution](architecture/schema-evolution.md)** — schema versioning policy.
- **[Architecture — ADR index](architecture/adr/index.md)** — the decisions behind each phase.
- **[Contributing](contributing/index.md)** — how to land work in the next release.
