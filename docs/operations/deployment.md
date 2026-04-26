# Deployment

PAL ships as a single Docker image (`pal-api`) backed by a Postgres database.  The image bundles the Blazor Server UI, REST API, background analysis worker, and the built-in threshold packs.

## Quick start (Docker Compose)

```bash
# 1. Copy the env template
cp .env.example .env

# 2. Edit .env — set POSTGRES_PASSWORD and PAL_BOOTSTRAP_ADMIN_PASSWORD
#    Password must be at least 10 characters.

# 3. Start
docker compose up -d

# 4. Open the UI
open http://localhost:8080
#    Log in with admin@pal.local and the password you set above.
```

The API is available at `http://localhost:8080`.  Swagger UI is served at `http://localhost:8080/swagger` when `ASPNETCORE_ENVIRONMENT` is not `Production`.

## Environment variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `ConnectionStrings__Postgres` | yes | — | Full Npgsql connection string |
| `PAL_BOOTSTRAP_ADMIN_PASSWORD` | first run only | — | Password for the `admin@pal.local` seed account. Ignored after first run. |
| `POSTGRES_PASSWORD` | yes (compose only) | `paldev` | Postgres container password; also interpolated into the API connection string by compose |
| `Storage__LocalRoot` | no | `data/storage` | Where uploaded files are stored. Mount a volume here. |
| `Packs__Directory` | no | `packs/thresholds` | Directory of threshold pack YAML files. Bundled packs are at `/app/packs/thresholds` inside the image. |
| `ASPNETCORE_ENVIRONMENT` | no | `Production` | Set to `Development` to enable Swagger UI and verbose EF logging. |

## Data persistence

Two named volumes keep data across restarts:

| Volume | Contents |
|---|---|
| `postgres_data` | All database state (users, jobs, results, alerts) |
| `api_data` | Uploaded `.csv` / `.blg` files |

To back up: `pg_dump` the database and archive the `api_data` volume mount.

## First-run admin bootstrap

On startup, `Program.cs` calls `IdentitySeeder.SeedAsync`, which:

1. Creates the `Admin`, `Analyst`, and `Viewer` roles if they don't exist.
2. If `PAL_BOOTSTRAP_ADMIN_PASSWORD` is set **and** `admin@pal.local` does not exist, creates that account with the Admin role.

After first run you can unset `PAL_BOOTSTRAP_ADMIN_PASSWORD` from your `.env` (or leave it — subsequent starts skip creation because the account already exists).

Additional users are created via **Settings → User management** in the UI (Admin only), or via `POST /account/users`.

## Building the image manually

```bash
# From repo root
docker build -f infra/docker/api.Dockerfile -t pal-api:latest .
```

The Dockerfile uses a two-stage build: SDK image for compile + publish, then the smaller `aspnet:8.0` runtime image.  Pack YAML files are copied from `packs/thresholds/` into `/app/packs/thresholds` at build time.

## Health check

```
GET /health  →  200 {"status":"ok","version":"..."}
```

Docker Compose polls this every 10 s.  The endpoint is anonymous and does not require authentication.

## Updating

```bash
docker compose pull   # if using a registry image
docker compose build  # if building locally
docker compose up -d
```

EF migrations run automatically on startup via `db.Database.MigrateAsync()` in `Program.cs`.

## Production hardening checklist

- [ ] Use a managed Postgres instance (RDS, Cloud SQL, Azure Database) instead of the compose container.
- [ ] Mount `api_data` to durable block storage, not a local Docker volume.
- [ ] Put a TLS-terminating reverse proxy (nginx, Caddy, Traefik) in front of port 8080.
- [ ] Rotate `POSTGRES_PASSWORD` and `PAL_BOOTSTRAP_ADMIN_PASSWORD` using your secrets manager; never commit `.env` to git.
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production` (already set by compose) to suppress Swagger UI.
- [ ] Configure log shipping from the container's stdout to your observability stack.
