---
title: pal remote correlations
description: Show co-occurring finding pairs across the last N completed analysis jobs.
---

# `pal remote correlations`

Cross-job correlation analysis. Identifies rule pairs whose findings move together across recent history — useful for surfacing root causes vs. symptoms.

A typical pattern: "every time `low-available-memory` worsens, `high-paging-activity` also worsens" — a correlation pair the server flags so you can investigate them together.

## Synopsis

```text
pal remote correlations [OPTIONS]
```

## Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |
| `--last <N>` | `10` | Number of most-recent completed jobs to include in the correlation window. |
| `--json` | off | Print the raw correlations JSON instead of a formatted table. |

## Example

```bash
pal remote correlations \
  --api $PAL_API --api-key $PAL_TOKEN \
  --last 25
```

## What you get

For each correlated rule pair:

- The two `rule_id`s involved.
- Co-occurrence count and direction (both worsening, both improving, etc.).
- The job IDs that contributed.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Correlations retrieved. |
| `2` | Invalid `--last` value. |
| `5` | API unreachable or server error. |

## Related

- **[pal remote trends](pal-remote-trends.md)** — per-rule trajectories over the same window.
- **[pal remote diagnostics](pal-remote-diagnostics.md)** — per-job insights that draw on correlation data.
