# Environments and Deployment

## Environments

### Local
Purpose:
- dev and feature work
- pack authoring
- UI development
- contract testing

Expected stack:
- PostgreSQL
- Redis
- object storage emulator or local filesystem
- API
- web
- analysis worker
- optional trend worker

### Integration
Purpose:
- service integration
- queue workflows
- OpenAPI validation
- pack compatibility checks

### Staging
Purpose:
- realistic test environment
- long-running job validation
- baseline and alert lifecycle validation

### Production
Purpose:
- real customer/internal workloads
- continuous ingestion
- alerting and automation

## Deployment posture

### Early phases
- Docker Compose for local and small-host deployments
- containerized API and workers
- PostgreSQL and Redis as managed or local services

### Later phases
- Kubernetes or managed container platform
- managed Postgres
- managed Redis
- cloud object storage
- centralized secrets management

## Runtime composition by phase

### Phase 1
- CLI local execution

### Phase 2
- API
- web
- analysis-worker
- Postgres
- Redis
- object storage

### Phase 3
Add:
- trend-worker
- richer background rollups

### Phase 4
Add:
- ingestion-worker
- automation-worker
- integration endpoints
- optional event forwarding or collectors
