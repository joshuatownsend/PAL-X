---
title: Compare
description: Diff two completed analysis jobs — new findings, resolved findings, drift in statistics.
---

# Compare

Compare two completed jobs and emit a structured diff: which findings appear in the candidate that weren't in the baseline, which were resolved, and which signals moved meaningfully. The diff is **deterministic** — same two inputs always produce the same `CompareResult` row.

`CompareRunner` does the diffing. Auto-compare via [`selectedBaselineId`](analysis-jobs.md#post-analysis) on submit calls into the same runner — manual compare and auto-compare are the same code path.

| Endpoint | Verb | Auth |
|---|---|---|
| `…/compare` | `POST` | required |
| `…/compare/list` | `GET` | required |
| `…/compare/{id}` | `GET` | required |

## `POST /compare`

Run a comparison and persist the result.

### Request

```json
{
  "baselineJobId": "…",
  "candidateJobId": "…"
}
```

Both jobs must be `completed`. They don't need to share the same pack set — the runner reports pack-set drift as part of the diff.

### Response

`201 Created` with `Location: /compare/{id}` and the persisted `CompareResult`:

```json
{
  "id": "…",
  "baselineJobId": "…",
  "candidateJobId": "…",
  "createdAt": "…",
  "summary": {
    "appearing": 2,
    "resolved": 1,
    "unchanged": 12,
    "worsening": 3,
    "improving": 1
  },
  "items": [...]
}
```

### Status codes

- `201 Created` on success.
- `400 Bad Request` if either job isn't `completed`.
- `404 Not Found` if either job id doesn't exist.
- `500 Internal Server Error` if either job's result data is missing on disk (shouldn't happen unless retention raced the request).

### Example

```bash
curl -X POST http://localhost:5043/api/workspaces/$WS/compare \
  -H "Authorization: Bearer pal_xxx" \
  -H "Content-Type: application/json" \
  -d '{"baselineJobId":"…","candidateJobId":"…"}'
```

## `GET /compare/list`

List all compare results in the workspace.

```json
{
  "items": [
    { "id": "…", "baselineJobId": "…", "candidateJobId": "…", "createdAt": "…" }
  ]
}
```

## `GET /compare/{id}`

Get one compare result by id. `404` if not found.

## Related

- **[`pal remote compare`](../cli/pal-remote-compare.md)** — CLI front-end with `--baseline` / `--candidate` flags.
- **[Baselines](baselines.md)** — designating a job to serve as a baseline.
- **[Analysis jobs: `selectedBaselineId`](analysis-jobs.md#post-analysis)** — auto-compare at submit time.
