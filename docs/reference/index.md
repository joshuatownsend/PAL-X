---
title: Reference
description: Authoritative listings of every CLI command, HTTP endpoint, schema field, and configuration key in PAL-X.
---

# Reference

Reference is for lookup, not for learning. If you want to understand a concept or accomplish a task, start with **[Getting Started](../getting-started/index.md)** or the Guides section. The pages below are the authoritative inventory of the surface PAL-X exposes.

## CLI

[Command-line interface reference](cli/index.md) — every `pal` command, every flag, every exit code.

## HTTP API

Hand-written reference for the 53 endpoints under `/api/...`. *(Coming up.)*

## Pack schema

Field-by-field walkthrough of the YAML pack format:

- **[Pack schema v1](pack-schema-v1.md)** — the base schema: packs, rules, conditions, `host_context` thresholds, recommendations.
- **[Pack schema v1.1](pack-schema-v1.1.md)** — adds rolling-window aggregations via the `window:` block.

## Report schema

**[Report schema](report-schema.md)** — field-by-field walkthrough of `pal.report/v1`: the JSON document every analysis run emits. HTML and Markdown are derived views.

## Canonical metric IDs

**[Canonical metric IDs](metric-ids.md)** — the snake_case identifiers PAL-X recognises and how they map to Windows counter paths. Covers Processor, Memory, PhysicalDisk, Network, Process, SQL Server (default and named instances), and IIS/ASP.NET.

## Configuration

**[Configuration](configuration.md)** — every `appsettings` section the API reads (`ConnectionStrings`, `Storage`, `Packs`, `Retention`, `Schedules`, `Logging`) with environment-variable equivalents and a production-ready example.

## Exit codes

**[Exit codes](exit-codes.md)** — every code the `pal` CLI emits, mapped to its meaning and the commands that can fire it.

## Conventions

- All command examples assume your shell is at the repo root.
- PowerShell and bash examples are interchangeable unless explicitly marked. Forward slashes work in both.
- All path examples use forward slashes. Windows-specific paths (e.g. `$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe`) call out PowerShell explicitly.
- The CLI binary is invoked as `dotnet run --project dotnet/src/Pal.Cli -c Release -- <command>` until a self-contained `pal` ships. The reference pages elide that prefix for readability and assume you'll add it back.
