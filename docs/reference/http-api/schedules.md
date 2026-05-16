---
title: Schedules
description: Configure recurring ingestion — list, create, update, enable/disable, delete.
---

# Schedules

A schedule polls a watched source directory on a fixed interval and submits any newly-arrived files as analysis jobs. The `ScheduledIngestionWorker` evaluates schedules on its `Schedules:TickIntervalSeconds` cadence (defaults to every 30 seconds — see [Configuration](../configuration.md#schedules)).

Read endpoints accept any authenticated user. Mutation endpoints require the `Admin` role.

| Endpoint | Verb | Auth | Role |
|---|---|---|---|
| `…/schedules/data` | `GET` | required | any |
| `…/schedules/{id}` | `GET` | required | any |
| `…/schedules` | `POST` | required | `Admin` |
| `…/schedules/{id}` | `PUT` | required | `Admin` |
| `…/schedules/{id}/enabled` | `PATCH` | required | `Admin` |
| `…/schedules/{id}` | `DELETE` | required | `Admin` |

## `GET /schedules/data`

List every schedule in the workspace. The `data` suffix avoids a Blazor route conflict.

### Response

```json
{
  "items": [
    {
      "id": "…",
      "name": "hourly-windows",
      "intervalMinutes": 60,
      "sourceConfigJson": "{\"directory\":\"/var/captures\",\"glob\":\"*.csv\"}",
      "packIds": ["windows-core"],
      "enabled": true,
      "lastRunAt": "…",
      "nextRunAt": "…",
      "createdAt": "…",
      "updatedAt": "…"
    }
  ]
}
```

## `GET /schedules/{id}`

Get one schedule. `404` if missing.

## `POST /schedules`

Create a schedule. `Admin` only.

### Request

```json
{
  "name": "hourly-windows",
  "intervalMinutes": 60,
  "sourceConfigJson": "{\"directory\":\"/var/captures\",\"glob\":\"*.csv\"}",
  "packIds": ["windows-core"],
  "enabled": true
}
```

- `name` — required, unique within the workspace.
- `intervalMinutes` — minutes between scheduled checks.
- `sourceConfigJson` — JSON describing where to watch. Today's only source type is a watched directory with a glob pattern.
- `packIds` — packs to apply to each submitted job.
- `enabled` — `false` to create paused.

### Status codes

- `201 Created` with `Location: …/schedules/{id}`.
- `400 Bad Request` from `IngestionScheduleValidationException` (malformed JSON, missing fields, invalid interval).
- `409 Conflict` if name is taken in the workspace (unique on `(workspace_id, name)`).

## `PUT /schedules/{id}`

Replace a schedule's body. Same shape as `POST`. `204 No Content` on success, `404` if missing.

## `PATCH /schedules/{id}/enabled`

Toggle the enabled flag without touching anything else.

### Request

```json
{ "enabled": false }
```

`204 No Content` on success, `404` if missing.

## `DELETE /schedules/{id}`

Delete the schedule. `204` on success, `404` if missing.

## Related

- **[`pal remote schedules`](../cli/pal-remote-schedules.md)** — CLI front-end for all five verbs.
- **[Configuration: Schedules](../configuration.md#schedules)** — tick interval, file-stable age, max files per tick.
- **[Webhooks](webhooks.md)** — schedule failure events can be routed to a webhook.
