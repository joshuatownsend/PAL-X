# PAL 2026 — Claude Code Mega-Prompt for Phase 2

You are implementing Phase 2 of PAL 2026.

Phase 1 already established a deterministic analysis engine with:
- ingestion and normalization
- pack-driven rule execution
- structured results
- report generation
- local CLI use

Your job is to build the Phase 2 platform around that engine without rewriting the engine itself.

## Primary outcome

Turn PAL into an internal, API-driven diagnostics platform with:
- upload handling
- asynchronous analysis jobs
- persistent storage
- pack discovery and version resolution
- report retrieval
- thin engineer-facing web UI
- CLI calls routed through the API

## Architectural rules

1. Preserve the Phase 1 engine as a bounded module.
2. Do not move rule logic into API handlers or workers.
3. Keep contracts schema-first and explicit.
4. Prefer clean layering over shortcuts.
5. Optimize for trust, reproducibility, and maintainability over cleverness.

## Build targets

### Backend
- .NET 8 API
- .NET 8 worker service
- Postgres persistence
- Redis queue
- local filesystem-backed object storage abstraction for dev

### Frontend
- Next.js web app
- thin UI only:
  - submit analysis
  - list jobs
  - show job details
  - show findings
  - fetch report

### CLI
- remote-first CLI commands:
  - submit
  - status
  - results
  - report
  - packs

## Required implementation scope

### 1. API
Implement endpoints matching the OpenAPI contract in `PAL-2026-Phase2-OpenAPI.yaml`.

Minimum endpoints:
- GET /health
- GET /packs
- POST /uploads
- POST /analysis
- GET /analysis
- GET /analysis/{analysisId}
- GET /analysis/{analysisId}/results
- GET /analysis/{analysisId}/report?format=html|markdown

### 2. Persistence
Implement database schema and repositories for:
- uploads
- analysis_jobs
- analysis_job_packs
- analysis_results
- analysis_reports
- packs
- pack_versions
- audit_events

Use a migration-based approach.

### 3. Worker
Implement queue-driven job processing.
The worker must:
- claim a queued job safely
- download input artifact
- resolve exact pack versions
- invoke the Phase 1 engine
- persist structured results
- persist reports
- mark job completed or failed

### 4. Pack registry
Implement pack listing and version resolution.
Historical runs must retain exact pack versions used at execution time.

### 5. Web UI
Build a minimal but usable UI with these screens:
- `/submit`
- `/jobs`
- `/jobs/[id]`

Keep the UI functional and plain. Do not over-design it.

### 6. CLI
Add or refactor CLI commands so they call the API rather than local-only engine code.

## Deliverables in repo

Create or update:
- `/docs/phase-2/architecture.md`
- `/docs/phase-2/api.md`
- `/docs/phase-2/data-model.md`
- `/docs/phase-2/worker.md`
- `/docs/phase-2/web-ui.md`
- `/docs/phase-2/runbook.md`

Also create:
- migrations
- OpenAPI wiring
- docker compose for local stack
- seed data for at least two packs

## Acceptance criteria

The implementation is complete when:
1. I can upload a BLG and receive an upload id.
2. I can submit an analysis job with one or more packs.
3. The worker processes the job asynchronously.
4. Results are stored and retrievable as JSON.
5. HTML and Markdown reports are retrievable.
6. The CLI can submit and retrieve results through the API.
7. The web UI can submit and review jobs.
8. Exact pack versions used for a run are persisted.

## Code quality expectations

- Strong typing
- Clear boundaries
- Good naming
- Defensive error handling
- Structured logs
- No hidden magic
- No unnecessary framework sprawl

## What not to build yet

Do not add:
- ML or anomaly detection
- advanced auth/RBAC
- billing
- multi-tenancy
- OpenTelemetry ingestion
- ETW-native trace parsing
- analytics dashboards

## Final instruction

Make the smallest clean system that satisfies the Phase 2 platform shape and is ready for later expansion into baselines, comparisons, and correlation features.
