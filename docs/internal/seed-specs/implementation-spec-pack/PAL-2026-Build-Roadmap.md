# PAL 2026 Build Roadmap

## Phase 0: Discovery
- Audit current PAL capabilities
- Inventory current threshold files
- Identify reusable parsing logic, if any
- Build fixture corpus:
  - healthy Windows server
  - CPU constrained server
  - memory pressure
  - disk latency scenario
  - SQL bottleneck scenario
- Define pack schema v1
- Decide rewrite boundaries

## Phase 1: Engine MVP
- Implement normalized metric model
- Build BLG ingestion pipeline
- Implement rule engine v1
- Implement report renderer
- Build CLI:
  - analyze
  - validate-pack
- Ship starter packs:
  - Windows base
  - SQL starter
- Create golden fixtures and expected outputs

## Phase 2: API + Web
- Build upload/dataset model
- Add queue-backed run execution
- Build findings UI
- Add export/download
- Add run history and search
- Add baseline designation

## Phase 3: Compare + Correlate
- Build compare engine
- Add drift/regression concepts
- Ingest event log extracts
- Add timeline/highlight model
- Tighten evidence narratives and severity scoring

## Phase 4: Expand Sources
- Add SQL enrichment imports
- Add IIS log import/correlation
- Add ETW/WPA summary import
- Add custom pack registry
- Add pack test/preview UI

## Phase 5: Product Hardening
- Auth/RBAC
- Org/workspace model
- retention controls
- audit trail
- webhook integrations
- deployment charts/templates
