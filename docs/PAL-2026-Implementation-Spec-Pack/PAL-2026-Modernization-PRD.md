# PAL 2026 Modernization PRD

## 1. Overview

This document defines a modernization plan for **PAL (Performance Analysis of Logs)** as a modern diagnostics and performance-analysis platform.

The goal is not to discard what made PAL valuable. The goal is to preserve its strongest idea:

**Take captured performance data, apply domain knowledge, explain what matters, and produce a useful diagnostic report quickly.**

That idea still holds. What must change is the platform around it.

## 2. Product Vision

PAL 2026 becomes a **diagnostic analysis engine and workflow platform** for Windows-centric performance troubleshooting, with expansion into broader telemetry correlation.

It should serve three audiences:

1. **Operators / administrators**
   Need quick first-pass analysis of a collected dataset.

2. **Performance engineers / escalation engineers**
   Need deep drill-down, comparison, tuning guidance, and machine-readable output.

3. **Support organizations / MSPs / internal platform teams**
   Need repeatable case workflows, standard rule packs, automation hooks, and exportable evidence.

## 3. Problem Statement

Classic PAL solved a real problem well:
- Performance data is hard to interpret manually.
- Engineers often know what counters to collect but not how to explain them quickly.
- Customers and internal teams need an output that is readable, actionable, and shareable.

Modern gaps now include:
- Too much reliance on BLG/PerfMon as the only real source of truth
- Heavy dependence on fixed thresholds
- Weak support for cross-source correlation
- Desktop-era UX and architecture
- Limited automation and CI/API friendliness
- Poor fit for hybrid environments, ephemeral workloads, and modern telemetry pipelines

## 4. Product Goals

### Primary goals
- Preserve PAL's strength in **threshold/rule-based performance analysis**
- Support **modern Windows Server** and common enterprise workloads reliably
- Make the analysis engine **scriptable, testable, and API-driven**
- Improve report quality and explainability
- Introduce **baseline comparison** and **drift analysis**
- Expand inputs beyond BLG while keeping BLG first-class

### Secondary goals
- Allow teams to author and version their own rule packs
- Support case bundles and workflow automation
- Enable a path to hosted or self-hosted web deployment

### Non-goals for v1
- Full APM replacement
- Full distributed tracing backend
- Real-time observability platform at hyperscale
- Agent-heavy always-on monitoring as the first release

## 5. Product Principles

1. **Keep the engine opinionated**
   PAL should not become a generic chart viewer. It should make judgments.

2. **Explain every finding**
   Every flagged condition should include what happened, why it matters, and what to check next.

3. **Support offline analysis first**
   Engineers often troubleshoot from bundles captured after the fact.

4. **Treat rule packs as product assets**
   The rule packs are core IP and must be versioned, testable, and maintainable.

5. **Design for automation**
   Anything the UI can do should be possible from CLI and API.

6. **Prefer structured outputs**
   Human-readable reports matter, but machine-readable JSON is essential.

## 6. Target Users and Jobs To Be Done

### A. Windows systems administrator
**Job:** I collected counters from a slow server. Tell me what stands out and where to start.

### B. SQL Server engineer
**Job:** I need to correlate host pressure, SQL waits, and system symptoms to identify likely bottlenecks.

### C. Escalation/support engineer
**Job:** I need a standardized report I can attach to a ticket, with enough detail to justify next actions.

### D. Platform/operations team
**Job:** I want to compare a current log to a healthy baseline and spot drift or regressions.

### E. Consultant/MSP
**Job:** I need a repeatable way to analyze customer performance bundles using reusable domain packs.

## 7. Core Use Cases

1. Analyze a Windows PerfMon BLG and generate a prioritized diagnostic report
2. Compare a “bad” capture to a baseline capture
3. Analyze a SQL-focused bundle including counters plus selected DMVs or waits
4. Ingest a case bundle from a customer and output a summary plus detailed findings
5. Run analysis from PowerShell or CI and return JSON
6. Apply environment-specific thresholds through a custom pack
7. Correlate performance symptoms with event log markers and notable timestamps

