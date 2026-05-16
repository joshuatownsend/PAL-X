---
title: Reports
description: Stream the JSON / HTML / Markdown rendering of a completed job's report.
---

# Reports

| Endpoint | Verb | Auth |
|---|---|---|
| `…/analysis/{id}/report` | `GET` | required |

A completed API job has all three report formats persisted on disk: JSON (canonical), HTML (browser-friendly), and Markdown (GFM tables). The API worker writes all three unconditionally on every job completion.

## `GET /analysis/{id}/report`

Stream the report in the requested format.

### Query

| Param | Default | Values |
|---|---|---|
| `format` | `html` | `html`, `json`, `markdown` |

### Response

Streamed body. Headers:

- `Content-Type` — `text/html`, `application/json`, or `text/markdown` (all `charset=utf-8`).
- `Content-Disposition` — `attachment; filename="pal-report-<jobId>.{html,json,md}"`.

The bytes are exactly what the `JsonReportWriter` / `HtmlReportWriter` / `MarkdownReportWriter` produced at analysis time — UTF-8 without BOM, deterministic.

### Status codes

- `200 OK` — report streamed.
- `400 Bad Request` — `format` not one of the three.
- `404 Not Found` — job unknown, **or** no report of the requested format exists for this job (the API generates all three formats on completion, so a `404` here usually means the job was created before the format was added or the file has been pruned).
- `409 Conflict` — job is not `completed`.

### Examples

JSON (canonical) for downstream processing:

```bash
curl http://localhost:5043/api/workspaces/$WS/analysis/$JOB/report?format=json \
  -H "Authorization: Bearer pal_xxx" \
  -o report.json
```

HTML in a browser tab:

```bash
xdg-open "http://localhost:5043/api/workspaces/$WS/analysis/$JOB/report?format=html"
```

Markdown — generated unconditionally by the API worker; always retrievable via `?format=markdown` for completed jobs.

## Schema

The JSON form conforms to **[`pal.report/v1`](../report-schema.md)**.

## Related

- **[`pal remote report`](../cli/pal-remote-report.md)** — CLI front-end that hits this endpoint with a `--format` flag.
- **Markdown reports** — generated unconditionally by the API; available locally via `pal analyze --markdown`.
- **[Report schema](../report-schema.md)** — the JSON contract.
