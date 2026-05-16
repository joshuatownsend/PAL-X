---
title: Alerting and notification
description: How alerts are emitted from analysis runs, how they flow through lifecycle, and how webhooks deliver them.
---

# Alerting and notification

Three pieces, one workflow:

- **Schedules** ingest captures on a recurring cadence.
- **Analysis runs** fire findings.
- **Alerts** are emitted from findings via a policy; webhooks deliver them.

This page covers the model. Endpoint contracts live under **[HTTP API — Alerts](../reference/http-api/alerts.md)** / **[Webhooks](../reference/http-api/webhooks.md)** / **[Schedules](../reference/http-api/schedules.md)**.

## What gets alerted

Not every finding becomes an alert. The `PolicyEvaluator` (Phase 4 v1) decides:

- **Any current `critical` finding** → alert.
- **A current `warning` finding** that has also fired in 3 of the last 5 completed jobs → alert (escalated to `critical`).
- Otherwise → no alert.

The intent is to silence one-off blips while still surfacing persistent warnings. A warning that fires once isn't an alert; a warning that fires 3 out of 5 times *is*, and is escalated to critical severity to reflect the persistence.

Suppression rules (e.g., maintenance windows, per-rule mute) are not yet implemented in v1 — the policy is a single hard-coded "3 of 5" check.

## Alert lifecycle

An alert has three states and one orthogonal silence:

```text
open  →  acknowledged  →  resolved
     ↘                ↗
      ─── (skip ack) ──

(at any non-resolved state, can be snoozed until <future timestamp>)
```

| State | What it means |
|---|---|
| `open` | New alert; no human has touched it yet. |
| `acknowledged` | A human signed off that they've seen it. Doesn't mean the underlying issue is fixed. |
| `resolved` | The underlying issue is considered closed. Optional resolution note for postmortem context. |
| `snoozed` | The alert is silenced until a future timestamp. Snooze is orthogonal — an acknowledged-but-snoozed alert is valid. Max 30-day snooze (prevents accidental "forever" silence). |

Transitions:

- `open` → `acknowledged`: PATCH `/alerts/{id}/acknowledge`.
- `open` or `acknowledged` → `resolved`: PATCH `/alerts/{id}/resolve` with an optional note.
- Any non-resolved state → snoozed: PATCH `/alerts/{id}/snooze` with `until: <timestamp>`.
- Snoozed → un-snoozed: DELETE `/alerts/{id}/snooze`.

All mutation endpoints require the `Analyst` role; reads accept any authenticated user.

## Webhook events

The `NotificationService` posts JSON to every webhook subscribed to the event type. **Today's actual event surface is alert-lifecycle only** — four events:

- `alert.created` — a new alert was just emitted.
- `alert.escalated` — a warning was escalated to critical by the 3-of-5 policy.
- `alert.acknowledged` — a user acknowledged.
- `alert.resolved` — a user resolved.

There is no `analysis.completed` event in the current implementation — submit-and-poll is the pattern for caring about job completion. Future work may add more events.

## Webhook signing

Each webhook can carry a shared secret. When set, outgoing deliveries include:

```text
X-PAL-Signature: sha256=<hex>
```

`<hex>` is the HMAC-SHA256 of the request body, hex-encoded. Receivers verify by recomputing on their side and constant-time-comparing. If the secret is unset, no header is sent.

This is the standard webhook signing pattern. There's no rotation API — to rotate, update the sink with a new `secret` and `updateSecret: true`.

## Test delivery

Every sink has a test endpoint: `POST /webhooks/{id}/test`. The notification service sends a synthetic payload to the configured URL. The response carries the upstream HTTP status:

- `200 OK { delivered: true, httpStatus: 200 }` — success.
- `502 Bad Gateway` — delivery failed (network error or non-2xx response).

Use the test endpoint after creating a new sink and after rotating a secret.

## Schedules — the trigger upstream

Schedules trigger ingestion: every `intervalMinutes`, the `ScheduledIngestionWorker` checks a configured source for new files, picks up any that have been stable for at least `Schedules:FileStableAgeSeconds`, and submits them as analysis jobs.

Source config today is a JSON blob describing one directory + glob pattern:

```json
{ "directory": "/var/captures", "glob": "*.csv" }
```

Future versions may add SMB, S3, etc.; today the directory poller is the only source type.

Schedule failures (validation errors, source unreachable) are logged but don't fire alerts today. That's a gap in Phase 4 v1.

## End-to-end shape

A complete production loop:

```text
[ perfmon DCS ] →  /var/captures/*.csv
                        │
                    (every 60 min)
                        ▼
              [ ScheduledIngestionWorker ]
                        │
                        ▼
               [ AnalysisWorker → job ]
                        │
              [ PolicyEvaluator on findings ]
                        │
              ┌─────────┴─────────┐
              │                   │
           (no alert)        (alert created)
                                  │
                       [ NotificationService → webhook ]
                                  │
                       [ ops team triages ]
                                  │
                       (acknowledged → resolved)
```

Every arrow is rule-based and traceable. There's no inference layer.

## Related

- **[Configure alerts](../guides/configure-alerts.md)** — policy and lifecycle workflow.
- **[Configure webhooks](../guides/configure-webhooks.md)** — sink creation, signing, testing.
- **[Schedule ingestion](../guides/schedule-ingestion.md)** — recurring capture-to-analysis loop.
- **[HTTP API — Alerts](../reference/http-api/alerts.md)** / **[Webhooks](../reference/http-api/webhooks.md)** / **[Schedules](../reference/http-api/schedules.md)** — endpoint contracts.
