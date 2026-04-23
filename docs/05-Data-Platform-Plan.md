# Data Platform Plan

## Primary stores

### PostgreSQL
Use for:
- job metadata
- result summaries
- baselines
- comparisons
- trends
- alerts
- policies
- tenants
- systems inventory

### Redis
Use for:
- queueing
- transient job state
- rate limiting
- short-lived orchestration locks

### Object storage
Use for:
- uploaded evidence bundles
- generated reports
- large normalized artifacts
- export packages

## Core domain entities

### Phase 1 entities
- evidence_bundle
- analysis_job
- analysis_result
- finding
- report_artifact
- pack_version

### Phase 2 entities
- job_attempt
- analysis_run
- uploaded_artifact
- report_export

### Phase 3 entities
- baseline
- baseline_member
- comparison_run
- correlated_observation
- trend_snapshot
- recommendation_instance
- incident_window

### Phase 4 entities
- monitored_system
- environment
- fleet_group
- policy
- policy_assignment
- alert
- alert_event
- integration_endpoint
- automation_action
- tenant
- membership
- audit_event

## Important design rule
Never bury the only explanation in freeform text.
Store structured evidence references and structured recommendation fields alongside human-readable summaries.
