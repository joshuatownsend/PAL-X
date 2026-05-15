---
title: pal remote trends
description: Show finding trends across the last N completed analysis jobs.
---

# `pal remote trends`

Cross-job trend analysis. For each rule that has fired in the recent history, classify its trajectory as `improving`, `stable`, `worsening`, or `appearing`.

This is workspace-wide, not scoped to a single machine; use baseline `type`/`context` filters if you need slicing.

## Synopsis

```text
pal remote trends [OPTIONS]
```

## Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |
| `--last <N>` | `10` | Number of most-recent completed jobs to include in the trend window. |
| `--json` | off | Print the raw trends JSON instead of a formatted table. |

## Examples

Default 10-job window:

```bash
pal remote trends --api $PAL_API --api-key $PAL_TOKEN
```

Wider window for catching slow-moving regressions:

```bash
pal remote trends \
  --api $PAL_API --api-key $PAL_TOKEN \
  --last 50
```

JSON for downstream tooling:

```bash
pal remote trends \
  --api $PAL_API --api-key $PAL_TOKEN \
  --json --last 30 | jq '.[] | select(.direction == "worsening")'
```

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Trends retrieved. |
| `2` | Invalid `--last` value. |
| `5` | API unreachable or server error. |

## Related

- **[pal remote correlations](pal-remote-correlations.md)** — co-occurring finding pairs across the same window.
- **[pal remote compare](pal-remote-compare.md)** — pairwise diff between two specific jobs.
