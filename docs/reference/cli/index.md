---
title: CLI reference
description: Every pal command, every flag, every exit code.
---

# CLI reference

PAL-X ships a single binary (`pal`) built with Spectre.Console.Cli. Until packaging lands, every invocation is prefixed with `dotnet run --project dotnet/src/Pal.Cli -c Release --`. The reference pages elide that prefix for readability.

## Local commands

These run entirely on your machine — no API server required.

| Command | Purpose |
|---|---|
| **[pal analyze](pal-analyze.md)** | Analyze one input dataset and generate report artifacts. |
| **[pal validate-pack](pal-validate-pack.md)** | Validate one pack or a directory of packs. |
| **[pal inspect-dataset](pal-inspect-dataset.md)** | Import and inspect a dataset without running rules. |
| **[pal list-packs](pal-list-packs.md)** | List all packs available on the search path. |
| **[pal packs](pal-packs.md)** | Pack-management commands. Currently houses `packs sign`. |

## Remote commands

These talk to a running [PAL API server](../../getting-started/first-analysis-remote.md). See **[pal remote](pal-remote.md)** for the umbrella overview and links to every subcommand.

## Conventions

### Global flags

`pal remote` subcommands share two flags inherited from `RemoteSettings`:

| Flag | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Base URL of the PAL API server. **Must include the workspace path prefix** (e.g. `http://localhost:8080/api/workspaces/{workspaceId}`) for workspace-scoped commands. |
| `--api-key <pal_...>` | *(unset)* | Personal access token. Required for any authenticated endpoint. |

### Exit codes

PAL-X uses a small, consistent exit-code scheme across every command:

| Code | Meaning |
|---|---|
| `0` | Success. |
| `1` | Success with findings, where `--fail-on-warning` (or similar gate) was set. |
| `2` | Invalid arguments — mutually-exclusive flags, malformed GUIDs, etc. |
| `3` | Input file not found or unreadable. |
| `4` | Pack validation failed. |
| `5` | Analysis engine error. |

### Output format

By default, `pal` prints human-readable text to stdout. Commands that produce structured data (`pal list-packs`, `pal remote results`) accept a `--json-output <PATH>` or `--json` flag to emit JSON.

### Determinism

`pal analyze` writes UTF-8 without BOM and content-hash IDs (`finding_id`, `report_id`). Same inputs + same pack versions + same `--now` value produce byte-identical outputs across runs and machines.
