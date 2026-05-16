---
title: Orgs and workspaces
description: Multi-tenant hierarchy — create orgs, workspaces, and member roles.
---

# Orgs and workspaces

PAL-X is two-level multi-tenant: an **Org** contains many **Workspaces**, and every data-plane resource carries a `WorkspaceId`. Users are members of orgs (with a role); the workspace they operate under is selected by the URL prefix on every data-plane request.

All endpoints below require the `Admin` role.

| Endpoint | Verb |
|---|---|
| `/api/orgs` | `GET`, `POST` |
| `/api/orgs/{orgId}` | `GET` |
| `/api/orgs/{orgId}/workspaces` | `GET`, `POST` |
| `/api/orgs/{orgId}/members` | `GET` |
| `/api/orgs/{orgId}/members/{userId}` | `PUT`, `DELETE` |

## `GET /api/orgs`

List all orgs. `Admin` only.

```json
{
  "items": [
    { "id": "…", "name": "Acme", "slug": "acme", "createdAt": "…" }
  ]
}
```

## `POST /api/orgs`

Create an org.

### Request

```json
{ "name": "Acme", "slug": "acme" }
```

- `slug` must be unique across all orgs.

### Responses

- `201 Created` with `{ id, name, slug, createdAt }`.
- `400 Bad Request` if name or slug missing.
- `409 Conflict` if slug is taken.

## `GET /api/orgs/{orgId}`

Get one org's details. `404` if not found.

## `GET /api/orgs/{orgId}/workspaces`

List workspaces in an org.

```json
{
  "items": [
    { "id": "…", "name": "Default", "slug": "default", "createdAt": "…" }
  ]
}
```

## `POST /api/orgs/{orgId}/workspaces`

Create a workspace.

### Request

```json
{ "name": "Default", "slug": "default" }
```

- `slug` must be unique **within the org** (not globally).

### Responses

- `201 Created` with `{ id, name, slug, orgId, createdAt }`.
- `400 Bad Request` if name or slug missing.
- `404 Not Found` if org doesn't exist.
- `409 Conflict` if slug taken within this org.

## `GET /api/orgs/{orgId}/members`

List org members and their roles.

```json
{
  "items": [
    { "userId": "…", "email": "admin@example.com", "role": "Admin" }
  ]
}
```

## `PUT /api/orgs/{orgId}/members/{userId}`

Add or update a user's role in an org. Idempotent — calling twice is fine.

### Request

```json
{ "role": "Analyst" }
```

- `role` must be one of `Admin`, `Analyst`, `Viewer`.

### Responses

- `204 No Content` on success.
- `400 Bad Request` if role isn't one of the three.
- `404 Not Found` if the org doesn't exist.

## `DELETE /api/orgs/{orgId}/members/{userId}`

Remove a user from an org. Returns `204` on success, `404` if no membership existed.

## Related

- **[Default tenant](index.md#default-tenant)** — the seeded org/workspace IDs you can use out of the box.
- **[Workspace routing](index.md#workspace-routing)** — how the workspace prefix is enforced.
- **[Account: user management](account.md#post-accountusers)** — creating the user before adding them to an org.
