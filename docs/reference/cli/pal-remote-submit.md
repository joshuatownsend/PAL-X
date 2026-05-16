---
title: pal remote submit
description: Upload a perfmon capture and queue a server-side analysis job.
---

# `pal remote submit`

Upload a CSV or BLG capture to a running PAL API server and queue an analysis job. Returns the new job ID for use with `pal remote status` / `report`.

## Synopsis

```text
pal remote submit [OPTIONS]
```

## Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. See **[pal remote](pal-remote.md#shared-options)**. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |
| `-f`, `--file <PATH>` | required | Path to the CSV or BLG to analyze. |
| `-p`, `--pack <PACK-ID>` | `windows-core` | Pack ID to run. Repeatable. Defaults to `windows-core` if omitted. |
| `--include-dataset` | off | Persist the normalized dataset artifact for later download with [`pal remote dataset`](pal-remote-dataset.md). |
| `--baseline <JOB-GUID>` | none | Auto-compare the new job's results against this baseline once analysis completes. |

## Examples

Submit a CSV with the default pack:

```bash
pal remote submit \
  --api $PAL_API --api-key $PAL_TOKEN \
  --file fixtures/cpu-pressure/input.csv
```

Submit a BLG, explicitly load two packs, and keep the dataset:

```bash
pal remote submit \
  --api $PAL_API --api-key $PAL_TOKEN \
  --file capture.blg \
  --pack windows-core --pack sql-host-core \
  --include-dataset
```

Submit and auto-compare against a known baseline job:

```bash
pal remote submit \
  --api $PAL_API --api-key $PAL_TOKEN \
  --file capture.csv \
  --baseline 9c2a14e0-...-d3b3
```

## What it does under the hood

1. `POST /uploads` with the file as multipart form data.
2. `POST /analysis` with the upload ID and pack list.
3. Prints the new job's ID and a one-liner showing how to poll it.

The submit operation is fast (upload + enqueue); the actual analysis runs on a background worker. Use **[pal remote status](pal-remote-status.md)** to track progress.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Job queued successfully. |
| `2` | `--file` does not exist, malformed `--baseline` GUID, or other argument error. |
| `1` | API unreachable, request timed out, or server returned a non-success status. A `404` typically means `--api` is missing the workspace path prefix. |

## Related

- **[pal remote status](pal-remote-status.md)** — poll the job until it completes.
- **[pal remote report](pal-remote-report.md)** — download the report.
- **[First analysis — remote API](../../getting-started/first-analysis-remote.md)** — walkthrough.