## 8. Scope

## In scope
- BLG ingestion
- CSV/JSON metrics ingestion
- Rule-based analysis engine
- Pack/version management
- HTML/Markdown/JSON reporting
- CLI and REST API
- Web UI for upload, analysis, browsing, compare, and export
- Baseline and drift comparison
- Windows event correlation
- SQL enrichment support
- Pack authoring and validation tools

## Out of scope initially
- Kubernetes-native scraping
- Full OpenTelemetry trace storage backend
- Continuous streaming metric storage
- Enterprise RBAC/SSO beyond basic auth for first hosted version

## 9. Functional Requirements

### 9.1 Data Ingestion

The platform shall support:
- PerfMon BLG files
- CSV metric imports
- JSON metric imports using a documented schema
- Zip case bundles containing one or more supported input files

The ingestion layer should:
- Normalize timestamps to a single canonical timeline
- Identify counter/category/object metadata
- Handle locale and counter-name mapping
- Detect missing or partial data
- Surface ingestion warnings cleanly

### 9.2 Analysis Engine

The engine shall:
- Apply threshold/rule packs to normalized datasets
- Support rules based on:
  - static thresholds
  - duration above/below threshold
  - percentile behavior
  - rate of change
  - correlation with other signals
  - compare-to-baseline deltas
- Produce scored findings with severity and confidence

Every finding should include:
- title
- severity
- affected source/counter
- time window
- evidence summary
- why it matters
- likely causes
- recommended next checks
- related findings

### 9.3 Rule Pack System

Rule packs shall:
- be versioned independently of the app
- define supported workload, OS/app version, and prerequisites
- include human guidance text and remediation notes
- support inheritance/composition from base packs
- be testable against fixture datasets

Planned first-party pack families:
- Windows OS
- CPU / memory / disk / network base
- IIS / ASP.NET
- SQL Server
- Active Directory / Domain Controller
- Hyper-V
- RDS
- Exchange
- File Server
- Custom organization packs

### 9.4 Comparison and Baselines

The platform shall support:
- compare run A vs run B
- compare run vs selected baseline
- compare within same host/workload family
- identify:
  - regression
  - drift
  - new symptoms
  - resolved symptoms

Comparison outputs should highlight:
- materially changed counters
- changed rule outcomes
- likely areas of regression

### 9.5 Reporting

The platform shall generate:
- executive summary
- engineering report
- machine-readable JSON
- Markdown export
- compact ticket summary

Report sections should include:
- analysis metadata
- coverage / missing data warnings
- top findings
- timeline highlights
- component findings
- counter evidence snapshots
- next-step recommendations

### 9.6 Web UI

The web UI shall support:
- upload/import of datasets
- dataset browser
- analysis run creation
- rule pack selection
- baseline selection
- compare view
- findings browser with filters
- export/download
- pack browser
- pack validation/test status
- saved analysis history

### 9.7 CLI / API

The product shall expose:
- CLI command to analyze a dataset
- CLI command to compare runs
- CLI command to validate packs
- REST API for upload, run, retrieve, export

Example CLI goals:
- predictable automation
- quiet mode for scripts
- JSON output for pipelines
- zero dependence on UI

## 10. Non-Functional Requirements

### Performance
- Handle large PerfMon logs without loading entire raw datasets into memory where avoidable
- Support background processing for large runs
- Stream analysis status to UI/API

### Reliability
- Deterministic results for the same dataset + same pack version
- Clear warnings when data prerequisites are not met
- Strong test coverage on ingestion and rule evaluation

### Security
- Secure handling of uploaded bundles
- No arbitrary code execution from rule packs
- Pack schema validation
- Role separation for hosted mode
- Optional local/self-hosted-only deployment mode

### Portability
- Core engine should run on modern .NET
- Support Windows first, with Linux-compatible service hosting where feasible for API/reporting layers

### Maintainability
- Modular service boundaries
- Pack versioning independent of release cycle
- Fixtures and golden-report tests

## 11. Information Architecture / Major Components

### A. Ingestion Service
Parses BLG, CSV, JSON, and bundles into normalized internal structures.

