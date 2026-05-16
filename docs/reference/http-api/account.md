---
title: Account
description: Login, logout, current user, and admin user management.
---

# Account

Handles cookie-based browser auth and admin-side user management. These endpoints live at the global root (no workspace prefix).

| Endpoint | Verb | Auth | Role |
|---|---|---|---|
| `/account/login` | `POST` | anonymous | — |
| `/account/logout` | `GET` | any | — |
| `/account/me` | `GET` | any | — |
| `/account/users` | `POST` | required | `Admin` |
| `/account/users` | `GET` | required | `Admin` |
| `/account/users/{id}` | `DELETE` | required | `Admin` |

## `POST /account/login`

Form POST — the browser sends credentials and receives a `Set-Cookie` directly. Antiforgery is disabled because the credentials in the form body already prevent CSRF.

### Request

`application/x-www-form-urlencoded`:

| Field | Type | Notes |
|---|---|---|
| `email` | string | Required. |
| `password` | string | Required. |
| `rememberMe` | bool | Optional. Omitted by browsers when unchecked. |

### Responses

- `302 Redirect` to `/jobs` on success.
- `302 Redirect` to `/account/login?error=invalid` on bad credentials.
- `302 Redirect` to `/account/login?error=locked` after 10 failed attempts (15-minute lockout).

Lockout settings live in `Program.cs`: `MaxFailedAccessAttempts = 10`, `DefaultLockoutTimeSpan = 15 minutes`.

## `GET /account/logout`

Signs out the current user and redirects to `/account/login`. Browser-navigable so the cookie is cleared.

## `GET /account/me`

Returns the current user's claims.

### Response

```json
{
  "id": "9e1c…",
  "email": "admin@example.com",
  "roles": ["Admin"]
}
```

### Example

```bash
curl -H "Authorization: Bearer pal_xxx" http://localhost:5043/account/me
```

## `POST /account/users`

Create a user. `Admin` role required.

### Request

```json
{
  "email": "user@example.com",
  "password": "secret-min-10-chars",
  "role": "Analyst",
  "displayName": "Optional display name"
}
```

- `role` must be one of `Admin`, `Analyst`, `Viewer`. Anything else is silently coerced to `Viewer`.
- `password` must be at least 10 characters (no non-alphanumeric requirement).

### Responses

- `200 OK` with `{ id, email, role }` on success.
- `400 Bad Request` with `{ errors: [...] }` on Identity validation failure.

## `GET /account/users`

List users. `Admin` only.

### Response

```json
{
  "items": [
    { "id": "…", "email": "admin@example.com", "displayName": "Admin" }
  ]
}
```

## `DELETE /account/users/{id}`

Delete a user. `Admin` only. Returns `204` on success, `404` if the id doesn't exist, `400` if Identity rejects the delete.

## Related

- **[Tokens](tokens.md)** — the API-key alternative for CLI / automation callers.
- **[Orgs and workspaces — members](orgs-and-workspaces.md)** — assigning a user to an org with a role.
- **[Getting started — remote](../../getting-started/first-analysis-remote.md)** — the bootstrap admin flow.
