# Package and Service Boundaries

## apps/api
Purpose:
- public/internal HTTP surface
- auth boundary
- request validation
- job submission and retrieval
- pack discovery
- analysis, baseline, comparison, alert, and policy endpoints

Should not contain:
- engine logic
- parser logic
- deep business rules

## apps/web
Purpose:
- operator and engineer UI
- upload workflows
- run review
- baseline management
- compare screens
- trends and alert views

## services/analysis-worker
Purpose:
- consume analysis jobs
- invoke Pal.Engine + Pal.Ingestion
- persist outputs
- generate reports

## services/ingestion-worker
Purpose:
- poll scheduled sources
- receive push inputs
- normalize incoming signals
- create evidence bundles or time-series snapshots

## services/trend-worker
Purpose:
- build and refresh baselines
- compute drift summaries
- prepare comparison rollups
- compute recurring finding summaries

## services/automation-worker
Purpose:
- evaluate policy-triggered actions
- send notifications
- create tickets or webhooks
- trigger safe follow-up workflows

## packages/contracts
Purpose:
- JSON schemas
- TypeScript types
- OpenAPI-generated clients
- canonical API DTO definitions

## packages/reporting
Purpose:
- report templates
- HTML/Markdown rendering
- export helpers

## packages/web-ui
Purpose:
- reusable UI
- data viewers
- timeline and comparison widgets

## packages/pack-runtime
Purpose:
- pack loading
- schema validation
- semantic version handling
- compatibility checks

## packages/recommendation-runtime
Purpose:
- structured playbooks
- recommendation resolution
- evidence-linked next-step content

## dotnet/src/Pal.Engine
Purpose:
- evaluate rules against normalized evidence
- compute findings
- severity/confidence scaffolding
- produce deterministic outputs

## dotnet/src/Pal.Ingestion
Purpose:
- parse BLG, CSV, JSON, event exports, SQL exports
- normalize evidence into engine-friendly models
- manage time windows and metadata

## dotnet/src/Pal.Correlation
Purpose:
- align time ranges
- score co-occurrence
- build correlated observations
- attach recommendation triggers

## dotnet/src/Pal.Policy
Purpose:
- evaluate policies
- create alerts
- decide when actions are eligible
- keep action decisions explainable

## dotnet/src/Pal.Storage
Purpose:
- repository/data access layer
- PostgreSQL access
- object storage integration
- result persistence abstractions

## dotnet/src/Pal.Workflows
Purpose:
- orchestration helpers
- job sequencing
- retry and resumability patterns
