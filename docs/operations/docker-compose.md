---
title: Docker Compose
description: The bundled docker-compose.yml — env-var overrides, healthchecks, volumes.
---

# Docker Compose

The repository ships a `docker-compose.yml` that brings up Postgres + API together. It's the fastest path from clone to a running API and the recommended starting point for small deployments.

For other install paths, see **[Installation](installation.md)**.

## Bring up the stack

```bash
git clone https://github.com/joshuatownsend/PAL-X.git
cd PAL-X

# Override the default dev password before exposing to anything
export POSTGRES_PASSWORD='a-real-postgres-password'
export PAL_BOOTSTRAP_ADMIN_PASSWORD='a-real-admin-password'

docker compose up -d
```

What's running:

- `postgres` — `postgres:16-alpine` with a `pg_isready` healthcheck.
- `api` — built from `infra/docker/api.Dockerfile`, depends on Postgres reaching healthy, with its own `/health` curl healthcheck.

Both are wired to named volumes (`postgres_data`, `api_data`) so a `docker compose down` doesn't destroy state. To wipe everything: `docker compose down -v`.

## Env-var overrides

The compose file reads these from the environment (or `.env` file in the same directory):

| Variable | Default | Notes |
|---|---|---|
| `POSTGRES_PASSWORD` | `paldev` | **Override before production exposure.** |
| `POSTGRES_PORT_HOST` | `5432` | Host-side port. Set to e.g. `5433` if you already run Postgres natively on 5432. |
| `PAL_BOOTSTRAP_ADMIN_PASSWORD` | *unset* | Required to seed the `admin@pal.local` account on first boot. **Don't leave unset in production.** |
| `RETENTION_JOB_DAYS` | `0` | Days before jobs (and their artifacts) are purged. `0` = keep forever. |
| `RETENTION_AUDIT_DAYS` | `0` | Days before audit events are purged. |

Example `.env` for development:

```text
POSTGRES_PASSWORD=local-dev-password
PAL_BOOTSTRAP_ADMIN_PASSWORD=local-dev-admin
POSTGRES_PORT_HOST=5433
```

Don't commit `.env`. The shipped `.gitignore` excludes it.

## Healthchecks

Both services have healthchecks; `docker compose ps` shows status.

- **Postgres**: `pg_isready -U pal -d pal` every 5s. The API container's `depends_on.condition: service_healthy` means the API doesn't start until Postgres reports healthy.
- **API**: `curl -sf http://localhost:8080/health` every 10s with a 20s start-up grace. After three failed pings the container is marked unhealthy.

Watch them:

```bash
docker compose ps
docker compose logs -f api
```

## Volumes

The compose file declares two named volumes:

| Volume | Mounted at | Holds |
|---|---|---|
| `postgres_data` | `/var/lib/postgresql/data` (postgres container) | Database files |
| `api_data` | `/data/storage` (api container) | Uploads, reports, datasets |

Both persist across `docker compose down` (without `-v`). Back them up explicitly — see **[Backup and restore](backup-and-restore.md)**.

## Port mapping

- Host `${POSTGRES_PORT_HOST:-5432}` → container `5432` (Postgres).
- Host `8080` → container `8080` (API).

To expose the API on a different host port:

```yaml
services:
  api:
    ports:
      - "9000:8080"
```

If port `8080` is taken on your host (often by another service or a previous container), this is the place to change it.

## Building vs pulling

The compose file builds the API from source via `infra/docker/api.Dockerfile`. To use a prebuilt image instead:

```yaml
services:
  api:
    build: null               # remove or comment out
    image: ghcr.io/your-org/pal-api:2026.2.0
```

There is no official published image today; the build-from-source flow is the canonical path.

## Common operational commands

```bash
# Restart just the API (after config changes)
docker compose restart api

# Tail logs
docker compose logs -f api postgres

# Run psql against the running database
docker compose exec postgres psql -U pal pal

# Run a one-off command in the API container
docker compose exec api ls /app/packs/thresholds

# Bring everything down (keeps volumes)
docker compose down

# Bring everything down AND delete volumes (wipes state)
docker compose down -v
```

## Going beyond compose

The compose stack is appropriate for:

- Development.
- Single-host production deployments where availability isn't critical.
- Demos and trials.

For production with redundancy, run Postgres separately (managed RDS / Cloud SQL / etc.), and the API as multiple replicas behind a load balancer. The shipped image works as-is in that role; you just don't use this compose file. See **[Backup and restore](backup-and-restore.md)** for the durability stance.

## Related

- **[Installation](installation.md)** — alternative install paths.
- **[Postgres setup](postgres-setup.md)** — when running Postgres elsewhere.
- **[Configuration](../reference/configuration.md)** — every env-var the API reads.
- **[Monitoring](monitoring.md)** — what to watch beyond compose's healthchecks.