### B. Normalization Layer
Maps source-specific names and metadata into canonical metric identities.

### C. Rule Engine
Evaluates rule packs against normalized datasets and produces findings.

### D. Comparison Engine
Compares runs, baselines, and prior findings.

### E. Report Service
Builds HTML, Markdown, JSON, and summary outputs.

### F. Pack Registry
Stores, versions, validates, and tests pack definitions.

### G. API Layer
Provides upload, run, compare, retrieve, export endpoints.

### H. Web App
Provides a human-friendly interface for interactive analysis.

### I. Worker Service
Runs heavy analyses asynchronously.

## 12. Proposed Technical Architecture

## Recommended stack
- **Backend:** .NET 8/9
- **API:** ASP.NET Core Web API
- **Worker:** background service / queue-backed worker
- **Web UI:** React / Next.js or ASP.NET frontend
- **Storage:** PostgreSQL for metadata + object/file storage for artifacts
- **Packaging:** Docker-first
- **CLI:** .NET global tool or standalone executable

## Suggested service decomposition
- `pal.ingest`
- `pal.engine`
- `pal.compare`
- `pal.report`
- `pal.api`
- `pal.worker`
- `pal.web`
- `pal.packs`

## Internal data model concepts
- Dataset
- Source
- Signal/Metric
- Sample series
- AnalysisRun
- Finding
- Evidence
- Pack
- PackVersion
- Baseline
- ComparisonRun
- Artifact

## 13. Rule Pack Design

## Pack schema concepts
- metadata
- supported_platforms
- prerequisites
- counter mappings
- rules
- severity model
- narrative templates
- remediation text
- related docs links
- tests/fixtures

## Example rule types
- threshold over duration
- threshold under duration
- deviation from baseline
- ratio between two counters
- multi-signal condition
- anomaly against rolling window
- event-correlated spike

## Authoring experience
Rule packs should be editable as structured files, ideally YAML or JSON with strict schema validation.

Author tooling should include:
- schema validation
- fixture replay
- preview finding generation
- linting for narrative quality and missing metadata

## 14. Data Sources Roadmap

### Phase 1 sources
- PerfMon BLG
- CSV
- JSON
- zipped evidence bundles

### Phase 2 sources
- Windows Event Log extracts
- SQL wait stats / selected DMV exports
- IIS logs
- simple ETW/WPA summary imports

### Phase 3 sources
- Azure Monitor exports
- OpenTelemetry metric imports
- cloud VM/platform snapshots
- custom plugin adapters

## 15. UX Direction

The UI should feel like a **case analysis workspace**, not a traditional monitoring dashboard.

### Primary screens
1. Home / recent analyses
2. Upload/import dataset
3. New analysis run
4. Findings summary
5. Evidence drilldown
6. Compare runs
7. Pack browser
8. Pack editor/validator (admin or advanced mode)

### UX priorities
- clear severity and prioritization
- fast scan to top findings
- easy jump from summary to evidence
- explicit pack version shown everywhere
- explicit ingestion warnings
- one-click export to shareable report formats

## 16. API Concept

### Example endpoints
- `POST /api/v1/datasets`
- `POST /api/v1/analysis-runs`
- `GET /api/v1/analysis-runs/{id}`
- `POST /api/v1/comparisons`
- `GET /api/v1/reports/{id}`
- `GET /api/v1/packs`
- `POST /api/v1/packs/validate`

### Key API outputs
- run status
- normalized dataset summary
- findings list
- compare deltas
- report artifacts
- ingestion warnings
- provenance metadata

## 17. Phased Delivery Plan

## Phase 0 — Discovery and stabilization
**Objective:** understand what must be preserved and what must be replaced.

Deliverables:
- inventory of existing PAL capabilities
- threshold/rule pack audit
- sample dataset corpus
- known issue list
- target architecture decision
- migration strategy

Exit criteria:
- clear cut list of reusable logic vs rewrite
- backlog sized for MVP

## Phase 1 — Core engine MVP
**Objective:** modern PAL engine for offline analysis.

