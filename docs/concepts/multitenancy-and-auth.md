---
title: Multitenancy and auth
description: How orgs, workspaces, and the two auth schemes (API key + cookie) compose into PAL-X's access model.
---

# Multitenancy and auth

PAL-X is two-level multi-tenant: an **Org** contains **Workspaces**, and every data-plane resource carries a `WorkspaceId`. Two auth schemes share the same authorisation pipeline — API keys for automation, cookies for browsers.

For the API contract, see **[HTTP API — Index (authentication, authorisation, workspace routing)](../reference/http-api/index.md)**.

## Why two levels

A single-level "tenant" model conflates two concerns:

- **Billing / contract / identity** — the organisation as a legal entity.
- **Workload isolation** — a project, environment, or team that wants its own data scope.

PAL-X separates these into Org and Workspace. One org can have many workspaces (one per environment, one per major customer segment, etc.). Users are members of orgs; they operate within a workspace by hitting that workspace's URL prefix.

```text
┌─────────── Org A ──────────────┐    ┌─── Org B ───┐
│                                │    │             │
│  Workspace "prod"  ──┐         │    │ Workspace   │
│                      ├─ jobs   │    │ "default"   │
│  Workspace "stage" ──┤         │    │             │
│                      │         │    └─────────────┘
│  Workspace "dev"   ──┘         │
└────────────────────────────────┘

users[A] ⊆ Org A members          users[B] ⊆ Org B members
```

A user in Org A cannot see anything in Org B, regardless of route.

## Workspace routing

Data-plane endpoints all live under:

```text
/api/workspaces/{workspaceId:guid}/...
```

The `TenantResolutionEndpointFilter` runs **before** the handler and verifies:

1. The `workspaceId` URL param parses as a Guid.
2. A workspace with that id exists.
3. The calling user is a member of the org that owns the workspace.

Any of these failing returns `403 Forbidden` before the handler sees the request. This makes the tenant boundary load-bearing and rejection cheap.

EF Core's global query filters then scope every database query by the resolved workspace id — repositories cannot accidentally leak data from another workspace, even if the developer forgets to filter.

## Default tenant

To make the API usable on a fresh install without a multi-step setup, the `IdentitySeeder` creates one default org and one default workspace on first boot:

```text
DefaultTenant.OrgId       = 00000000-0000-0000-0000-000000000001
DefaultTenant.WorkspaceId = 00000000-0000-0000-0000-000000000002
```

The seeded admin user (configured in `appsettings.json`) is added as an Admin member of the default org. This is the workspace ID you use in URLs until you create your own.

For production, create your own org and workspace via `POST /api/orgs` and `POST /api/orgs/{orgId}/workspaces`. The default tenant is a convenience for getting started, not a recommended permanent home for production data.

## Two auth schemes

PAL-X accepts two kinds of credentials:

| Sent header | Scheme | Used by |
|---|---|---|
| `Authorization: Bearer pal_<base64url>` | API key | CLI (`pal remote *`), automation, scripts |
| Browser cookie (set by `POST /account/login`) | ASP.NET Core Identity (cookie) | Blazor UI, browser users |

A single `CookieOrApiKey` policy scheme inspects the `Authorization` header and forwards to the right handler. The downstream authorisation pipeline is the same regardless of which scheme authenticated the caller — the user identity is what carries.

API keys are SHA-256-hashed at rest. The raw value is returned by `POST /tokens` exactly once and stored hashed thereafter. There is no recovery flow — lose the key, mint a new one. Tokens are user-bound (the user identity of the mintor), and the token's user identity is what's enforced when the call lands.

Tokens can expire (`expiresAt` set at mint time) or be manually revoked (`DELETE /tokens/{id}`).

## Three roles

Roles are layered:

| Role | Powers |
|---|---|
| `Viewer` | Read all data-plane resources in workspaces they're a member of. Cannot mutate. |
| `Analyst` | Viewer + alert mutations (ack, resolve, snooze). Designate baselines. Submit analyses. |
| `Admin` | Analyst + org / workspace / user / webhook / schedule management. Anything mutating identity or routing config. |

Role assignment is at the **org** level, not the workspace level. A user is an Admin of the org, not of a specific workspace — though their view is filtered by workspace on every request via the tenant filter.

There's no per-resource ACL today. The role + workspace tuple is the entire authorisation surface.

## What the default-fallback policy gives you

The authorisation builder sets a fallback policy of `RequireAuthenticatedUser`. So:

- Endpoints without an explicit `.RequireAuthorization(<role>)` accept any signed-in user.
- Endpoints with a role gate (`Roles.Analyst`, `Roles.Admin`) require that role.
- The handful of `.AllowAnonymous()` endpoints (login, logout, health) skip auth entirely.

This is why some HTTP API pages say "Auth: required, Role: any" — they're under the fallback policy.

## How CLI auth works under the hood

`pal remote *` sets `Authorization: Bearer <token>` on every request. The CLI stores the token via the `--api-key` flag or the `PAL_API_KEY` environment variable. There's no token cache file; the operator manages persistence.

The workspace ID for CLI calls comes from `--workspace` or the `PAL_WORKSPACE` environment variable. Together they fully describe "which API, which workspace, as which user."

## Use the HTTP API directly

If you're scripting against the API without going through the CLI, see **[Use the HTTP API](../guides/use-the-http-api.md)** for the end-to-end shape — token bootstrap, workspace selection, error handling.

## Related

- **[Use the HTTP API](../guides/use-the-http-api.md)** — end-to-end automation against the API.
- **[HTTP API — Index](../reference/http-api/index.md)** — auth / authz / routing details.
- **[HTTP API — Account](../reference/http-api/account.md)** / **[Tokens](../reference/http-api/tokens.md)** / **[Orgs](../reference/http-api/orgs-and-workspaces.md)** — endpoint contracts.
