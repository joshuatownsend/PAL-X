---
title: Orgs and workspaces setup
description: Provision orgs, workspaces, and user memberships for a production multi-tenant deployment.
---

# Orgs and workspaces setup

The seeded default org and workspace are enough for getting started and for single-team deployments. Once you have multiple teams or customers sharing the API, you need to provision separate orgs and workspaces so data is properly isolated.

For the model, see **[Concepts — Multitenancy and auth](../concepts/multitenancy-and-auth.md)**.

## The default tenant

On first boot, the `IdentitySeeder` creates:

```text
DefaultTenant.OrgId       = 00000000-0000-0000-0000-000000000001
DefaultTenant.WorkspaceId = 00000000-0000-0000-0000-000000000002
```

The bootstrap admin (`admin@pal.local`) is an `admin` member of the default org. Until you create more orgs, every API call uses these IDs in the URL prefix.

For most production deployments, **leave the default tenant in place** and use it for the operating team's own captures. Create additional orgs and workspaces for customer-segment isolation, environments (prod/stage/dev), or third-party packs you don't want commingled with first-party data.

## Create an org

`Admin` role required (the global Admin role, not org-level Admin — there's no separate concept).

```bash
curl -X POST http://localhost:8080/api/orgs \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"Customer Acme","slug":"customer-acme"}'
```

Response (HTTP 201):

```json
{ "id": "f3a1...", "name": "Customer Acme", "slug": "customer-acme", "createdAt": "…" }
```

- `slug` must be unique across all orgs.
- `name` is free-form.

## Create a workspace within the org

```bash
ORG=f3a1...
curl -X POST http://localhost:8080/api/orgs/$ORG/workspaces \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"Production","slug":"prod"}'
```

Response:

```json
{ "id": "9b21…", "name": "Production", "slug": "prod", "orgId": "f3a1…", "createdAt": "…" }
```

- `slug` must be unique within the org (not globally).

Workspaces are the unit of data isolation. A user with access to one workspace can't see data in another, even within the same org.

## Add a user to the org

Users are global (one user can be a member of multiple orgs). Assignment happens at the **org** level — role and workspace visibility flow from that.

```bash
USER_ID=$(curl -s -H "Authorization: Bearer $ADMIN_TOKEN" \
  http://localhost:8080/account/users \
  | jq -r '.[] | select(.email == "analyst@customer-acme.com") | .id')

curl -X PUT http://localhost:8080/api/orgs/$ORG/members/$USER_ID \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"role":"Analyst"}'
```

Returns `204 No Content`. The user can now operate within any workspace owned by the org, with `Analyst` permissions.

## Recommended layouts

### Single team, multiple environments

| Org | Workspaces |
|---|---|
| Default | `prod`, `stage`, `dev` |

The default org is fine; you add more workspaces under it. Analysts who should only see one environment can be — well, today they can't, because role assignment is at the org level. A future per-workspace role is the planned improvement.

For now, isolate by org if you really need per-environment access control.

### Multi-customer SaaS

| Org | Workspaces |
|---|---|
| Default | (operator's own captures) |
| Customer A | `prod`, `stage` |
| Customer B | `prod` |
| Customer C | `prod`, `dev`, `qa` |

Each customer is one org. The operator's team has the `Admin` role on the default org. Customer users are members of their own org only. Storage is shared at the disk level (one `Storage:LocalRoot`) but logically separated by `workspace_id` everywhere it matters.

### Self-hosted with strict isolation

| Org | Workspaces |
|---|---|
| Default | (single workspace) |

Single tenant; the multi-tenancy is along for the ride but doesn't impose burden. Skip creating extra orgs.

## What changes when you create a new workspace

- All workspace-scoped routes (`/api/workspaces/{newId}/...`) become reachable for org members.
- A new directory under `Storage:LocalRoot/uploads/{newId}/` gets created lazily on first upload.
- Repositories' global query filter scopes every query by `workspace_id` automatically.
- The `TenantResolutionEndpointFilter` validates membership before any handler runs.

You don't need to provision Postgres rows manually — the workspace exists from the moment `POST /api/orgs/{orgId}/workspaces` returns.

## Removing a member

```bash
curl -X DELETE http://localhost:8080/api/orgs/$ORG/members/$USER_ID \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

`204` on success. The user's data is unaffected; only their membership is gone. They'll get `403 Forbidden` on subsequent calls into that org's workspaces.

## Permissions matrix

| Action | Required role |
|---|---|
| List orgs / create orgs / create workspaces / manage members | `Admin` (global) |
| Submit, list, read analyses; read reports/datasets | Any authenticated user with org membership |
| Ack/resolve/snooze alerts | `Analyst` or higher |
| Designate baselines | `Analyst` or higher |
| Create/update webhooks, schedules | `Admin` (global) |

Today there's no per-workspace ACL — once a user is a member of the org, they see all of the org's workspaces with the role they were granted.

## Related

- **[Concepts — Multitenancy and auth](../concepts/multitenancy-and-auth.md)** — model.
- **[HTTP API — Orgs and workspaces](../reference/http-api/orgs-and-workspaces.md)** — endpoint shapes.
- **[Auth and tokens](auth-and-tokens.md)** — minting credentials for org members.
