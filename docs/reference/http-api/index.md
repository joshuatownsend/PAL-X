---
title: HTTP API reference
description: Hand-written reference for every endpoint the PAL-X API exposes, grouped by feature.
---

# HTTP API reference

The PAL-X API is an ASP.NET Core minimal API. Every endpoint is documented on a feature-area page below, with request and response shapes, status codes, an example `curl`, and the matching CLI command on `pal remote`.

If you'd rather drive the API from the command line, see **[pal remote](../cli/pal-remote.md)** — every API verb has a one-to-one CLI surface.

## Base URL

```text
http://localhost:5043     # default dev port
https://your-host          # production
```

The API binds to whatever Kestrel is configured for; the [installation guide](../../getting-started/installation.md) walks through the defaults.

## Authentication

Two schemes share the same authorization filter, chosen by the `Authorization` header:

| Sent header | Scheme | Use |
|---|---|---|
| `Authorization: Bearer pal_<base64url>` | API key | CLI, automation, `pal remote *`. Mint via [`POST /tokens`](tokens.md). |
| Browser cookie (set by `POST /account/login`) | ASP.NET Core Identity | Blazor UI, browser navigation. |

API keys are SHA-256-hashed at rest. The raw value is returned exactly once at creation time — there is no recovery flow. See the [Tokens](tokens.md) page.

A request without either is rejected with `401 Unauthorized` unless the endpoint is marked `AllowAnonymous` (only `/account/login`, `/account/logout`, and `/health`).

## Authorization

Three roles, layered:

| Role | Powers |
|---|---|
| `Viewer` | Read-only: list and read jobs, reports, baselines, alerts, etc. |
| `Analyst` | Viewer + acknowledge/resolve/snooze alerts; designate baselines. |
| `Admin` | Analyst + manage orgs, workspaces, users, webhooks, schedules. |

Each endpoint page calls out the required role. The default fallback policy is "authenticated user," so any endpoint without an explicit role gate accepts any signed-in caller.

## Workspace routing

Data-plane endpoints are scoped to a workspace and live under the prefix:

```text
/api/workspaces/{workspaceId:guid}/...
```

The `TenantResolutionEndpointFilter` validates that the calling user belongs to the org owning the workspace before any handler runs. A bad workspace id, a wrong-tenant id, or a missing membership all return `403 Forbidden` before reaching the handler.

**Global endpoints** (no workspace prefix): `/health`, `/account/*`, `/api/orgs/*`, `/packs/*`.

## Default tenant

On a fresh install, the `IdentitySeeder` creates a default org and workspace so the API is usable out of the box:

```text
DefaultTenant.OrgId       = 00000000-0000-0000-0000-000000000001
DefaultTenant.WorkspaceId = 00000000-0000-0000-0000-000000000002
```

The seeded admin is configured via `appsettings.json` and rotated through `POST /account/users`. See [Getting started — remote](../../getting-started/first-analysis-remote.md) for the bootstrap flow.

## Content types

- **Request bodies** are `application/json` unless explicitly `multipart/form-data` (only `/uploads`).
- **Response bodies** are `application/json; charset=utf-8` unless they're reports — see [Reports](reports.md) for `text/html`, `text/markdown`, and the JSON variant.
- **Streamed downloads** (datasets, reports) use the appropriate media type; the response includes a `Content-Disposition` filename.

## Error model

Errors use [RFC 7807 problem details](https://datatracker.ietf.org/doc/html/rfc7807) when a handler emits `Results.Problem(...)`. Most validation failures use the minimal-API helper `Results.BadRequest(new { error = "…" })` and return:

```json
{ "error": "human-readable message" }
```

Common status codes used across the surface:

| Code | Meaning |
|---|---|
| `200 OK` | Success with a body. |
| `201 Created` | New resource; `Location` header points to its canonical URI. |
| `202 Accepted` | Async work queued (e.g., analysis jobs). |
| `204 No Content` | Success without a body (mutations, deletes, snoozes). |
| `400 Bad Request` | Validation failure. |
| `401 Unauthorized` | No or bad credentials. |
| `403 Forbidden` | Authenticated but not authorised — wrong role or wrong workspace. |
| `404 Not Found` | Resource doesn't exist or wasn't visible to this tenant. |
| `409 Conflict` | State precondition failed (job not completed, alert already resolved, slug taken). |
| `422 Unprocessable Entity` | Semantic validation failure (e.g., pack couldn't be loaded for validation). |
| `502 Bad Gateway` | Outbound call failed (e.g., webhook test delivery). |
| `500 Internal Server Error` | Unhandled exception. Always logged. |

## Pagination

There is no pagination today. List endpoints return `{ "items": [...] }` and are intended for direct consumption. If you have a workload that requires pagination, file an issue.

## Pages

### Global

- **[Health](health.md)** — readiness probe.
- **[Account](account.md)** — login/logout, current user, user management.
- **[Orgs and workspaces](orgs-and-workspaces.md)** — multi-tenant setup, membership, workspace creation.
- **[Packs](packs.md)** — list packs and their versions; validate a stored pack.

### Workspace-scoped

- **[Tokens](tokens.md)** — mint and revoke API keys.
- **[Uploads](uploads.md)** — submit a CSV or BLG file.
- **[Analysis jobs](analysis-jobs.md)** — submit, list, status, results, diagnostics.
- **[Reports](reports.md)** — JSON / HTML / Markdown report retrieval.
- **[Datasets](datasets.md)** — download the gzipped dataset snapshot.
- **[Baselines](baselines.md)** — designate, list, version.
- **[Compare](compare.md)** — diff two jobs.
- **[Trends](trends.md)** — multi-job trend evaluation.
- **[Correlations](correlations.md)** — cross-signal correlations.
- **[Alerts](alerts.md)** — acknowledge / resolve / snooze.
- **[Webhooks](webhooks.md)** — notification sinks and test delivery.
- **[Schedules](schedules.md)** — recurring ingestion.

## OpenAPI / Swagger

In `Development` only, the API exposes Swagger UI at `/swagger` and the OpenAPI document at `/swagger/v1/swagger.json`. Production builds disable it by default; if you want it on in production, edit `Pal.Api/Program.cs` to move `app.UseSwagger()` / `app.UseSwaggerUI()` outside the `IsDevelopment()` guard.
