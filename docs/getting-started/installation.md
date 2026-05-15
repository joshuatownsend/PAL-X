---
title: Installation
description: Prerequisites and from-source build for the PAL-X CLI and API.
---

# Installation

PAL-X is currently built from source. The repository contains one .NET 8 solution that produces the `Pal.Cli` console tool and the `Pal.Api` web service.

## Prerequisites

| Component | Required for | Where to get it |
|---|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) | Everything | Direct download |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Running the API (Postgres + worker) | Direct download |
| [Git](https://git-scm.com/) | Cloning the repository | Direct download |
| Windows x64 | Native BLG ingestion (`.blg` capture files) | n/a — Linux/macOS users convert BLG to CSV first with the Windows-side `relog -f CSV server.blg -o server.csv` |

If you only ever plan to use the local CLI against CSV captures, you can skip Docker.

## Get the source

```bash
git clone https://github.com/joshuatownsend/PAL-X.git
cd PAL-X
```

The `legacy/` submodule contains the original PAL v2 PowerShell tool as historical reference. It's optional and not needed for any PAL-X workflow; you can omit `--recurse-submodules`.

## Build the solution

From the repository root:

```bash
dotnet build dotnet/Pal.sln -c Release
```

This restores NuGet packages, compiles all projects, and is a one-time step. Re-run it whenever you pull new changes.

## Verify the CLI

```bash
dotnet run --project dotnet/src/Pal.Cli -c Release -- --help
```

You should see the Spectre.Console banner and a list of top-level commands (`analyze`, `validate-pack`, `inspect-dataset`, `list-packs`, `packs`, `remote`). If you see a build error here, repeat the `dotnet build` step above.

> **Why `dotnet run --project ... --` for everything?** PAL-X doesn't yet ship a self-contained `pal.exe`. Until it does, every CLI invocation in these docs is prefixed with `dotnet run --project dotnet/src/Pal.Cli -c Release --`. Packaging is on the roadmap.

## Optional — set up the API

If you also want the multi-user service, copy and edit the environment file before starting Docker:

```bash
cp .env.example .env
```

Open `.env` and set:

```text
POSTGRES_PASSWORD=<your-postgres-password>     # no semicolons; inlined into the Npgsql connection string
PAL_BOOTSTRAP_ADMIN_PASSWORD=<10+ characters>  # used once on first boot to seed admin@pal.local
```

Then bring everything up:

```bash
docker compose up
```

On first run, the API seeds an `admin@pal.local` account with the bootstrap password you set. Sign in at `http://localhost:8080/account/login`. The auto-generated OpenAPI docs are at `http://localhost:8080/swagger` (development mode).

> **Bootstrap is one-shot.** If `admin@pal.local` already exists, the seeder skips silently — changing `PAL_BOOTSTRAP_ADMIN_PASSWORD` later has no effect. Rotate via the `/account/users` admin UI or reset directly in the database.

## What's next

- Got a CSV (or already cloned with the bundled fixtures)? Head to **[First analysis — local CLI](first-analysis-local.md)**.
- Want to run a job against the hosted API instead? Head to **[First analysis — remote API](first-analysis-remote.md)**.

If you hit a port-5432 collision when running `docker compose up`, see the troubleshooting note in the repo `README.md` — it's a real-world gotcha if you have a native PostgreSQL service installed on Windows.
