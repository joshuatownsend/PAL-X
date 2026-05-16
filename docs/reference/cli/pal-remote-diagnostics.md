---
title: pal remote diagnostics
description: Show guided diagnostics insights for a completed job.
---

# `pal remote diagnostics`

Show rule-based, fully cited diagnostic insights for a completed job. Insights are higher-level inferences than individual findings — e.g. "memory pressure is worsening because rules A, B, and C all degraded and co-occur in correlation pairs."

Insights are produced by `IDiagnosticsService` on the server. Every insight cites the rules and metric sources that produced it; nothing is black-box.

## Synopsis

```text
pal remote diagnostics <job-id> [OPTIONS]
```

## Arguments

| Argument | Purpose |
|---|---|
| `<job-id>` | Analysis job ID (GUID). |

## Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |

## Example

```bash
pal remote diagnostics \
  --api $PAL_API --api-key $PAL_TOKEN \
  9c2a14e0-...
```

## What you get

A list of insights, each with:

- A short title and explanation.
- `AffectedRuleIds` — every rule that contributed.
- `SourceDirection` (optional) — `worsening`, `appearing`, `stable`, etc.
- Links to related findings and (when applicable) the correlation pairs that informed the insight.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Diagnostics retrieved. |
| `2` | Malformed job ID. |
| `1` | Job not found or other server error. |

## Related

- **[pal remote results](pal-remote-results.md)** — the raw findings the insights are built on.
- **[pal remote correlations](pal-remote-correlations.md)** — the cross-job pairings that some insights draw from.
- **[Guided diagnostics concept](../../getting-started/glossary.md#diagnostic-insight)** — what these insights actually are.
