# Unified Product Plan

## Product thesis

PAL should not be rebuilt as a prettier desktop utility.
It should be rebuilt as an explainable diagnostics platform for Windows- and SQL-heavy environments.

The core differentiator remains the same across every phase:
- deterministic analysis first
- explainable findings
- evidence-linked recommendations
- structured operational workflows

## Strategic progression

### Phase 1 — Engine
Build the portable core:
- ingest captured evidence
- normalize measurements
- execute rule packs
- produce machine-readable results and human-readable reports

### Phase 2 — Platform
Wrap the engine in a real system:
- API
- job queue
- worker
- storage
- thin engineer UI

### Phase 3 — Intelligence
Add context:
- baselines
- comparisons
- trends
- drift
- cross-signal correlation
- guided next steps

### Phase 4 — Continuous system
Add presence and operations:
- ongoing ingestion
- alerting
- policies
- automation
- fleet awareness
- SaaS readiness

## Product boundary

PAL is not trying to be:
- a generic log platform
- a black-box AI root-cause engine
- a broad APM replacement

PAL is trying to be:
- a trustworthy diagnostics and performance operations system
- especially strong for Windows server, IIS, SQL Server, and adjacent Microsoft-centric estates

## Primary users

### Primary
- infrastructure engineers
- Windows platform engineers
- DBAs
- escalation/support engineers
- consultants performing performance reviews

### Secondary
- SRE / platform teams in hybrid Microsoft estates
- MSP or internal shared-services teams
- incident responders handling Windows or SQL performance issues

## Core outcomes
The system should help a user answer:
- What looks wrong?
- What changed?
- Is this abnormal for this system?
- What signals corroborate the issue?
- What should I collect or check next?
- Should I alert, open a case, or trigger deeper capture?
