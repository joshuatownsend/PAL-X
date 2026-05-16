---
title: Webhooks
description: Register and manage HTTP sinks that receive alert / analysis / schedule notifications.
---

# Webhooks

A webhook sink is a destination URL the `NotificationService` POSTs events to — alerts firing, analysis jobs completing, schedule failures. Each sink subscribes to one or more event types.

Read endpoints accept any authenticated user. Mutation and test endpoints require the `Admin` role.

| Endpoint | Verb | Auth | Role |
|---|---|---|---|
| `…/webhooks/data` | `GET` | required | any |
| `…/webhooks/{id}` | `GET` | required | any |
| `…/webhooks` | `POST` | required | `Admin` |
| `…/webhooks/{id}` | `PUT` | required | `Admin` |
| `…/webhooks/{id}` | `DELETE` | required | `Admin` |
| `…/webhooks/{id}/test` | `POST` | required | `Admin` |

## `GET /webhooks/data`

List all webhook sinks in the workspace. The `data` suffix avoids a Blazor route conflict.

### Response

```json
{
  "items": [
    {
      "id": "…",
      "name": "ops-slack",
      "url": "https://hooks.slack.com/…",
      "hasSecret": true,
      "enabled": true,
      "events": ["alert.fired", "analysis.completed"],
      "createdAt": "…",
      "updatedAt": "…"
    }
  ]
}
```

The actual secret is never returned — only `hasSecret` indicates whether one is configured.

## `GET /webhooks/{id}`

Get one sink. `404` if missing.

## `POST /webhooks`

Create a webhook. `Admin` only.

### Request

```json
{
  "name": "ops-slack",
  "url": "https://hooks.slack.com/services/T0000/B0000/abc",
  "secret": "shared-hmac-secret",
  "enabled": true,
  "events": ["alert.fired", "analysis.completed"]
}
```

Validation rules:

- `name` — required.
- `url` — required, absolute, `http` or `https` only.
- `events` — required, at least one.
- `secret` — optional. If present, outgoing requests carry `X-PAL-Signature: sha256=…` (HMAC-SHA256 over the body).

### Responses

- `201 Created` with `Location: /webhooks/{id}` and the sink body.
- `400 Bad Request` with `{ error: "…" }` on validation failure.

## `PUT /webhooks/{id}`

Update a webhook. `Admin` only. Same body shape as `POST`, plus:

| Field | Notes |
|---|---|
| `updateSecret` | If `true`, replaces the stored secret with `secret`. If `false`, the secret field is ignored (use this to update name/url/events without rotating the secret). |

- `204 No Content` on success.
- `404 Not Found` if the sink doesn't exist.
- `400 Bad Request` on validation failure.

## `DELETE /webhooks/{id}`

Delete a webhook. `204` on success, `404` if missing.

## `POST /webhooks/{id}/test`

Send a synthetic test event to the configured URL. `Admin` only.

### Response (success)

```json
{ "delivered": true, "httpStatus": 200 }
```

### Status codes

- `200 OK` — endpoint returned 2xx.
- `404 Not Found` — sink id unknown.
- `502 Bad Gateway` — delivery failed (network error or non-2xx response). The body includes the upstream status when known.

### Example

```bash
curl -X POST http://localhost:5043/api/workspaces/$WS/webhooks/$ID/test \
  -H "Authorization: Bearer pal_xxx"
```

## Signing

When a secret is configured, every outgoing webhook delivery carries:

```text
X-PAL-Signature: sha256=<hex>
```

Where `<hex>` is the HMAC-SHA256 of the request body, encoded as lowercase hex. Verify by recomputing on the receiver and constant-time-comparing.

## Related

- **[Alerts](alerts.md)** — the primary event source.
- **[Schedules](schedules.md)** — schedule failure events can target webhooks.
