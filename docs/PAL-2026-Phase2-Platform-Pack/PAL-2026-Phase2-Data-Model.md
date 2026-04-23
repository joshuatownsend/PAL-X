# PAL 2026 — Phase 2 Data Model

## 1. Overview

Phase 2 requires persistent storage for:
- uploaded artifacts
- analysis jobs
- structured results
- generated reports
- pack registry metadata
- audit events

Recommended baseline:
- Postgres for relational metadata and JSONB result payloads
- object storage for raw uploads and report artifacts

## 2. Core Tables

## uploads

| Column | Type | Notes |
|---|---|---|
| id | uuid PK | upload id |
| file_name | text | original file name |
| source_type | text | blg, csv, json, zip |
| content_type | text | MIME type |
| size_bytes | bigint | file size |
| sha256 | text | content hash |
| object_key | text | blob/object storage key |
| created_at | timestamptz | creation time |

## analysis_jobs

| Column | Type | Notes |
|---|---|---|
| id | uuid PK | analysis id |
| upload_id | uuid FK uploads(id) | source upload |
| status | text | queued, running, completed, failed, canceled |
| requested_by | text | user/service identity |
| options_json | jsonb | run options |
| context_json | jsonb | target/environment/workload metadata |
| created_at | timestamptz | creation time |
| started_at | timestamptz | worker start time |
| completed_at | timestamptz | end time |
| failure_reason | text | failure detail |

## analysis_job_packs

| Column | Type | Notes |
|---|---|---|
| analysis_job_id | uuid FK analysis_jobs(id) | |
| pack_id | text | canonical pack id |
| pack_version | text | resolved version at execution |
| PRIMARY KEY | composite | analysis_job_id + pack_id |

## analysis_results

| Column | Type | Notes |
|---|---|---|
| analysis_job_id | uuid PK FK analysis_jobs(id) | |
| summary_json | jsonb | roll-up summary |
| findings_json | jsonb | full findings |
| normalized_data_json | jsonb nullable | optional retained normalized data |
| generated_at | timestamptz | write timestamp |

## analysis_reports

| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| analysis_job_id | uuid FK analysis_jobs(id) | |
| format | text | html, markdown |
| object_key | text | blob/object location |
| size_bytes | bigint | report size |
| created_at | timestamptz | |

## packs

| Column | Type | Notes |
|---|---|---|
| id | text PK | canonical id |
| current_version | text | default version |
| title | text | display title |
| status | text | active, inactive, deprecated |
| metadata_json | jsonb | supportedTargets, inputTypes, etc. |
| created_at | timestamptz | |
| updated_at | timestamptz | |

## pack_versions

| Column | Type | Notes |
|---|---|---|
| pack_id | text FK packs(id) | |
| version | text | semantic version |
| schema_version | text | pack schema version |
| object_key | text | storage path for pack file |
| checksum | text | integrity |
| compatibility_json | jsonb | version compatibility |
| created_at | timestamptz | |
| PRIMARY KEY | composite | pack_id + version |

## audit_events

| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| actor | text | user/service identity |
| event_type | text | upload.created, analysis.submitted, pack.updated |
| entity_id | text | id of affected entity |
| event_json | jsonb | event body |
| created_at | timestamptz | |

## 3. Suggested Indexes

- uploads(sha256)
- analysis_jobs(status, created_at desc)
- analysis_jobs(upload_id)
- analysis_job_packs(pack_id, pack_version)
- analysis_results using GIN on findings_json
- audit_events(event_type, created_at desc)

## 4. Design Notes

### Immutable execution metadata
Each run should record the exact pack version used. Do not rely on current pack version for historical analysis.

### Object storage boundary
Store large payloads and report bodies outside Postgres unless intentionally caching small markdown results.

### Future compatibility
This model supports Phase 3 features such as:
- comparison runs
- baselines
- saved target profiles
- recurring schedules
