---
title: Baselines
description: Designate a completed job as a baseline; list baselines; enumerate versions of a baseline.
---

# Baselines

A baseline is a completed analysis job marked as a reference point — its findings, statistics, and metadata become the comparison anchor for new jobs. Designating a job as a baseline is **implicit versioning**: multiple baselines sharing the same `(type, contextJson)` are ordered by `createdAt` desc and treated as versions of the same logical baseline.

See [ADR 0001](../../architecture/adr/0001-deviations-from-seed-docs.md) for the rationale on `type` + `contextJson` over a separate "baseline" entity.

| Endpoint | Verb | Auth | Role |
|---|---|---|---|
| `…/analysis/{id}/baseline` | `PATCH` | required | `Analyst` |
| `…/analysis/baselines` | `GET` | required | any |
| `…/analysis/baselines/versions` | `GET` | required | any |

## `PATCH /analysis/{id}/baseline`

Toggle a job's baseline status. Mark it on, mark it off, change the type or context.

### Request

```json
{
  "isBaseline": true,
  "label": "Pre-deploy reference",
  "type": "release",
  "contextJson": "{\"release\":\"v2.5.0\"}"
}
```

| Field | Required | Notes |
|---|---|---|
| `isBaseline` | yes | `true` to designate, `false` to revoke. |
| `label` | no | Free-form description for the UI. |
| `type` | no | One of `machine`, `role`, `workload`, `release`. Lower-cased server-side. |
| `contextJson` | no | JSON string carrying the discriminator (e.g., `{"machine":"WEB-01"}`). Normalised server-side so equivalent JSON produces equivalent rows. |

### Responses

- `204 No Content` on success.
- `400 Bad Request` if `type` isn't one of the four, `contextJson` isn't valid JSON, or the job isn't `completed`.
- `404 Not Found` if the job doesn't exist.

### Example

```bash
curl -X PATCH http://localhost:5043/api/workspaces/$WS/analysis/$JOB/baseline \
  -H "Authorization: Bearer pal_xxx" \
  -H "Content-Type: application/json" \
  -d '{"isBaseline":true,"label":"v2.5.0 reference","type":"release","contextJson":"{\"release\":\"v2.5.0\"}"}'
```

## `GET /analysis/baselines`

List all baselines in the workspace, optionally filtered by type.

### Query

| Param | Notes |
|---|---|
| `type` | One of `machine`, `role`, `workload`, `release`. Omit to list every baseline. |

### Response

```json
{
  "items": [
    {
      "id": "…",
      "label": "v2.5.0 reference",
      "type": "release",
      "contextJson": "{\"release\":\"v2.5.0\"}",
      "createdAt": "…",
      "packs": ["windows-core@1.0.0"]
    }
  ]
}
```

`400 Bad Request` if `type` is given but invalid.

## `GET /analysis/baselines/versions`

List every baseline that shares the same `(type, contextJson)` — the implicit version history. Ordered by `createdAt` desc.

### Query

Both required:

| Param | Notes |
|---|---|
| `type` | One of `machine`, `role`, `workload`, `release`. |
| `contextJson` | JSON string. Normalised server-side. |

### Response

Same shape as `GET /analysis/baselines`. Most-recent baseline first.

### Example

```bash
curl -G "http://localhost:5043/api/workspaces/$WS/analysis/baselines/versions" \
  --data-urlencode 'type=release' \
  --data-urlencode 'contextJson={"release":"v2.5.0"}' \
  -H "Authorization: Bearer pal_xxx"
```

`400 Bad Request` on invalid `type` or unparseable `contextJson`.

## Related

- **[`pal remote baselines`](../cli/pal-remote-baselines.md)** — CLI list/version commands.
- **[Compare](compare.md)** — diff a candidate job against a baseline.
- **[Analysis jobs: `selectedBaselineId`](analysis-jobs.md#post-analysis)** — auto-compare on submission.
