---
title: Download a dataset
description: Submit a job with includeDataset=true, then retrieve the gzipped JSON dataset artifact for offline analysis.
---

# Download a dataset

Goal: get the normalised dataset that the rule engine ran against — the canonical-ID-tagged series with full samples — for offline analysis, custom rules, or long-term archive.

For the underlying endpoint, see **[HTTP API — Datasets](../reference/http-api/datasets.md)**.

## Opt-in at submit time

Datasets are **not** persisted by default. Submit the analysis with `includeDataset: true` to enable artifact generation:

### Via the CLI

```bash
pal remote submit \
  --file capture.csv \
  --pack windows-core \
  --include-dataset
```

### Via the HTTP API

```bash
curl -X POST http://localhost:5043/api/workspaces/$WS/analysis \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"uploadId\":\"$UPLOAD_ID\",\"packs\":[\"windows-core\"],\"includeDataset\":true}"
```

Without this flag, the dataset is held only in memory during analysis and discarded on completion.

## Retrieve once the job is complete

After the job reaches `completed`:

```bash
pal remote dataset $JOB_ID --output dataset.json.gz
```

Or directly via HTTP:

```bash
curl -s \
  -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5043/api/workspaces/$WS/analysis/$JOB_ID/dataset" \
  -o dataset.json.gz
```

The artifact is gzip-compressed JSON by default. Decompress:

```bash
gunzip dataset.json.gz
```

## What's inside

The dataset is a JSON document with one top-level `series` array. Each element:

```json
{
  "seriesId": "…",
  "counterPathOriginal": "\\\\WEB-01\\Processor(_Total)\\% Processor Time",
  "canonicalMetric": "processor.percent_processor_time",
  "instance": "_Total",
  "unit": "%",
  "samples": [
    { "timestamp": "2026-05-15T10:00:00Z", "value": 42.5 },
    { "timestamp": "2026-05-15T10:00:15Z", "value": 47.1 },
    …
  ],
  "statistics": { "count": 2880, "min": 12.1, "max": 99.7, "avg": 58.3, … }
}
```

Plus a `dataset` header carrying `datasetId`, `sampleIntervalSeconds`, `startTimeUtc`, `endTimeUtc`, and host context (if known).

This is the same structure that fed `RuleEngine.Evaluate` — every sample, every series, no aggregation lost.

## Storage and retention

The artifact lives at `<Storage:LocalRoot>/datasets/<jobId>/dataset.json.gz`. It's tied to the job's lifecycle:

- The `RetentionWorker` deletes it when the job exceeds `Retention:JobRetentionDays`. Default is `0` (keep forever); production deployments typically set it to 90.
- Deleting the job (not currently exposed via API) also deletes the artifact.

If you need long-term archival, copy the artifact out to your own storage before retention catches up.

## Use cases

**Replay with different packs.** If you developed a new pack and want to retro-evaluate it against historical captures, download the datasets and re-run analysis locally. The dataset is the canonical input — you don't need the original CSV/BLG.

**Custom analysis.** Load the dataset into a notebook, your own data pipeline, or a downstream tool that wants normalised series rather than raw Windows counter paths. The canonical metric IDs are stable across captures.

**Archive.** For compliance or postmortem retention, the dataset is denser than the original capture (gzipped JSON often half the size of CSV) and carries the canonical IDs, so it's analyzable without recreating the Windows context.

## Pitfalls

| Symptom | Cause | Fix |
|---|---|---|
| `404 Not Found "No dataset artifact for this job"` | Job was submitted without `includeDataset: true` | Re-submit with the flag |
| `404 Not Found "Dataset artifact file not found"` | Retention deleted the file (artifact metadata still exists) | Re-submit if the original capture is still around |
| `409 Conflict` | Job isn't `completed` yet | Wait or poll status |
| Very large file | Long capture with many counters | Compression typically brings it to ~30% of the original CSV size |

## Related

- **[HTTP API — Datasets](../reference/http-api/datasets.md)** — endpoint contract.
- **[CLI — `pal remote dataset`](../reference/cli/pal-remote-dataset.md)** — flags.
- **[Concepts — Datasets and inputs](../concepts/datasets-and-inputs.md)** — what canonical metric IDs are.
- **[Reference — Configuration: Retention](../reference/configuration.md#retention)** — when artifacts get cleaned up.
