---
title: Datasets
description: Download the gzipped JSON dataset snapshot of a completed job.
---

# Datasets

| Endpoint | Verb | Auth |
|---|---|---|
| `…/analysis/{id}/dataset` | `GET` | required |

A "dataset" is the normalised time-series the analyzer ran against — counters, instances, samples, statistics. It's the input to the rule engine, captured for reproducibility and downstream analysis.

Datasets are **opt-in**: only jobs submitted with `includeDataset: true` get one. Without that flag, the dataset is held only in memory during analysis and never persisted.

## `GET /api/workspaces/{workspaceId}/analysis/{id}/dataset`

Stream the dataset artifact for one job.

### Response

The body is the dataset's JSON, optionally gzip-compressed. Compression is the persistence default.

Headers:

- `Content-Type` — `application/gzip` (default) or `application/json; charset=utf-8`.
- `Content-Disposition` — `attachment; filename="pal-dataset-<jobId>.json.gz"` (or `.json`).

### Status codes

- `200 OK` — stream returned.
- `404 Not Found` — job unknown, **or** no dataset artifact exists for this job (submit with `includeDataset: true` to generate one), **or** the on-disk file is missing (server logs warn when this happens).
- `409 Conflict` — job is not `completed`.

### Example

```bash
curl http://localhost:5043/api/workspaces/$WS/analysis/$JOB/dataset \
  -H "Authorization: Bearer pal_xxx" \
  -o dataset.json.gz
gunzip dataset.json.gz
jq '.series | length' dataset.json
```

## Storage

Persisted to `<Storage:LocalRoot>/datasets/<jobId>/dataset.json.gz`. The `RetentionWorker` deletes it when the job passes its `Retention:JobRetentionDays` horizon — datasets are tied to their job's lifecycle.

## Related

- **[`pal remote dataset`](../cli/pal-remote-dataset.md)** — CLI front-end for this endpoint with output-path flag.
- **[Configuration: Storage](../configuration.md#storage)** / **[Retention](../configuration.md#retention)** — where datasets live and how they're cleaned up.
- **[Analysis jobs](analysis-jobs.md#post-analysis)** — set `includeDataset: true` here.
