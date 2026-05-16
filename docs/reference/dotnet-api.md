---
title: .NET API
description: Auto-generated reference for the public .NET types in Pal.Application, Pal.Engine, Pal.Packs, and Pal.Reporting.
---

# .NET API reference

The pages under this section are **auto-generated** from XML doc comments in the four library projects whose public surface is most likely to be consumed by embedders:

| Project | Why it's documented |
|---|---|
| `Pal.Application` | DTOs and service interfaces — the contracts between engine and surface layers. |
| `Pal.Engine` | Core analysis surface — `Dataset`, `Finding`, `RuleEngine`, model types. Most embedders only need this. |
| `Pal.Packs` | Pack loading and validation — `PackLoader`, `PackValidator`. |
| `Pal.Reporting` | Report writers — `JsonReportWriter`, `HtmlReportWriter`, `MarkdownReportWriter`, and chart canonicalisation. |

Browse the generated reference via the **"API (auto-generated)"** entry in the left navigation.

`Pal.Ingestion`, `Pal.Persistence`, `Pal.Api`, and `Pal.Cli` are intentionally **not** in the auto-generated reference. They're either surface concerns (CLI, API endpoints) or carry implementation details that would distract from the embedder use case.

## Coverage today

XML doc coverage is **patchy**. The `CS1591` warning ("missing XML doc on public member") is suppressed in the four documented projects so the build succeeds with partial coverage. Pages render whatever summaries exist; types without summaries show only their signatures.

This is by design for the bring-up — we publish whatever's documented today and let coverage improve incrementally. To contribute a summary, edit the relevant `.cs` file and add a `/// <summary>` block; it lands in this reference on the next build.

## Browsing

- **By namespace.** Use the left nav under "API". DocFX flattens nested namespaces so `Pal.Application.Alerts.Policy` appears alongside `Pal.Application.Alerts`.
- **By type.** Search (top of page) finds types by name.

## Most useful entry points

| Looking for... | Start at... |
|---|---|
| The core rule evaluator | `Pal.Engine.Rules.RuleEngine` |
| The dataset / finding model | `Pal.Engine.Model` namespace |
| The pack loader and validator | `Pal.Packs.PackLoader`, `Pal.Packs.PackValidator` |
| Canonical metric ID handling | `Pal.Engine.Normalization.CanonicalMetricId`, `MetricAliasRegistry` |
| Service contracts the API consumes | `Pal.Application` interfaces (`IAnalysisRepository`, `IAlertService`, etc.) |
| Report writer entry points | `Pal.Reporting.Json.JsonReportWriter`, `Pal.Reporting.Html.HtmlReportWriter`, `Pal.Reporting.Markdown.MarkdownReportWriter` |

## Related

- **[Architecture — Project map](../architecture/index.md#project-map)** — the wider project layering.
- **[Architecture — Data flow](../architecture/data-flow.md)** — how these types connect at runtime.
- **[Pack schema v1](pack-schema-v1.md)** / **[Report schema](report-schema.md)** — the data formats these APIs read and write.
