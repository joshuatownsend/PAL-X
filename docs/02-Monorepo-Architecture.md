# Monorepo Architecture

## Recommended stack
- pnpm workspaces
- Turborepo
- Node.js 22 for frontend/tooling
- .NET 8 for engine and services
- PostgreSQL for operational data
- Redis for queue/cache
- object storage for uploaded and generated artifacts
- Next.js for web applications

## Architectural principles
1. Keep the analysis engine independent of transport.
2. Treat rule packs, policy packs, and recommendation packs as versioned content.
3. Prefer machine-readable contracts first, then UI.
4. Keep ingestion modular by signal family.
5. Keep explainability first-class in every result object.
6. Separate product concerns from tenant concerns until Phase 4 boundaries are actually needed.

## Top-level system layout

```text
apps/
  web                -> engineer/operator UI
  api                -> public/internal HTTP API gateway
services/
  analysis-worker    -> batch analysis execution
  ingestion-worker   -> scheduled + streaming ingestion
  trend-worker       -> baseline, drift, comparison rollups
  automation-worker  -> policy actions and safe automations
packages/
  contracts          -> shared schemas, DTOs, OpenAPI-generated types
  reporting          -> HTML/Markdown/JSON report generation
  web-ui             -> shared UI primitives and feature modules
  pack-runtime       -> rule/pack loading, validation, versioning
  recommendation-runtime -> recommendation and playbook evaluation
dotnet/
  src/
    Pal.Engine       -> core deterministic analysis engine
    Pal.Ingestion    -> evidence parsers and normalization
    Pal.Correlation  -> time alignment and co-occurrence scoring
    Pal.Policy       -> alert/policy evaluation
    Pal.Storage      -> data access abstractions
    Pal.Workflows    -> orchestration and job execution helpers
  tests/
    Pal.Engine.Tests
    Pal.Ingestion.Tests
    Pal.Correlation.Tests
    Pal.Policy.Tests
packs/
  thresholds/
  recommendations/
  policies/
  samples/
infra/
  docker/
  compose/
  sql/
  k8s/
docs/
  architecture/
  product/
  operations/
  runbooks/
tools/
  scripts/
  dev/
```

## Why this split works

### Node side
Use the JavaScript/TypeScript side for:
- web application
- contract generation
- report templates
- operational tooling
- dev ergonomics

### .NET side
Use .NET for:
- engine logic
- parsers
- queue workers
- runtime analysis
- policy evaluation where performance and type safety matter

This gives strong execution/runtime characteristics while keeping the product surface easy to evolve.
