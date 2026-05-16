---
title: Getting Started
description: Install PAL-X and analyze your first perfmon capture in under ten minutes.
---

# Getting Started

PAL-X has two entry points — pick whichever matches what you're trying to do right now:

| Path | When to use it |
|---|---|
| **[First analysis — local CLI](first-analysis-local.md)** | You have a single perfmon capture (CSV or BLG) and want findings on your laptop, one shot, no infrastructure. |
| **[First analysis — remote API](first-analysis-remote.md)** | You're setting up the multi-user service: an API + Postgres + a Blazor UI behind an authenticated workspace. |

Both paths build from source today. There is no pre-built `pal` binary yet — that ships with a future release. See **[Installation](installation.md)** for the prerequisites and the one-time build step.

## What PAL-X actually does

You hand PAL-X a Windows Performance Monitor capture. It loads one or more **rule packs** — YAML files describing what "bad" looks like for CPU, memory, disk, IIS, SQL Server, and so on — and produces a **report**: JSON and HTML, optionally Markdown. Each entry in the report is a **finding**, and every finding cites the rule that fired, the metric values that triggered it, and the time window where the trigger held. Nothing is summarized into a single score; you see the rules and the numbers, you make the call.

If you've used the legacy [PAL v2](https://github.com/clinthuffman/PAL) PowerShell tool, this is a from-scratch rewrite covering the same conceptual ground (rule-pack-driven analysis of perfmon captures) with a declarative pack schema, signed packs, content-hash report IDs, and a hosted API mode. See [ADR 0001](../architecture/adr/0001-deviations-from-seed-docs.md) for the design history.

## What's next on this site

- **Concepts** — the model behind packs, rules, findings, baselines, comparisons, trends, correlations, diagnostics, alerts, and multi-tenancy. *(coming soon)*
- **Guides** — task-focused how-tos: write a pack, sign a pack, set a baseline, configure an alert, etc. *(coming soon)*
- **Reference** — every CLI command, HTTP endpoint, pack-schema field, and configuration key. *(coming soon)*
- **Operations** — running the API in production. *(coming soon)*

## Need a quick word lookup?

The [Glossary](glossary.md) defines every term you'll see in this documentation and in the report itself.
