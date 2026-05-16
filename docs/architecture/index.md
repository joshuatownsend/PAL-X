---
title: Architecture
description: How PAL-X is structured — project map, layering, decision records, and how to read the code.
---

# Architecture

PAL-X is a .NET 8 application: a rule-based analyser for Windows performance counter captures, with a CLI for local use and an HTTP API + background workers for shared deployments. This section describes how the code is organised and the design decisions that shaped it.

If you want the user's-eye view of the system, start with **[Concepts](../concepts/index.md)**. This section is for the contributor's-eye view.

## Project map

All source lives under `dotnet/src/`. Eight projects, divided into engine / collection / persistence / surface layers:

| Project | Layer | Role |
|---|---|---|
| `Pal.Engine` | engine | Core analysis — dataset model, rule evaluator, statistics, status classifier. No I/O. |
| `Pal.Ingestion` | engine | Collectors: `CsvCollector` (cross-platform), `BlgCollector` (Windows-only via PDH interop). |
| `Pal.Packs` | engine | YAML pack loader, validator, signature verification. |
| `Pal.Reporting` | engine | JSON / HTML / Markdown report writers, ScottPlot SVG charts. |
| `Pal.Application` | shared | DTOs, service interfaces, shared cross-layer types (analytics, alerts, diagnostics, webhooks). |
| `Pal.Persistence` | infrastructure | EF Core 8 + PostgreSQL — entities, migrations, repositories. |
| `Pal.Api` | surface | ASP.NET Core minimal API, background workers, Blazor Server UI. |
| `Pal.Cli` | surface | Spectre.Console.Cli standalone tool. |

The engine layer (`Pal.Engine`, `Pal.Ingestion`, `Pal.Packs`, `Pal.Reporting`) has **no I/O** beyond reading inputs and writing reports. No HTTP, no database. It's the same code path that runs locally for `pal analyze` and remotely under `AnalysisWorker`.

The surface layer is two consumers of that engine: the CLI does it synchronously against the local filesystem; the API queues it in a worker and persists state.

## Layer dependencies

```text
Pal.Cli ─────► Pal.Reporting ─┐
   │                          ├──► Pal.Engine
   ├───────► Pal.Packs ───────┤
   └───────► Pal.Ingestion ───┘

Pal.Api ──┬──► Pal.Application ─────► Pal.Engine (model only)
          ├──► Pal.Persistence ───┐
          ├──► Pal.Packs          ├──► Pal.Application
          ├──► Pal.Ingestion      │
          ├──► Pal.Reporting      │
          └──► Pal.Engine ────────┘
```

Two rules enforce sanity:

1. `Pal.Engine` depends on nothing else in the solution. It defines the canonical model (`Dataset`, `Finding`, `RuleEngine`) and everything else consumes it.
2. `Pal.Application` doesn't reference `Pal.Persistence`. It only knows interfaces (`IAnalysisRepository`, `IAlertRepository`, etc.) — `Pal.Persistence` implements them and `Pal.Api` composes the two.

If you find yourself wanting to add a `Pal.Engine` → `Pal.Application` reference, you're probably trying to embed surface concerns into the engine; reconsider.

## Three runtime modes

1. **CLI — synchronous, in-process.** `pal analyze` calls `BlgCollector` or `CsvCollector` → `Dataset` → `RuleEngine` → `JsonReportWriter` + `HtmlReportWriter`. One process, one capture, one report.
2. **API + worker — asynchronous, persistent.** HTTP submits to `/analysis`, which enqueues a `Guid` on an in-process `Channel<Guid>`. `AnalysisWorker` (a `BackgroundService`) dequeues, runs the same engine pipeline, and persists the result via repositories. Two additional `BackgroundService` workers handle retention (`RetentionWorker`) and scheduled ingestion (`ScheduledIngestionWorker`).
3. **Blazor Server UI.** Same `Pal.Api` process — the UI uses cookie auth and the same authorisation pipeline as the API surface; data flows through the same repositories.

The shared engine code is the same in all three modes. This is the property the test suite leans on — golden fixtures run against the same `RuleEngine` that powers production.

## Pages in this section

- **[Data flow](data-flow.md)** — CSV/BLG → Dataset → RuleEngine → Findings → Report, end-to-end.
- **[Persistence](persistence.md)** — EF Core 8, Postgres, the multi-tenant query filter, the DbContextFactory pattern.
- **[Schema evolution](schema-evolution.md)** — pack v1 → v1.1 and what comes next.
- **[ADR index](adr/index.md)** — every accepted architecture decision, with status.

## Where ground truth lives

When the docs and the code disagree, the code wins. The authoritative places to look:

| Topic | Authoritative source |
|---|---|
| Pack schema | `dotnet/schemas/pal.pack.v1.json` |
| Report schema | `dotnet/schemas/pal.report.v1.json` |
| Canonical metric IDs | `dotnet/src/Pal.Engine/Normalization/MetricAliasRegistry.cs` |
| CLI flags | `dotnet/src/Pal.Cli/Commands/*.cs` |
| HTTP endpoints | `dotnet/src/Pal.Api/Endpoints/*.cs` |
| Migration list | `dotnet/src/Pal.Persistence/Migrations/` |
| Pack validator | `dotnet/src/Pal.Packs/PackValidator.cs` |
| Auth pipeline | `dotnet/src/Pal.Api/Program.cs` |
| Tenant resolution | `dotnet/src/Pal.Api/Middleware/TenantResolutionEndpointFilter.cs` |
| Analysis runner | `dotnet/src/Pal.Application/Analysis/AnalysisRunner.cs` |

If you're contributing, **read the source first**, then update docs to match. The reverse direction is harder to keep accurate.

## Related

- **[Data flow](data-flow.md)** — the runtime story.
- **[Persistence](persistence.md)** — the persistence story.
- **[ADR index](adr/index.md)** — the decisions that shaped the code.
- **[Reference — CLI](../reference/cli/index.md)** / **[HTTP API](../reference/http-api/index.md)** — the surface those decisions produced.
