---
title: Uploads
description: Submit a CSV or BLG file for later analysis.
---

# Uploads

| Endpoint | Verb | Auth | Content-Type |
|---|---|---|---|
| `…/uploads` | `POST` | required | `multipart/form-data` |

## `POST /api/workspaces/{workspaceId}/uploads`

Upload a performance counter file. The bytes are SHA-256-hashed; if the hash matches an existing upload the API returns the existing record instead of duplicating storage.

### Request

`multipart/form-data` with:

| Field | Required | Notes |
|---|---|---|
| `file` | yes | The file content. Max 512 MB (`Kestrel.Limits.MaxRequestBodySize`). |
| `sourceType` | no | `csv` or `blg`. If omitted, inferred from the file extension. |

### Response

```json
{
  "uploadId": "…",
  "fileName": "cpu-pressure.csv",
  "sourceType": "csv"
}
```

### Status codes

- `201 Created` — new upload, `Location: /api/workspaces/{workspaceId}/uploads/{uploadId}`.
- `200 OK` — duplicate by SHA-256; the existing record is returned and the new bytes discarded.
- `400 Bad Request` — missing `file` field or not a multipart request.

### Example

```bash
WS=00000000-0000-0000-0000-000000000002
curl -X POST http://localhost:5043/api/workspaces/$WS/uploads \
  -H "Authorization: Bearer pal_xxx" \
  -F file=@fixtures/cpu-pressure/input.csv
```

## Storage layout

The committed file lives at `<Storage:LocalRoot>/uploads/<workspaceId>/<sha256>/<originalFileName>`. Deduping by SHA-256 prevents re-uploading the same capture from costing disk again.

## Related

- **[Analysis jobs: `POST /analysis`](analysis-jobs.md#post-analysis)** — the next step: submit an analysis referencing this `uploadId`.
- **[`pal remote submit`](../cli/pal-remote-submit.md)** — does both calls (upload + submit) in one go.
- **[Configuration: Storage](../configuration.md#storage)** — where uploads land on disk.
