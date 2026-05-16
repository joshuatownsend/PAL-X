---
title: Alerts
description: List, acknowledge, resolve, and snooze alerts emitted by policy evaluation.
---

# Alerts

Alerts are emitted by the `PolicyEvaluator` when a completed job's findings match a policy condition. Each alert has a lifecycle (`open` → `acknowledged` → `resolved`) and can be temporarily silenced via snooze.

Read endpoints accept any authenticated user. Mutation endpoints require the `Analyst` role.

| Endpoint | Verb | Auth | Role |
|---|---|---|---|
| `…/alerts/data` | `GET` | required | any |
| `…/alerts/{id}` | `GET` | required | any |
| `…/alerts/{id}/acknowledge` | `PATCH` | required | `Analyst` |
| `…/alerts/{id}/resolve` | `PATCH` | required | `Analyst` |
| `…/alerts/{id}/snooze` | `PATCH` | required | `Analyst` |
| `…/alerts/{id}/snooze` | `DELETE` | required | `Analyst` |

## `GET /alerts/data`

List alerts, optionally filtered by status and severity.

The path is `/alerts/data` (not `/alerts`) to avoid a Blazor routing conflict — same pattern as [Trends](trends.md) and [Correlations](correlations.md).

### Query

| Param | Notes |
|---|---|
| `status` | `open`, `acknowledged`, `resolved`. Omit for all. |
| `severity` | `critical`, `warning`. Omit for all. |

### Response

```json
{
  "items": [
    {
      "id": "…",
      "title": "Sustained high CPU utilization",
      "severity": "warning",
      "status": "open",
      "ruleId": "high-cpu-sustained",
      "jobId": "…",
      "createdAt": "…",
      "snoozedUntil": null
    }
  ]
}
```

## `GET /alerts/{id}`

Get one alert. `404` if not found.

## `PATCH /alerts/{id}/acknowledge`

Move an `open` alert to `acknowledged`. No body required.

- `204 No Content` on success.
- `404 Not Found` if the alert doesn't exist.
- `409 Conflict` if the alert isn't in `open` state.

## `PATCH /alerts/{id}/resolve`

Move an alert to `resolved`. Optional resolution note.

### Request

```json
{ "note": "Auto-resolved after pool recycle." }
```

- `204 No Content` on success.
- `404 Not Found` if missing.
- `409 Conflict` if already resolved.

## `PATCH /alerts/{id}/snooze`

Silence an alert until a future timestamp.

### Request

```json
{ "until": "2026-05-16T08:00:00Z" }
```

- `until` must be in the future. A 30-second skew window is tolerated.
- `until` cannot be more than 30 days in the future — prevents accidental "forever" snoozes.

### Status codes

- `204 No Content` on success.
- `400 Bad Request` if `until` is in the past or beyond 30 days.
- `404 Not Found` if the alert doesn't exist.
- `409 Conflict` if the alert is already resolved.

## `DELETE /alerts/{id}/snooze`

Clear the snooze (return to active state).

- `204 No Content` on success.
- `404 Not Found` if missing.
- `409 Conflict` if the alert is resolved.

## Related

- **[`pal remote alerts`](../cli/pal-remote-alerts.md)** — CLI front-end for all five verbs.
- **[Webhooks](webhooks.md)** — sinks the notification service delivers alerts to.
