---
title: Authentication and tokens
description: Bootstrap the admin account, mint API keys, rotate them, and configure the dual cookie / API-key scheme operationally.
---

# Authentication and tokens

PAL-X supports two auth schemes that share the same authorisation pipeline — see the **[Concepts — Multitenancy and auth](../concepts/multitenancy-and-auth.md)** page for the model. This page is the operational view: bootstrap the admin on a fresh install, mint API keys for automation, and rotate them.

## Bootstrap admin

On first startup, the `IdentitySeeder` looks for the env var `PAL_BOOTSTRAP_ADMIN_PASSWORD` (or `Auth:BootstrapAdminPassword` in config). If present **and** the admin account doesn't yet exist, the seeder creates:

- A user `admin@pal.local` with that password.
- That user is granted the `Admin` role.
- That user is added as an `admin` member of the default org (`DefaultTenant.OrgId`).

If the env var is unset, no admin is seeded — the API runs but you can't log in until you've provisioned a user some other way (rare; typically you set the env var).

If the admin user already exists, the seeder is idempotent and does nothing. **This means rotating the bootstrap password through env var alone won't work** after first boot — see [Rotate the admin password](#rotate-the-admin-password) below.

### Pick a strong password

The Identity policy requires:

- Minimum length 10.
- No non-alphanumeric requirement (deliberately permissive — strength comes from length and randomness).
- Lockout after 10 failed attempts (15-minute lockout).

For production, generate randomly:

```bash
openssl rand -base64 24
```

Or in PowerShell:

```powershell
[Convert]::ToBase64String((1..24 | %{ Get-Random -Maximum 256 }))
```

Set the value as the bootstrap env var **before** first startup. After first startup, the password lives hashed in `asp_net_users.password_hash` and the env var is no longer consulted.

## First login flow

```bash
# 1. Log in via the cookie scheme
curl -X POST http://localhost:8080/account/login \
  -d "email=admin@pal.local&password=<your-bootstrap-password>" \
  -c cookies.txt -L

# 2. Confirm via /account/me
curl -b cookies.txt http://localhost:8080/account/me
# {"id":"…","email":"admin@pal.local","roles":["Admin"]}
```

Or via the browser at `http://localhost:8080/account/login`.

## Mint an API key

API keys are the right credential for non-browser callers — CLI, automation, scripts. They're workspace-scoped under `/api/workspaces/{workspaceId}/tokens` and tied to the user who minted them.

The first time, you need to authenticate via the cookie (you've just logged in) or via Basic auth:

```bash
WS=00000000-0000-0000-0000-000000000002   # default workspace

# Via cookie (after browser/curl login)
curl -X POST http://localhost:8080/api/workspaces/$WS/tokens \
  -b cookies.txt \
  -H "Content-Type: application/json" \
  -d '{"name":"automation"}'

# Via Basic auth (no cookie needed)
curl -X POST http://localhost:8080/api/workspaces/$WS/tokens \
  -u admin@pal.local:<your-bootstrap-password> \
  -H "Content-Type: application/json" \
  -d '{"name":"automation"}'
```

Response:

```json
{
  "id": "…",
  "name": "automation",
  "createdAt": "2026-05-15T10:00:00Z",
  "token": "pal_AbCdEf0123…"
}
```

**`token` is returned exactly once.** Capture it. The stored representation is `SHA-256(token)` — there is no recovery flow. Lost a token? Mint a new one and delete the old.

## Use the API key

```bash
TOKEN=pal_AbCdEf0123…

curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:8080/api/workspaces/$WS/analysis
```

The CLI auto-picks-up `PAL_API_KEY`:

```bash
export PAL_API_KEY=pal_AbCdEf0123…
export PAL_WORKSPACE=$WS

pal remote status
```

## Expiry and rotation

Tokens carry an optional `expiresAt`. Set it at mint time:

```json
{ "name": "ci-runner", "expiresAt": "2027-01-01T00:00:00Z" }
```

After `expiresAt`, the API rejects the token with `401 Unauthorized`. Renew by minting a new one.

To revoke a token before its expiry:

```bash
curl -X DELETE http://localhost:8080/api/workspaces/$WS/tokens/<token-id> \
  -H "Authorization: Bearer $ANOTHER_VALID_TOKEN"
```

Or via the Blazor UI's tokens management page.

### Rotation procedure

Zero-downtime rotation of an API key in automation:

1. Mint a new token.
2. Update the consumer's configuration to use the new token.
3. Wait one rotation cycle (a CI run, a deploy) to confirm the new token works.
4. `DELETE` the old token.

The two tokens coexist during the swap window. There's no built-in two-secret accept — you simply mint, switch, then revoke.

## Rotate the admin password

Because the bootstrap env var is consulted only on first boot, rotating the admin password requires either:

### Option A — In-app (preferred)

If the Blazor UI exposes a password change form for the current user, use it. Otherwise, hit the underlying Identity endpoint with the user's existing credentials.

### Option B — Direct via the database

Open psql and update the `asp_net_users.password_hash` column with a freshly-hashed password. The ASP.NET Core Identity hasher format is non-trivial — use a small script that constructs an `ApplicationUser` and calls `userManager.PasswordHasher.HashPassword(user, newPassword)`. Don't store an unhashed password.

### Option C — Wipe and re-seed (loses cookies/tokens)

1. `DELETE FROM asp_net_users WHERE email = 'admin@pal.local';`
2. Restart the API with the new `PAL_BOOTSTRAP_ADMIN_PASSWORD` env var set.
3. All previously-issued tokens for that user are invalidated; mint new ones.

Option C is destructive — only do it if you can re-mint all dependent tokens.

## Create more users

```bash
curl -X POST http://localhost:8080/account/users \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"email":"analyst@example.com","password":"strong-password","role":"Analyst","displayName":"Jane"}'
```

The same Identity password rules apply. The `role` defaults to `Viewer` if you pass an unrecognised value.

To add the new user to the org as a member with a specific role, use `PUT /api/orgs/{orgId}/members/{userId}` — see **[Orgs and workspaces setup](orgs-and-workspaces-setup.md)**.

## Disable bootstrap after first install

Once `admin@pal.local` exists, the bootstrap env var is no longer load-bearing. You can unset it from the environment for ongoing operation — leaving it set is harmless (it's only consulted if no admin user is found), but it's cleaner to remove the secret from your deployment manifests once it's done its job.

## Related

- **[Concepts — Multitenancy and auth](../concepts/multitenancy-and-auth.md)** — the model.
- **[HTTP API — Account](../reference/http-api/account.md)** / **[Tokens](../reference/http-api/tokens.md)** — endpoint contracts.
- **[Orgs and workspaces setup](orgs-and-workspaces-setup.md)** — production tenancy.
- **[Use the HTTP API](../guides/use-the-http-api.md)** — applying these credentials in automation.