Deliverables:
- BLG ingestion
- normalized metric model
- rule engine v1
- pack schema v1
- JSON + Markdown + HTML reports
- CLI analyze command
- initial Windows base pack
- initial SQL pack
- fixture-driven test suite

Exit criteria:
- can analyze real BLG files and generate useful reports without desktop UI dependency

## Phase 2 — Web/API product
**Objective:** make the engine operationally usable by teams.

Deliverables:
- REST API
- worker service
- web UI upload and run workflow
- findings browser
- export/download
- run history
- baseline storage

Exit criteria:
- team can upload a dataset and review/export results in browser

## Phase 3 — Comparison and correlation
**Objective:** improve diagnostic quality.

Deliverables:
- baseline comparison
- run-to-run diff
- event log correlation
- improved severity/confidence scoring
- timeline visualization
- compact ticket summary output

Exit criteria:
- product can explain what changed and why it matters

## Phase 4 — Source expansion and pack ecosystem
**Objective:** broaden usefulness without losing focus.

Deliverables:
- SQL enrichment inputs
- IIS log correlation
- ETW/WPA summary import
- pack registry
- pack validation UI
- custom pack authoring docs
- signed/distributed pack bundles

Exit criteria:
- internal teams can build and validate their own packs

## Phase 5 — Hosted/team product maturity
**Objective:** production-readiness for broader rollout.

Deliverables:
- auth/RBAC
- org/project scoping
- artifact retention controls
- queue scalability
- audit logging
- notifications/webhooks
- support bundle templates

Exit criteria:
- safe, supportable multi-user deployment

## 18. Success Metrics

### Product metrics
- time from upload to first useful finding
- percent of runs with at least one validated high-signal finding
- report export rate
- compare/baseline usage rate
- pack validation pass rate

### Engineering metrics
- ingestion success rate
- rule evaluation test coverage
- median analysis duration by dataset size
- false-positive/false-negative feedback rates

### User outcome metrics
- reduced time to triage
- reduced ticket back-and-forth
- improved consistency of diagnostics across engineers

## 19. Key Risks

1. **Over-expanding too early**
   Trying to become full observability too soon will blur the product.

2. **Rule pack quality debt**
   Weak or stale packs will damage trust quickly.

3. **BLG parsing complexity**
   Locale/version differences and counter availability can create fragility.

4. **False certainty**
   Threshold tools can sound more precise than they are. Findings must include confidence and prerequisites.

5. **UI overbuild**
   The engine and outputs matter more than fancy dashboards early on.

## 20. Recommendations

### Strategic recommendation
Do **not** merely refresh the legacy desktop app. Rebuild around a modern engine and keep the threshold-analysis concept.

### Build recommendation
Start with:
- engine
- pack schema
- report outputs
- CLI

Then add:
- API
- web workflow
- comparison/baselines
- source expansion

### Product positioning
Position PAL 2026 as:
- **diagnostic analysis for collected performance evidence**
- not a full always-on monitoring replacement
- not a generic dashboard
- a fast, explainable, repeatable performance troubleshooting platform

## 21. MVP Definition

A valid MVP should include:
- BLG ingestion
- Windows base rules
- SQL starter rules
- CLI analysis
- JSON/Markdown/HTML reports
- pack schema and validation
- fixture-based testing
- clear evidence and recommendations in findings

If those are strong, the product is already useful.

## 22. Open Questions

1. Should pack authoring be internal-only at first, or exposed to customers?
2. How far should SQL-specific enrichment go in early versions?
3. Should the first UI be fully web-based, or should CLI + reports ship first?
4. Do we want to support purely local/offline mode permanently?
5. Should baseline storage be per host, per workload, or per pack family?
6. How opinionated should severity scoring be across workloads?
7. How much ETW/WPA integration is enough before complexity explodes?

## 23. Conclusion

The path forward is clear:

Preserve PAL's core idea.
Modernize the architecture completely.
Treat analysis logic and rule packs as the product.
Expand inputs carefully.
Make outputs explainable, structured, and automation-friendly.

That approach keeps what made PAL valuable while making it relevant again.
