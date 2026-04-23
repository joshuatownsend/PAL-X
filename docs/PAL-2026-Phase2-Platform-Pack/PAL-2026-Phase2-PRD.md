# PAL 2026 — Phase 2 Product Requirements Document

## 1. Purpose

Phase 2 converts PAL from a local analysis utility into a headless diagnostics platform that can be called by scripts, pipelines, scheduled jobs, and a thin web application.

Phase 1 established:
- deterministic ingestion and normalization
- pack-driven rule execution
- structured findings and reports
- local CLI execution

Phase 2 adds:
- REST API
- asynchronous job model
- persisted job and result storage
- pack registry and version control
- web UI for submission and result review
- remote-capable CLI

## 2. Problem Statement

Classic PAL is strong at post-hoc analysis of PerfMon logs, but weak as an operational capability. It is difficult to integrate into:
- centralized troubleshooting workflows
- repeatable support processes
- CI/CD and load-test gates
- scheduled performance health checks
- multi-user engineering review

The system must preserve the deterministic value of PAL while making it callable, repeatable, and composable.

## 3. Product Vision

A platform that accepts one or more telemetry artifacts, executes one or more analysis packs asynchronously, stores results, and returns human-readable and machine-readable outputs through a clean API and thin UI.

## 4. Goals

1. Wrap the Phase 1 engine without rewriting it.
2. Enable remote and automated execution.
3. Persist artifacts, runs, and outputs for later review.
4. Support pack versioning and compatibility metadata.
5. Provide a thin engineer-focused UI for real-world use.
6. Preserve deterministic, explainable rule execution.

## 5. Non-Goals

Phase 2 does not include:
- anomaly detection
- machine learning scoring
- OpenTelemetry-native ingestion
- deep ETW parsing
- multi-tenant SaaS billing
- external marketplace for packs
- role-rich enterprise administration

## 6. Primary Users

### 6.1 Platform operator
Runs and maintains the PAL service and worker infrastructure.

### 6.2 Performance engineer / sysadmin
Uploads a file or submits a job, selects packs, and reviews findings.

### 6.3 Automation pipeline
Uses the API or CLI to submit jobs and consume JSON results.

## 7. Key Use Cases

### 7.1 Manual analysis from UI
Engineer uploads a BLG and selects:
- Windows Server pack
- SQL Server pack

System queues job, processes it, and exposes:
- summary
- findings
- report
- downloadable JSON

### 7.2 API-driven support workflow
A script uploads an artifact bundle and polls for completion. On success it attaches the report and JSON to a ticket.

### 7.3 Load-test gate
A pipeline submits exported metrics after a test run. If critical findings are present, the pipeline fails.

### 7.4 Scheduled fleet analysis
A scheduler submits recurring files from selected systems and stores findings for comparison and review.

## 8. Functional Requirements

### 8.1 Analysis submission
The platform must support:
- file upload submission
- reference-based submission for previously uploaded artifacts
- one or more selected packs
- optional metadata such as target system, environment, workload, operator notes

### 8.2 Asynchronous jobs
The platform must:
- create a job record
- queue work
- execute in a worker
- update state transitions
- expose progress and failure information

### 8.3 Results retrieval
The platform must expose:
- job metadata
- structured findings JSON
- HTML report
- Markdown report
- downloadable raw normalized dataset where configured

### 8.4 Pack management
The platform must support:
- pack listing
- active/inactive state
- version metadata
- compatibility metadata
- pack validation before use

### 8.5 CLI over API
The CLI must be able to:
- submit jobs
- check status
- download results
- download reports
- list packs

### 8.6 Thin web UI
The UI must include:
- submit analysis page
- jobs page
- job detail page
- findings explorer
- report viewer

## 9. Quality Attributes

### 9.1 Reliability
- jobs must be resumable or clearly failed
- uploads must be content-addressed or otherwise de-duplicated where practical
- worker failures must not corrupt job records

### 9.2 Performance
- API must return job acceptance quickly
- large file work must occur only in workers
- results retrieval should not require recomputation

### 9.3 Explainability
- each finding must include rule source, threshold logic, and rationale
- reports must retain deterministic references to pack and rule versions

### 9.4 Security
- uploaded artifacts must be access controlled
- administrative operations must be gated
- audit events should be retained for job submission and pack changes

## 10. Constraints

- Phase 1 engine contracts should remain stable
- API and worker must not embed report-specific logic that belongs in the engine
- storage model should support future comparison and baseline features
- outputs should remain schema-first

## 11. Success Criteria

Phase 2 is complete when:
- a BLG can be submitted through the API
- the worker processes it asynchronously
- results are stored and retrieved through the API
- the CLI operates through the API
- packs can be listed and version-validated
- the web UI supports submission and review
- automation workflows can consume machine-readable output

## 12. Release Recommendation

Release Phase 2 first as an internal engineering platform, not a public product. Optimize for trust, reproducibility, and clean contracts before UI polish.
