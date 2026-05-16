---
title: Tokens
description: Mint, list, and revoke API keys for CLI / automation callers.
---

# Tokens

API keys are workspace-scoped — the route group lives under `/api/workspaces/{workspaceId:guid}/tokens`. They authenticate the same user across workspaces (the user identity is what carries; the workspace prefix just enforces which tenant context the call runs under).

| Endpoint | Verb | Auth | Role |
|---|---|---|---|
| `…/tokens` | `GET` | required | any |
| `…/tokens` | `POST` | required | any |
| `…/tokens/{id}` | `DELETE` | required | any |

## `GET /api/workspaces/{workspaceId}/tokens`

List the calling user's tokens. The raw token value is **not** returned — only metadata. Lost a token? Delete and recreate it.

### Response

```json
{
  "items": [
    {
      "id": "…",
      "name": "ci-runner",
      "createdAt": "2026-05-15T10:00:00Z",
      "lastUsedAt": "2026-05-15T10:23:14Z",
      "expiresAt": null
    }
  ]
}
```

## `POST /api/workspaces/{workspaceId}/tokens`

Create a token. **The raw value is returned exactly once.** SHA-256 of the token is stored; the raw value is non-recoverable.

### Request

```json
{
  "name": "ci-runner",
  "expiresAt": "2027-01-01T00:00:00Z"
}
```

- `name` — required, free-form label.
- `expiresAt` — optional. Omit for a non-expiring token (the lifecycle is then bounded by manual `DELETE`).

### Response

```json
{
  "id": "…",
  "name": "ci-runner",
  "createdAt": "2026-05-15T10:23:14Z",
  "expiresAt": "2027-01-01T00:00:00Z",
  "token": "pal_AbCdEf0123…"
}
```

Use `token` thereafter as `Authorization: Bearer pal_AbCdEf0123…`.

### Example

```bash
WS=00000000-0000-0000-0000-000000000002
curl -X POST http://localhost:5043/api/workspaces/$WS/tokens \
  -H "Content-Type: application/json" \
  -u admin@example.com:password \
  -d '{"name":"ci-runner"}'
```

(For the bootstrap call the cookie isn't available, so Basic auth via `-u` is the only way to mint the first token. See [Getting started — remote](../../getting-started/first-analysis-remote.md).)

## `DELETE /api/workspaces/{workspaceId}/tokens/{id}`

Revoke a token. Returns `204` on success, `404` if the token doesn't exist or belongs to a different user.

```bash
curl -X DELETE http://localhost:5043/api/workspaces/$WS/tokens/<id> \
  -H "Authorization: Bearer pal_xxx"
```

## Related

- **[Authentication](index.md#authentication)** — the two schemes share an authorization filter.
- **[`pal remote`](../cli/pal-remote.md)** — the CLI sends every request with the `Authorization: Bearer …` header.
