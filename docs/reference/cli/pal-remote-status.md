---
title: pal remote status
description: Poll the status of an analysis job.
---

# `pal remote status`

Print the current status of an analysis job by GUID.

## Synopsis

```text
pal remote status <job-id> [OPTIONS]
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

## Examples

```bash
pal remote status \
  --api $PAL_API --api-key $PAL_TOKEN \
  9c2a14e0-1234-5678-9abc-def012345678
```

## Status values

| Status | Meaning |
|---|---|
| `queued` | The job is in the worker queue. |
| `running` | The worker is analyzing the dataset. |
| `completed` | The report is available. Use [`pal remote report`](pal-remote-report.md). |
| `failed` | Analysis errored. Server-side logs explain why; consult `docker compose logs -f pal-api`. |

If the job stays `queued` for more than a few seconds, the background `AnalysisWorker` may be stalled.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Status retrieved. |
| `2` | Malformed job ID. |
| `1` | API unreachable, job not found, or other server error. |

## Related

- **[pal remote results](pal-remote-results.md)** — show the findings once status is `completed`.
- **[pal remote report](pal-remote-report.md)** — download the full report.
