---
title: pal remote results
description: Show findings from a completed analysis job.
---

# `pal remote results`

Fetch and display the findings from a completed job. The default output is a human-readable table; pass `--json` for machine-readable output.

## Synopsis

```text
pal remote results <job-id> [OPTIONS]
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
| `--json` | off | Print raw findings JSON (the same shape as the JSON report's `findings` array). |
| `--verbose` | off | Include each finding's recommendations in the table output. |

## Examples

Human-readable table:

```bash
pal remote results \
  --api $PAL_API --api-key $PAL_TOKEN \
  9c2a14e0-...
```

Same job, JSON for piping into `jq`:

```bash
pal remote results \
  --api $PAL_API --api-key $PAL_TOKEN \
  --json 9c2a14e0-... | jq '.[] | select(.severity == "critical")'
```

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Results retrieved. |
| `2` | Malformed job ID. |
| `1` | Job not found, not yet complete, or other server error. |

## Related

- **[pal remote report](pal-remote-report.md)** — full report with charts, inputs, and recommendations rendered.
- **[pal remote diagnostics](pal-remote-diagnostics.md)** — higher-level inferences across findings.
