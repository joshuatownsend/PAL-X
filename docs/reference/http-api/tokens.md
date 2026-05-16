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

The API doesn't accept HTTP Basic auth — the auth pipeline forwards non-`Bearer` `Authorization` headers to the cookie scheme. To mint your first token with curl, log in via form POST to capture a session cookie, then send the token-create request with that cookie:

```bash
WS=00000000-0000-0000-0000-000000000002

# 1. Log in; capture the auth cookie
curl -X POST http://localhost:5043/account/login \
  -c cookies.txt -L \
  -d "email=admin@example.com&password=<password>"

# 2. Mint the token (cookies.txt carries the auth)
curl -X POST "http://localhost:5043/api/workspaces/$WS/tokens" \
  -b cookies.txt \
  -H "Content-Type: application/json" \
  -d '{"name":"ci-runner"}'
```

The easier path is the Blazor UI at `/account/tokens`. The curl flow is documented because automation might prefer it.

## `DELETE /api/workspaces/{workspaceId}/tokens/{id}`

Revoke a token. Returns `204` on success, `404` if the token doesn't exist or belongs to a different user.

```bash
curl -X DELETE http://localhost:5043/api/workspaces/$WS/tokens/<id> \
  -H "Authorization: Bearer pal_xxx"
```

## Related

- **[Authentication](index.md#authentication)** — the two schemes share an authorization filter.
- **[`pal remote`](../cli/pal-remote.md)** — the CLI sends every request with the `Authorization: Bearer …` header.
