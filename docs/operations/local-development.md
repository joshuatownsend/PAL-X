# Local Development

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) (Windows x64 recommended)
- Git (for submodule checkout if using legacy reference)

## Quick Start

```powershell
# Clone and build
git clone https://github.com/your-org/pal-x
cd pal-x
dotnet build dotnet/Pal.sln

# Run all tests
dotnet test dotnet/Pal.sln

# Analyze a CSV log
dotnet run --project dotnet/src/Pal.Cli -- analyze `
  --input fixtures/cpu-pressure/input.csv `
  --output out `
  --pack-dir packs/thresholds

# Analyze with host context (for RAM/CPU-relative rules)
dotnet run --project dotnet/src/Pal.Cli -- analyze `
  --input fixtures/memory-pressure/input.csv `
  --output out `
  --pack-dir packs/thresholds `
  --host-memory-mb 8192 `
  --host-cpu-count 4

# Validate a pack
dotnet run --project dotnet/src/Pal.Cli -- validate-pack `
  --path packs/thresholds/windows-core

# Inspect a dataset
dotnet run --project dotnet/src/Pal.Cli -- inspect-dataset `
  --input fixtures/cpu-pressure/input.csv
```

## Converting a BLG File

BLG support is a stub in Phase 1. Use `relog.exe` (ships with Windows) to convert:

```powershell
relog -f CSV "C:\PerfLogs\server01.blg" -o "C:\PerfLogs\server01.csv"
```

Then analyze the CSV:

```powershell
dotnet run --project dotnet/src/Pal.Cli -- analyze `
  --input "C:\PerfLogs\server01.csv" `
  --output "C:\PerfLogs\out"
```

## Running the API (Docker)

To run the full API + Blazor UI locally, see `docs/operations/deployment.md`.  The short version:

```bash
cp .env.example .env  # edit to set passwords
docker compose up -d
open http://localhost:8080
```

## Project Structure

See `docs/architecture/dotnet-layout.md` for the full project dependency graph and key types.

## Writing a Pack

1. Create a directory under `packs/thresholds/<your-pack-id>/`
2. Write `pack.yaml` conforming to `dotnet/schemas/pal.pack.v1.json`
3. Validate: `pal validate-pack --path packs/thresholds/<your-pack-id>`

See existing packs (`windows-core`, `iis-core`, `sql-host-core`) as examples.

## Running Fixture Tests

The golden fixture tests live in `dotnet/tests/Pal.Cli.Tests/GoldenFixtureTests.cs`.
They locate fixtures by walking up the directory tree from the test binary to find
the repo root (where `packs/` and `fixtures/` directories exist).

```powershell
dotnet test dotnet/tests/Pal.Cli.Tests --logger "console;verbosity=detailed"
```

## Determinism

All output is deterministic given the same input. The `generated_at_utc` field is the only
non-deterministic part — override it with `--now <ISO>` for byte-comparable output:

```powershell
pal analyze --input input.csv --output out --now 2026-01-01T00:00:00Z
```
