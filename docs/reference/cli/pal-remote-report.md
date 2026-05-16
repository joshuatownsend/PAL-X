---
title: pal remote report
description: Download the HTML, JSON, or Markdown report for a completed job.
---

# `pal remote report`

Download a server-rendered report artifact. The same content the [`/jobs/{id}` UI page](../../getting-started/first-analysis-remote.md#step-7--see-your-job-in-the-ui) renders, but to a file or to stdout.

## Synopsis

```text
pal remote report <job-id> [OPTIONS]
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
| `--format <html\|json\|markdown>` | `html` | Report format. |
| `-o`, `--output <PATH>` | *(stdout)* | Save the report to a file. If omitted, content is printed to stdout. |

## Examples

Save the HTML report:

```bash
pal remote report \
  --api $PAL_API --api-key $PAL_TOKEN \
  --format html --output report.html \
  9c2a14e0-...
```

Stream JSON straight into `jq` for ad-hoc inspection:

```bash
pal remote report \
  --api $PAL_API --api-key $PAL_TOKEN \
  --format json 9c2a14e0-... | jq '.summary'
```

Markdown for embedding into a ticket:

```bash
pal remote report \
  --api $PAL_API --api-key $PAL_TOKEN \
  --format markdown --output report.md \
  9c2a14e0-...
```

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Report saved or printed. |
| `2` | Malformed job ID or invalid `--format`. |
| `1` | Job not found, not yet complete (409 from server), or other server error. |

## Related

- **[pal remote results](pal-remote-results.md)** — just the findings, without the surrounding HTML/JSON envelope.
- **[pal remote dataset](pal-remote-dataset.md)** — the underlying normalized dataset, if `--include-dataset` was passed on submit.
