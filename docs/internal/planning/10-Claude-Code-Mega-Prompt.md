# Claude Code Mega Prompt — Build the PAL Monorepo Foundation

You are working inside a new repository for PAL 2026, a modern rebuild of the Performance Analysis of Logs concept.

Your job is to create the initial repo-ready monorepo foundation that supports all four planned phases.

## Product context

PAL is being rebuilt as an explainable diagnostics platform for Windows- and SQL-heavy environments.

The system evolves across four phases:

1. Phase 1 — deterministic analysis engine
   - evidence ingestion
   - normalized metrics
   - rule packs
   - structured findings
   - report generation

2. Phase 2 — platform
   - API
   - queue
   - worker
   - storage
   - thin web UI

3. Phase 3 — intelligence
   - baselines
   - comparison
   - trends
   - drift
   - cross-signal correlation
   - guided recommendations

4. Phase 4 — continuous system
   - scheduled ingestion
   - policies
   - alerts
   - automation
   - fleet awareness
   - SaaS readiness

## Architectural direction

Use a monorepo with:
- pnpm workspaces
- Turborepo
- Node.js 22
- Next.js for web UI
- a TypeScript API layer or gateway app under apps/api
- .NET 8 for the core engine and worker-side runtime
- PostgreSQL
- Redis
- Docker Compose for local dev

## Required repo structure

Create this structure:

- apps/web
- apps/api
- services/analysis-worker
- services/ingestion-worker
- services/trend-worker
- services/automation-worker
- packages/contracts
- packages/reporting
- packages/web-ui
- packages/pack-runtime
- packages/recommendation-runtime
- dotnet/src/Pal.Engine
- dotnet/src/Pal.Ingestion
- dotnet/src/Pal.Correlation
- dotnet/src/Pal.Policy
- dotnet/src/Pal.Storage
- dotnet/src/Pal.Workflows
- dotnet/tests/*
- packs/thresholds
- packs/recommendations
- packs/policies
- packs/schemas
- packs/samples
- infra/compose
- infra/docker
- infra/sql
- docs/architecture
- docs/product
- docs/operations
- docs/runbooks
- tools/scripts
- tools/dev

## What to implement now

Implement the repo foundation, not the full product.

### Required outputs
1. Root workspace and build tooling
2. Base Next.js web app scaffold
3. Base API scaffold
4. Base .NET solution and projects
5. Shared contracts package
6. Initial pack schemas and sample pack files
7. Docker Compose for local Postgres, Redis, object storage emulator or equivalent local artifact storage
8. Root README
9. docs/architecture/monorepo.md
10. docs/product/roadmap.md
11. docs/operations/local-development.md

## Technical requirements

### Root
- package.json
- pnpm-workspace.yaml
- turbo.json
- .node-version pinned to 22
- .gitignore
- .editorconfig if helpful

### packages/contracts
Define initial schemas and TS types for:
- EvidenceBundle
- AnalysisJob
- AnalysisResult
- Finding
- ReportArtifact
- PackManifest
- Baseline
- ComparisonRun
- Alert
- Policy

Prefer a schema-first approach.

### packs/schemas
Add machine-readable schemas for:
- threshold packs
- recommendation packs
- policy packs

### dotnet
Create a solution with projects and tests for the engine-side libraries.
Set project references cleanly and keep responsibilities separated.

### apps/api
Provide a minimal API with health endpoint and placeholder endpoints for:
- POST /analysis/run
- GET /analysis/:id
- GET /analysis/:id/results
- GET /analysis/:id/report
- GET /packs
- POST /baselines
- POST /compare
- GET /alerts

These can be stubbed if needed, but the route layout and contracts must be coherent.

### apps/web
Provide a thin shell with pages/views for:
- dashboard
- analyses
- baselines
- compare
- alerts
- settings

Keep the UI minimal and engineer-focused.

### services
Create minimal runnable placeholders with README comments or docs indicating intended responsibility.

### infra
Provide a local compose file for:
- postgres
- redis
- object/artifact storage or a clearly documented local filesystem substitute

## Important constraints

- Keep the engine deterministic and independent of UI/API transport.
- Keep contracts centralized.
- Keep pack content versioned and schema-validated.
- Do not over-engineer auth yet.
- Do not implement fake AI features.
- Favor clarity over completeness.

## Deliverable quality bar

The repository should be something a real engineer can clone, install, boot locally, and begin implementing against.
It should feel coherent, intentional, and ready for incremental development.
