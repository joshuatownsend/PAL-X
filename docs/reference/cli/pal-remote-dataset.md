---
title: pal remote dataset
description: Download the normalized dataset artifact for a completed job.
---

# `pal remote dataset`

Download the gzip-compressed JSON dataset artifact persisted alongside an analysis job — useful for re-analyzing offline, debugging a finding, or feeding into a separate tool.

The artifact only exists when the job was submitted with `--include-dataset` (CLI) or `includeDataset: true` (API).

## Synopsis

```text
pal remote dataset <job-id> [OPTIONS]
```

## Arguments

| Argument | Purpose |
|---|---|
| `<job-id>` | Analysis job ID (GUID). |

## Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |
| `-o`, `--output <PATH>` | required | File path to save the artifact (e.g. `dataset.json.gz`). |

## Example

```bash
pal remote dataset \
  --api $PAL_API --api-key $PAL_TOKEN \
  --output cpu-job.dataset.json.gz \
  9c2a14e0-...
```

Then decompress and inspect:

```bash
gunzip -c cpu-job.dataset.json.gz | jq '.metrics | length'
```

## What's in the artifact

A gzipped JSON document containing:

- `dataset_meta` — machine name, time zone, time range.
- `host_context` — RAM/CPU values used.
- `metrics` — for every canonical metric ID, every sample value over time.
- `instances` — instance values per metric.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Artifact saved. |
| `2` | Malformed job ID or missing `--output`. |
| `5` | Job not found, dataset not persisted for this job, or other server error. |

## Notes

- Dataset retention follows `Retention:JobRetentionDays` — once the job is purged, the dataset is too. Plan accordingly.
- The artifact is server-side-compressed; do not expect `--output mydataset.json` to give you raw JSON.

## Related

- **[pal remote submit](pal-remote-submit.md)** — pass `--include-dataset` on submit to make this artifact available later.
- **[pal remote report](pal-remote-report.md)** — the human-facing companion artifact.
