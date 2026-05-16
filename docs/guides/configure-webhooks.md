---
title: Configure webhooks
description: Register an HTTP sink for alert events, sign requests with HMAC-SHA256, and verify delivery with the test endpoint.
---

# Configure webhooks

Goal: register an HTTP endpoint that receives PAL-X alert events, configure signing, and verify delivery before relying on it in production.

For the lifecycle and event types, see **[Concepts — Alerting and notification](../concepts/alerting-and-notification.md)**. For the API contract, see **[HTTP API — Webhooks](../reference/http-api/webhooks.md)**.

## Create a webhook

`Admin` role required.

```bash
pal remote webhooks create \
  --name ops-slack \
  --url https://hooks.slack.com/services/T0000/B0000/abc \
  --secret 'shared-hmac-secret' \
  --event alert.created \
  --event alert.escalated \
  --event alert.acknowledged \
  --event alert.resolved
```

Available events today (Phase 4 v1):

- `alert.created` — new alert emitted from the policy evaluator.
- `alert.escalated` — warning escalated to critical by the 3-of-5 policy.
- `alert.acknowledged` — a user acknowledged.
- `alert.resolved` — a user resolved.

Subscribe to whichever subset you need. Sinks subscribed to zero events are rejected by the API.

There is **no** `analysis.completed` event in the current implementation. To watch for job completions, poll `pal remote status` — or the corresponding HTTP endpoint — until status is `completed`.

## List, get, and update

```bash
pal remote webhooks list
pal remote webhooks get <sinkId>

# Update — same shape as create, plus --update-secret if rotating
pal remote webhooks update <sinkId> \
  --name ops-slack \
  --url https://hooks.slack.com/services/T0000/B0000/abc \
  --enabled true \
  --event alert.created \
  --event alert.escalated
```

The secret is **never returned** by any read endpoint — only a `hasSecret: true/false` flag. Lost a secret? Rotate it.

## Test delivery

The most important step after creating a webhook is verifying it actually delivers:

```bash
pal remote webhooks test <sinkId>
```

Possible results:

- `delivered: true, httpStatus: 200` — your endpoint accepted a synthetic payload. You're good.
- `502 Bad Gateway, …` — delivery failed. Common causes:
  - Endpoint URL is wrong (typo, stale token).
  - Endpoint requires auth your webhook doesn't carry.
  - Network egress blocked from the PAL-X host.

Test after every create, every URL change, and every secret rotation.

## Signing — HMAC-SHA256

When you configure a `secret`, every outgoing webhook delivery carries:

```text
X-PAL-Signature: sha256=<hex>
```

`<hex>` is HMAC-SHA256 of the raw request body, hex-encoded.

A receiver in Node:

```javascript
const crypto = require('crypto');

function verifyPalSignature(secret, header, body) {
  const expected = 'sha256=' + crypto
    .createHmac('sha256', secret)
    .update(body)
    .digest('hex');
  // Constant-time comparison
  return crypto.timingSafeEqual(Buffer.from(header), Buffer.from(expected));
}
```

In Python:

```python
import hmac, hashlib

def verify_pal_signature(secret: bytes, header: str, body: bytes) -> bool:
    expected = 'sha256=' + hmac.new(secret, body, hashlib.sha256).hexdigest()
    return hmac.compare_digest(header, expected)
```

If `secret` is unset on the sink, no header is sent. The receiver should reject unsigned requests in production.

## Rotate a secret

To change the secret without disrupting other fields:

```bash
pal remote webhooks update <sinkId> \
  --secret 'new-shared-hmac-secret' \
  --update-secret \
  --name <unchanged> --url <unchanged> --event <unchanged> ...
```

The `--update-secret` flag is required — without it, the `--secret` value is ignored. This is so you can change name/url/events without accidentally rotating the secret.

## Rotation procedure with zero downtime

If your receiver verifies signatures, swap the secret in two phases:

1. Update the receiver to accept **either** the old or the new secret (during the rollover window).
2. Update the webhook sink with the new secret (`--update-secret`).
3. After verifying the next live event signed by the new secret, remove the old secret from the receiver.

There's no API-side dual-secret support — the sink stores one secret at a time. Dual-acceptance lives on the receiver side.

## Disable without deleting

To pause delivery temporarily without losing the sink config:

```bash
pal remote webhooks update <sinkId> --enabled false [other fields unchanged]
```

`enabled: false` means PAL-X drops the event silently — no retry queue. Re-enable when ready.

## Delete

```bash
pal remote webhooks delete <sinkId>
```

Returns `204`. The sink is gone; pending deliveries are not retried.

## Related

- **[Concepts — Alerting and notification](../concepts/alerting-and-notification.md)** — events, lifecycle, signing model.
- **[Configure alerts](configure-alerts.md)** — the upstream signal.
- **[HTTP API — Webhooks](../reference/http-api/webhooks.md)** — endpoint shapes.
- **[CLI — `pal remote webhooks`](../reference/cli/pal-remote.md)** — flag reference.
