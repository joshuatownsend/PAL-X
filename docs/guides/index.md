---
title: Guides
description: Task-oriented walkthroughs — how to accomplish a specific outcome with PAL-X.
---

# Guides

Guides are step-by-step recipes for specific tasks. Each guide assumes you've finished **[Getting Started](../getting-started/index.md)** and just need the procedure for one outcome.

If you want to understand *why* PAL-X works a particular way, see **[Concepts](../concepts/index.md)**. If you need the exhaustive field-by-field contract, see **[Reference](../reference/index.md)**.

## Capture and analyze

- **[Analyze a CSV](analyze-csv.md)** — end-to-end CSV run with output paths, useful flags, and a CI gating snippet.
- **[Analyze a BLG on Windows](analyze-blg-windows.md)** — same workflow against the binary format.
- **[Convert BLG on Linux](convert-blg-on-linux.md)** — the `relog -f CSV` fallback when the analysis host isn't Windows.
- **[Interpret the HTML report](interpret-html-report.md)** — walk through every section of the report file.

## Pack authoring

- **[Write a pack](write-a-pack.md)** — author a minimal pack from scratch.
- **[Validate a pack](validate-a-pack.md)** — `pal validate-pack` in CI and locally.
- **[Rolling-window rules](rolling-window-rules.md)** — schema v1.1 `window:` block for time-bounded aggregations.
- **[Sign and trust packs](sign-and-trust-packs.md)** — RSA-PSS-SHA256 signing for cross-team distribution.

## Baselines and comparisons

- **[Set a baseline](set-a-baseline.md)** — designate a completed job; version via `(type, contextJson)`.
- **[Compare jobs](compare-jobs.md)** — manual and auto-compare workflows; reading the diff categories.

## Analytics and diagnostics

- **[Work with trends](work-with-trends.md)** — per-rule trajectories across recent jobs.
- **[Work with correlations](work-with-correlations.md)** — metric pairs that co-vary.
- **[Use guided diagnostics](use-guided-diagnostics.md)** — per-job rule-cited insights.

## Alerting

- **[Configure alerts](configure-alerts.md)** — triage open alerts; understand the 3-of-5 escalation.
- **[Configure webhooks](configure-webhooks.md)** — register HTTP sinks; HMAC-SHA256 signing; test delivery.
- **[Schedule ingestion](schedule-ingestion.md)** — recurring directory-poller setup.

## API and edges

- **[Use the HTTP API](use-the-http-api.md)** — end-to-end automation with bash and PowerShell.
- **[Generate a Markdown report](generate-markdown-report.md)** — GFM output for PRs and chatops.
- **[Download a dataset](download-dataset.md)** — gzipped JSON snapshot for offline analysis.
