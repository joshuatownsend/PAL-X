---
title: Health
description: Readiness probe — the only endpoint that's both anonymous and unprefixed.
---

# Health

| Endpoint | Verb | Auth | Scope |
|---|---|---|---|
| `/health` | `GET` | anonymous | global |

## `GET /health`

Returns the API's current status and version. Always returns `200 OK` if the process is running — does not check Postgres connectivity, pack registry sync state, or worker liveness. Use it as a liveness probe; for readiness, observe the API's startup logs or hit an authenticated endpoint.

### Response

```json
{
  "status": "ok",
  "version": "2026.2.0"
}
```

### Example

```bash
curl http://localhost:5043/health
```

## Related

- Liveness/readiness probe wiring lives under the Operations section. *(Coming up.)*
