---
title: Development setup
description: Prerequisites, clone, build, and verify — the first 15 minutes of contributing.
---

# Development setup

Goal: a clean clone running the full unit-test suite in under 15 minutes. This page assumes you've installed the prerequisites; everything else is "follow the commands."

For the user-facing install paths, see **[Operations — Installation](../operations/installation.md)**. This page is contributor-focused — you need the SDK and the test toolchain, not just the runtime.

## Prerequisites

| Tool | Purpose | How to verify |
|---|---|---|
| **.NET 8 SDK** | Build, test, EF tools | `dotnet --version` → `8.0.x` |
| **Git** | Clone, branch, commit | `git --version` |
| **PostgreSQL 14+ client** *(optional)* | psql against the dev DB | `psql --version` |
| **Docker Desktop** *(integration tests only)* | Testcontainers for Pal.Api.Tests | `docker info` (must succeed) |

You can run the full unit-test suite (146 tests today) without Docker. Integration tests live in `Pal.Api.Tests` and spin up a Postgres container — those need Docker.

## Clone

```bash
git clone https://github.com/joshuatownsend/PAL-X.git
cd PAL-X

# The legacy/ submodule is read-only reference; pull it for context only
git submodule update --init --recursive
```

## Build

```bash
dotnet build dotnet/Pal.sln -c Release
```

First-run restore + build is around 30 seconds. Subsequent incremental builds are sub-5s.

If you hit a NuGet restore issue (corporate proxy, etc.), try with explicit feed:

```bash
dotnet restore dotnet/Pal.sln --source https://api.nuget.org/v3/index.json
```

## Run unit tests (no Docker)

```bash
dotnet test dotnet/Pal.sln -c Release --filter "FullyQualifiedName!~Pal.Api.Tests"
```

Expected: 146 passed, 0 failed, 1 skipped (the skip is BLG-on-non-Windows in `Pal.Ingestion.Tests` and is expected on Linux/macOS).

The `--filter` excludes the integration tests; without it, those try to start a Postgres container and fail without Docker.

## Run integration tests (requires Docker Desktop)

```bash
# Make sure Docker Desktop is running first
dotnet test dotnet/tests/Pal.Api.Tests -c Release
```

The integration tests use Testcontainers to spin up an ephemeral Postgres per fixture. First run pulls the `postgres:16-alpine` image (~80MB); subsequent runs use the cached image. Total runtime is around 30-60 seconds.

Excluded from the Windows CI runner (no Docker on the GitHub-hosted Windows VM); these run on the Linux CI job or your local machine.

## Run the API locally

```bash
# 1. Start Postgres via the bundled compose
docker compose up -d postgres

# 2. Run the API
dotnet run --project dotnet/src/Pal.Api
```

The API listens on `http://localhost:5043` by default in `Development` mode. Migrations run automatically on first startup; the database (`pal` on `localhost:5432`) gets created if it doesn't exist (it does, thanks to the compose file).

Set `PAL_BOOTSTRAP_ADMIN_PASSWORD` before first startup to seed the admin account — see **[Operations — Auth and tokens](../operations/auth-and-tokens.md)**.

For Swagger UI: `Development` enables it automatically at `http://localhost:5043/swagger`.

## Run the CLI locally

The CLI is `Pal.Cli`. From the repo root:

```bash
dotnet run --project dotnet/src/Pal.Cli -- analyze \
  --input fixtures/cpu-pressure/input.csv \
  --output /tmp/out \
  --pack-dir packs/thresholds
```

The `--` separates `dotnet run` args from the CLI's own args. For a self-contained binary, use `dotnet publish`.

## EF Core tools

Adding a migration needs the `dotnet-ef` global tool:

```bash
dotnet tool install --global dotnet-ef --version 8.*
```

On Windows the tool installs to `%USERPROFILE%\.dotnet\tools\` but isn't always on PATH. From PowerShell, invoke it as:

```powershell
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" --version
```

See **[Adding a migration](migrations.md)** for the workflow.

## DocFX tools (for editing docs)

The docs site (this site) uses DocFX as a .NET global tool. The repo pins the version via `.config/dotnet-tools.json`:

```bash
dotnet tool restore
docfx docs/docfx.json --serve --port 8081
```

Serves at `http://localhost:8081/`. See **[Docs workflow](docs-workflow.md)**.

## IDE setup

Any C# IDE works:

- **JetBrains Rider** — best .NET-only experience. Solution file at `dotnet/Pal.sln`.
- **Visual Studio Code** with the C# Dev Kit extension — lightweight, cross-platform.
- **Visual Studio 2022** — most-featureful on Windows.

There's no recommended `.editorconfig`; .NET's defaults are accepted.

## Common first-build issues

| Symptom | Cause | Fix |
|---|---|---|
| `dotnet build` fails with "SDK 'Microsoft.NET.Sdk' not found" | Wrong SDK version | Install .NET 8 SDK (not just runtime); `dotnet --list-sdks` |
| `dotnet test` hangs at "Test run for `Pal.Api.Tests.dll`" | Docker not running | Start Docker Desktop, or filter out the Pal.Api.Tests project |
| Postgres test errors with "port 5432 in use" | Native Postgres on host | Set `POSTGRES_PORT_HOST=5433` and restart compose |
| `pack.yaml.sig` verification fails | Stale signature after pack edit | Re-sign or skip with `--require-signature` off for local dev |

## Verifying you're in a good state

After clone + build:

```bash
# Build succeeds with no errors
dotnet build dotnet/Pal.sln -c Release

# Unit tests all pass
dotnet test dotnet/Pal.sln -c Release --filter "FullyQualifiedName!~Pal.Api.Tests"
# Output ends with: Passed!  - Failed: 0, Passed: 146, Skipped: 1

# Sample CLI run produces a report
dotnet run --project dotnet/src/Pal.Cli -- analyze \
  --input fixtures/cpu-pressure/input.csv \
  --output /tmp/pal-test \
  --pack-dir packs/thresholds
ls /tmp/pal-test/input.pal-report.*
# Should list input.pal-report.json and input.pal-report.html
```

If all three work, you're ready to contribute.

## Related

- **[Testing](testing.md)** — what each test project covers.
- **[Adding a migration](migrations.md)** — when you've made a schema change.
- **[Docs workflow](docs-workflow.md)** — when you're editing docs.
- **[Operations — Installation](../operations/installation.md)** — the production install path for comparison.
