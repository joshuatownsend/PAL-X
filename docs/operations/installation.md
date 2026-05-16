---
title: Installation
description: Production install paths — from source publish, from the bundled Dockerfile, or via the docker-compose stack.
---

# Installation

Three install paths:

- **From source** — publish a self-contained binary and run it as a service. Most control, most setup.
- **From the bundled Dockerfile** — `infra/docker/api.Dockerfile` produces a single image with the API + bundled packs.
- **Docker Compose** — `docker-compose.yml` brings up the API + Postgres together with healthchecks. See **[Docker Compose](docker-compose.md)** for that path specifically.

The CLI (`Pal.Cli`) is a separate concern — it doesn't need any of this. Skip directly to **[Getting Started — installation](../getting-started/installation.md)** if you only need local analysis.

## Prerequisites

- **.NET 8 SDK** (for source builds) or **.NET 8 ASP.NET runtime** (for prebuilt binaries).
- **PostgreSQL 14 or newer** — schema and migrations are tested against 16. See **[Postgres setup](postgres-setup.md)**.
- **64-bit Linux, Windows, or macOS** — Linux is the production target; Windows is supported for the CLI + API; macOS works for development.

The API does not embed Postgres. You always provision your own database.

## Install from source

```bash
git clone https://github.com/joshuatownsend/PAL-X.git
cd PAL-X

# Build everything
dotnet build dotnet/Pal.sln -c Release

# Publish the API as a runtime-dependent binary (small — needs .NET runtime on host)
dotnet publish dotnet/src/Pal.Api -c Release -o /opt/pal-api --no-restore
```

For a self-contained publish (no .NET runtime needed on the host):

```bash
dotnet publish dotnet/src/Pal.Api \
  -c Release \
  -o /opt/pal-api \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true
```

The output directory contains `pal-api` (or `pal-api.exe` on Windows). Plus `appsettings.json` / `appsettings.Production.json` overlays.

Run it:

```bash
cd /opt/pal-api
ASPNETCORE_ENVIRONMENT=Production \
  ConnectionStrings__Postgres="Host=db.internal;Database=pal;Username=pal;Password=…" \
  ./pal-api
```

Or as a systemd unit — see **[systemd example](#systemd-example)** below.

## Install from the Dockerfile

The bundled `infra/docker/api.Dockerfile` is a multi-stage build that produces a self-contained image with the API and the three shipped packs (`windows-core`, `iis-core`, `sql-host-core`) pre-loaded at `/app/packs/thresholds`.

```bash
docker build -f infra/docker/api.Dockerfile -t pal-api:local .

docker run -d --name pal-api \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__Postgres="Host=db.internal;Database=pal;Username=pal;Password=…" \
  -e PAL_BOOTSTRAP_ADMIN_PASSWORD="initial-admin-password" \
  -p 8080:8080 \
  -v pal-data:/data/storage \
  pal-api:local
```

The image:

- Exposes port `8080`.
- Mounts `/data/storage` as a volume — needed for upload, report, and dataset persistence across restarts.
- Bundles `curl` for the compose healthcheck.
- Built FROM `mcr.microsoft.com/dotnet/aspnet:8.0` (Debian slim).

## Install via docker-compose

The bundled `docker-compose.yml` brings up Postgres + API together. See **[Docker Compose](docker-compose.md)** for the full walkthrough including env-var overrides and healthcheck integration.

## File layout after install

| Path | Purpose |
|---|---|
| `/opt/pal-api/pal-api` (or `pal-api.exe`) | The binary. |
| `/opt/pal-api/appsettings.json` | Committed defaults. |
| `/opt/pal-api/appsettings.Production.json` | Production overrides. |
| `/opt/pal-api/packs/thresholds/` | Bundled packs (if you copied them). |
| `/var/lib/pal/storage/` *(your choice)* | `Storage:LocalRoot` — uploads, reports, datasets. |

In a Docker image these are at `/app` and `/data/storage`.

## systemd example

`/etc/systemd/system/pal-api.service`:

```ini
[Unit]
Description=PAL-X API
After=network.target postgresql.service

[Service]
Type=simple
User=pal
Group=pal
WorkingDirectory=/opt/pal-api
ExecStart=/opt/pal-api/pal-api
Restart=on-failure
RestartSec=10

Environment=ASPNETCORE_ENVIRONMENT=Production
EnvironmentFile=/etc/pal-api.env

[Install]
WantedBy=multi-user.target
```

`/etc/pal-api.env` (chmod 600, owned by `pal`):

```text
ConnectionStrings__Postgres=Host=db.internal;Database=pal;Username=pal;Password=…
PAL_BOOTSTRAP_ADMIN_PASSWORD=initial-admin-password
Storage__LocalRoot=/var/lib/pal/storage
Packs__Directory=/etc/pal/packs
Retention__JobRetentionDays=90
Retention__AuditEventRetentionDays=365
```

Enable + start:

```bash
sudo useradd --system --home /var/lib/pal --shell /usr/sbin/nologin pal
sudo mkdir -p /var/lib/pal/storage /etc/pal/packs
sudo chown -R pal:pal /var/lib/pal /etc/pal
sudo systemctl enable --now pal-api
sudo journalctl -u pal-api -f
```

## Verifying the install

```bash
curl http://localhost:8080/health
# {"status":"ok","version":"2026.2.0"}
```

If `/health` returns `200` but `/api/workspaces/…` returns `401`, the API is running and rejecting unauthenticated calls — exactly as it should. Move on to **[Auth and tokens](auth-and-tokens.md)** to mint your first token.

## Related

- **[Docker Compose](docker-compose.md)** — the easiest path.
- **[Postgres setup](postgres-setup.md)** — what to point `ConnectionStrings:Postgres` at.
- **[Configuration](../reference/configuration.md)** — every setting the API reads.
- **[Auth and tokens](auth-and-tokens.md)** — next step after install.
