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

Field-by-field walkthrough of `pal.pack/v1` and the v1.1 rolling-window addition. *(Coming up.)*

## Report schema

Field-by-field walkthrough of `pal.report/v1`. *(Coming up.)*

## Configuration

Every `appsettings` section the API reads, with env-var equivalents. *(Coming up.)*

## Conventions

- All command examples assume your shell is at the repo root.
- PowerShell and bash examples are interchangeable unless explicitly marked. Forward slashes work in both.
- All path examples use forward slashes. Windows-specific paths (e.g. `$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe`) call out PowerShell explicitly.
- The CLI binary is invoked as `dotnet run --project dotnet/src/Pal.Cli -c Release -- <command>` until a self-contained `pal` ships. The reference pages elide that prefix for readability and assume you'll add it back.
