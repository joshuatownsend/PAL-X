---
title: Pack distribution
description: Where packs live on the server, how PackRegistrySyncService loads them, and how to manage signed packs in production.
---

# Pack distribution

Packs are the rules PAL-X evaluates. There are two places a pack can live in a deployed API:

1. **On disk** under `Packs:Directory` — loaded at startup by `PackRegistrySyncService` and refreshed every time the API restarts.
2. **In Postgres** — uploaded through the API or pushed by `PackRegistrySyncService` for managed access via `/packs/*`.

For authoring, see **[Write a pack](../guides/write-a-pack.md)** *(in concepts/guides)*. For the directory setting, see **[Configuration — Packs](../reference/configuration.md#packs)**.

## What loads at startup

On every API startup, `PackRegistrySyncService` walks `Packs:Directory` looking for subdirectories containing `pack.yaml`. Each one becomes a pack entry in the database — with the YAML stored verbatim and versioned by the pack's `version` field. If a pack with the same `(pack_id, version)` already exists in the database, it's updated; if not, it's inserted.

This is the **canonical path** for putting packs into production: drop them on disk under the configured directory and restart. No API calls needed.

The shipped Docker image bundles `windows-core`, `iis-core`, and `sql-host-core` at `/app/packs/thresholds` for this reason — they're always available out of the box.

## Layout under `Packs:Directory`

```text
${Packs:Directory}/
├── windows-core/
│   ├── pack.yaml
│   └── pack.yaml.sig           # optional
├── iis-core/
│   └── pack.yaml
├── sql-host-core/
│   └── pack.yaml
└── customer-internal/
    ├── pack.yaml
    └── pack.yaml.sig
```

Each pack is one directory containing one `pack.yaml`. Sub-directories within the pack are ignored.

Set the directory:

| Where | How |
|---|---|
| `appsettings.json` | `"Packs": { "Directory": "packs/thresholds" }` |
| Env var | `Packs__Directory=/etc/pal/packs` |
| Docker image | `/app/packs/thresholds` (set in the Dockerfile) |
| Docker compose | `/app/packs/thresholds` (inherited from the image) |

If the directory doesn't exist at startup, the sync skips silently — the API still runs, just with no packs loaded.

## Adding a pack in production

The recommended workflow:

1. Author and sign the pack on a development machine.
2. Run `pal validate-pack --path packs/customer-internal --strict --require-signature --trust-key team.pub.pem` to confirm.
3. Copy the pack directory to the production host's `Packs:Directory`.
4. Restart the API. `PackRegistrySyncService` picks it up and registers a new pack version.

For zero-downtime swaps, deploy the new pack version with the same `pack_id` and a bumped `version`. The API supports multiple versions of the same `pack_id` side-by-side; rules pin via `pack_id@version` syntax or float on the latest.

## Updating an existing pack version

If you replace `pack.yaml` for `(pack_id, version)` that already exists in the database, `PackRegistrySyncService` overwrites the stored content on the next startup. **This is convenient for development but lossy for production** — once you've shipped a pack version, treat it as immutable and bump the version for any change.

If you want to enforce immutability operationally, add a CI check that fails if a pack's `(pack_id, version)` exists in production but the `pack.yaml` hash differs.

## Trust setup for signed packs

Signing is documented at **[Sign and trust packs](../guides/sign-and-trust-packs.md)** (in guides). The operational view:

- **For local development**: trust comes from `--trust-key <file>` flags passed at CLI invocation. The API itself currently doesn't enforce signatures at load time — the trust check happens at validation time, not load time.
- **For production**: enforce signatures via your CI's `pal validate-pack --require-signature --trust-key` step **before** the pack reaches `Packs:Directory`. The API loads whatever's on disk.

The split is intentional: the API is permissive (it loads what you give it); CI is strict (it rejects unsigned/wrong-keyed packs). If you want the API to enforce signatures at runtime too, that's a feature gap to file.

## Pack distribution across replicas

If you run multiple API replicas behind a load balancer, you have two choices for keeping packs in sync:

### Option A — Shared filesystem

Mount the same directory (NFS, EFS, Azure Files, etc.) as `Packs:Directory` on every replica. Drop a pack once; every replica picks it up on its next startup. Easy, but requires shared storage that all replicas can read.

### Option B — Per-replica deployment

Bake packs into the image (the shipped Dockerfile pattern) or push them with the deploy. Every replica has its own copy. Simpler for stateless deployments; requires re-deploying when you want to change packs.

### Option C — Database-only (no disk)

Don't put packs on disk at all. Upload via `POST /api/packs` (today this is exposed in the Blazor UI; there's no direct HTTP API for it). Once in the database, every replica sees them via the existing repository. Operationally rarer.

## Listing what's loaded

After startup, query the registry:

```bash
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/packs

# {"items":[
#   {"packId":"windows-core","name":"Windows Core","latestVersion":"1.0.0"},
#   …
# ]}
```

Or with the CLI:

```bash
pal remote packs list
```

If a pack you expected isn't there, check the API logs at startup — `PackRegistrySyncService` logs every load attempt and any errors.

## Validating stored packs

```bash
pal remote validate-pack windows-core 1.0.0
```

This hits **[`GET /packs/{id}/versions/{version}/validation`](../reference/http-api/packs.md#get-packsidversionsversionvalidation)** which runs `PackValidator` against the stored YAML. Useful sanity check before relying on a pack in a critical workflow.

## Related

- **[Configuration — Packs](../reference/configuration.md#packs)** — the `Directory` setting.
- **[HTTP API — Packs](../reference/http-api/packs.md)** — list and validate endpoints.
- **[Write a pack](../guides/write-a-pack.md)** / **[Sign and trust packs](../guides/sign-and-trust-packs.md)** — authoring + signing.
- **[Storage layout](storage-layout.md)** — context on where things live on disk.
